using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameTranslator.Models;
using GameTranslator.Services;

namespace GameTranslator.ViewModels
{
    public class TranslationReport
    {
        public string FileName { get; set; } = string.Empty;
        public int TranslatedCount { get; set; }
        public int TranslatedCharacters { get; set; }
        public TimeSpan TimeTaken { get; set; }
        public List<string> SkippedStringIds { get; set; } = new();
    }

    public partial class MainViewModel : ObservableObject
    {
        private readonly TranslationService _translationService;
        private readonly SettingsService _settingsService;
        private CancellationTokenSource _cancellationTokenSource;

        [ObservableProperty]
        private ObservableCollection<TranslationItem> _translationItems = new();

        [ObservableProperty]
        private TranslationSettings _appSettings = new TranslationSettings();

        [ObservableProperty]
        private string _selectedSourceLanguage = "English";

        [ObservableProperty]
        private string _selectedTargetLanguage = "Arabic";

        [ObservableProperty]
        private bool _isTranslating = false;

        [ObservableProperty]
        private string _currentFilePath = string.Empty;

        [ObservableProperty]
        private bool _isTranslationFinished = false;

        // Progress and Report Properties
        [ObservableProperty]
        private double _progressPercentage = 0;

        [ObservableProperty]
        private string _estimatedTimeRemaining = "Calculating...";

        public TranslationReport LastReport { get; private set; } = new();

        public MainViewModel()
        {
            _translationService = new TranslationService();
            _settingsService = new SettingsService();
            AppSettings = _settingsService.LoadSettings();
        }

        [RelayCommand]
        private void OpenFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "JSON Game Files (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Select Game Localization File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    CurrentFilePath = openFileDialog.FileName;
                    IsTranslationFinished = false;
                    ProgressPercentage = 0;
                    EstimatedTimeRemaining = "";

                    string jsonContent = File.ReadAllText(openFileDialog.FileName);
                    var parsedData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(jsonContent);

                    TranslationItems.Clear();

                    if (parsedData != null)
                    {
                        int lineCounter = 1;

                        foreach (var group in parsedData)
                        {
                            foreach (var item in group.Value)
                            {
                                TranslationItems.Add(new TranslationItem
                                {
                                    LineNumber = lineCounter++,
                                    ParentId = group.Key,
                                    Id = item.Key,
                                    OriginalText = item.Value,
                                    TranslatedText = string.Empty
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    new CustomAlertWindow("Error", $"Error reading file: {ex.Message}").ShowDialog();
                }
            }
        }

        [RelayCommand]
        private void CancelTranslation()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }
        }

        [RelayCommand]
        private async Task StartTranslationAsync()
        {
            if (IsTranslating) return;

            if (TranslationItems.Count == 0)
            {
                new CustomAlertWindow("No File Selected", "Please select a game localization file to translate first.").ShowDialog();
                OpenFile();
                return;
            }

            IsTranslating = true;
            IsTranslationFinished = false;
            ProgressPercentage = 0;

            _cancellationTokenSource = new CancellationTokenSource();

            LastReport = new TranslationReport
            {
                FileName = Path.GetFileName(CurrentFilePath)
            };

            Stopwatch stopwatch = Stopwatch.StartNew();
            int totalItems = TranslationItems.Count;
            int currentIndex = 0;

            foreach (var item in TranslationItems)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    break;
                }

                currentIndex++;

                if (!string.IsNullOrWhiteSpace(item.TranslatedText) && item.TranslatedText != "Translating...")
                {
                    UpdateProgress(currentIndex, totalItems, stopwatch.Elapsed);
                    continue;
                }

                if (item.OriginalText.Length > 2000)
                {
                    item.TranslatedText = "[Skipped: Text too long]";
                    LastReport.SkippedStringIds.Add(item.Id);
                    UpdateProgress(currentIndex, totalItems, stopwatch.Elapsed);
                    continue;
                }

                item.TranslatedText = "Translating...";

                string result = await _translationService.TranslateTextAsync(
                    item.OriginalText,
                    AppSettings,
                    SelectedSourceLanguage,
                    SelectedTargetLanguage);

                item.TranslatedText = result;

                if (!result.StartsWith("[Error") && !result.StartsWith("[Skipped"))
                {
                    LastReport.TranslatedCount++;
                    LastReport.TranslatedCharacters += result.Length;
                }

                UpdateProgress(currentIndex, totalItems, stopwatch.Elapsed);

                if (AppSettings.DelayInSeconds > 0)
                {
                    try
                    {
                        await Task.Delay(AppSettings.DelayInSeconds * 1000, _cancellationTokenSource.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }

            stopwatch.Stop();
            LastReport.TimeTaken = stopwatch.Elapsed;

            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;

            IsTranslating = false;
            IsTranslationFinished = true;

            System.Media.SystemSounds.Asterisk.Play();
            new CustomAlertWindow("Translation Complete", "The translation process has finished successfully!").ShowDialog();
        }

        private void UpdateProgress(int current, int total, TimeSpan elapsed)
        {
            ProgressPercentage = (double)current / total * 100;

            if (current > 5)
            {
                double itemsPerSecond = current / elapsed.TotalSeconds;
                double remainingSeconds = (total - current) / itemsPerSecond;
                TimeSpan eta = TimeSpan.FromSeconds(remainingSeconds);
                EstimatedTimeRemaining = $"ETA: {eta:hh\\:mm\\:ss}";
            }
            else
            {
                EstimatedTimeRemaining = "Calculating ETA...";
            }
        }

        public void SaveCurrentSettings()
        {
            _settingsService.SaveSettings(AppSettings);
        }

        public void SaveFile(SaveAction action)
        {
            if (string.IsNullOrEmpty(CurrentFilePath) || TranslationItems.Count == 0) return;

            var outputData = new Dictionary<string, Dictionary<string, string>>();

            foreach (var item in TranslationItems)
            {
                if (!outputData.ContainsKey(item.ParentId))
                    outputData[item.ParentId] = new Dictionary<string, string>();

                string finalString = item.OriginalText;
                if (!string.IsNullOrWhiteSpace(item.TranslatedText) &&
                    item.TranslatedText != "Translating..." &&
                    item.TranslatedText != "Waiting for translation..." &&
                    !item.TranslatedText.StartsWith("[Skipped"))
                {
                    finalString = item.TranslatedText;
                }

                outputData[item.ParentId][item.Id] = finalString;
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string jsonOutput = JsonSerializer.Serialize(outputData, options);

            try
            {
                if (action == SaveAction.Overwrite)
                {
                    File.WriteAllText(CurrentFilePath, jsonOutput);
                    new CustomAlertWindow("Saved", "Original file overwritten successfully!").ShowDialog();
                }
                else if (action == SaveAction.SaveAsNew)
                {
                    var saveDialog = new Microsoft.Win32.SaveFileDialog
                    {
                        FileName = Path.GetFileNameWithoutExtension(CurrentFilePath) + "_ar" + Path.GetExtension(CurrentFilePath),
                        Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                        InitialDirectory = Path.GetDirectoryName(CurrentFilePath)
                    };

                    if (saveDialog.ShowDialog() == true)
                    {
                        File.WriteAllText(saveDialog.FileName, jsonOutput);
                        new CustomAlertWindow("Saved", "New file saved successfully!").ShowDialog();
                    }
                }
            }
            catch (Exception ex)
            {
                new CustomAlertWindow("Error", $"Error saving file: {ex.Message}").ShowDialog();
            }
        }
    }
}