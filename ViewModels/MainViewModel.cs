using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.ComponentModel;
using System.Windows.Data;
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
        private CancellationTokenSource? _cancellationTokenSource;

        [ObservableProperty]
        private ObservableCollection<TranslationItem> _translationItems = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        public ICollectionView TranslationItemsView { get; private set; }

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

            TranslationItemsView = CollectionViewSource.GetDefaultView(_translationItems);
            TranslationItemsView.Filter = FilterTranslationItems;
        }

        partial void OnSearchTextChanged(string value)
        {
            TranslationItemsView.Refresh();
        }

        private bool FilterTranslationItems(object obj)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            if (obj is TranslationItem item)
            {
                string searchLower = SearchText.ToLowerInvariant();
                return (item.OriginalText != null && item.OriginalText.ToLowerInvariant().Contains(searchLower)) ||
                       (item.TranslatedText != null && item.TranslatedText.ToLowerInvariant().Contains(searchLower)) ||
                       (item.Id != null && item.Id.ToLowerInvariant().Contains(searchLower)) ||
                       (item.ParentId != null && item.ParentId.ToLowerInvariant().Contains(searchLower));
            }
            return false;
        }

        [RelayCommand]
        private void OpenFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Supported Files (*.json;*.csv)|*.json;*.csv|JSON Files (*.json)|*.json|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
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
                    SearchText = string.Empty;

                    string extension = Path.GetExtension(CurrentFilePath).ToLower();
                    List<TranslationItem> items = new List<TranslationItem>();

                    if (extension == ".csv")
                    {
                        items = _translationService.LoadCsv(CurrentFilePath);
                    }
                    else if (extension == ".json")
                    {
                        items = _translationService.LoadJson(CurrentFilePath);
                    }

                    TranslationItems.Clear();
                    foreach (var item in items)
                    {
                        TranslationItems.Add(item);
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
                FileName = Path.GetFileName(CurrentFilePath) ?? "Unknown"
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

                if (item.OriginalText.Length > AppSettings.MaxCharactersPerString)
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

            try
            {
                string targetPath = CurrentFilePath;
                string currentExtension = Path.GetExtension(CurrentFilePath).ToLower();

                if (action == SaveAction.SaveAsNew)
                {
                    string defaultExt = currentExtension == ".csv" ? ".csv" : ".json";
                    string filter = currentExtension == ".csv"
                        ? "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"
                        : "JSON Files (*.json)|*.json|All Files (*.*)|*.*";

                    var saveDialog = new Microsoft.Win32.SaveFileDialog
                    {
                        FileName = (Path.GetFileNameWithoutExtension(CurrentFilePath) ?? "Translated") + "_ar" + defaultExt,
                        Filter = filter,
                        InitialDirectory = Path.GetDirectoryName(CurrentFilePath) ?? string.Empty
                    };

                    if (saveDialog.ShowDialog() == true)
                    {
                        targetPath = saveDialog.FileName;
                    }
                    else
                    {
                        return;
                    }
                }

                string targetExtension = Path.GetExtension(targetPath).ToLower();

                if (targetExtension == ".csv")
                {
                    _translationService.SaveCsv(targetPath, TranslationItems);
                }
                else
                {
                    _translationService.SaveJson(targetPath, TranslationItems);
                }

                new CustomAlertWindow("Saved", action == SaveAction.Overwrite ? "Original file overwritten successfully!" : "New file saved successfully!").ShowDialog();
            }
            catch (Exception ex)
            {
                new CustomAlertWindow("Error", $"Error saving file: {ex.Message}").ShowDialog();
            }
        }
    }
}