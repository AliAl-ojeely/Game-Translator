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
using System.Windows;
using System.Linq;
using System.Text.RegularExpressions;

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

        [ObservableProperty]
        private string _textToClean = "";

        [ObservableProperty]
        private string _findText = string.Empty;

        [ObservableProperty]
        private string _replaceText = string.Empty;

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
                // Updated filter to officially support *.txt selection
                Filter = "All Supported Files|*.csv;*.json;*.tsv;*.loc.tsv;*.txt|Text Files (*.txt)|*.txt|TSV Files (*.tsv;*.loc.tsv)|*.tsv;*.loc.tsv|JSON Files (*.json)|*.json|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
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
                    bool isLocTsv = CurrentFilePath.EndsWith(".loc.tsv", StringComparison.OrdinalIgnoreCase);

                    if (extension == ".tsv" || isLocTsv)
                    {
                        LoadTsvFile(CurrentFilePath);
                        return;
                    }

                    List<TranslationItem> items = new List<TranslationItem>();

                    if (extension == ".csv")
                    {
                        items = _translationService.LoadCsv(CurrentFilePath);
                    }
                    else if (extension == ".json")
                    {
                        items = _translationService.LoadJson(CurrentFilePath);
                    }
                    else if (extension == ".txt")
                    {
                        items = _translationService.LoadTxt(CurrentFilePath);
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
        private void CleanTranslatedText()
        {
            if (string.IsNullOrWhiteSpace(TextToClean) || TranslationItems.Count == 0)
            {
                new CustomAlertWindow("Alert", "Please select a file and enter the text to be cleaned.").ShowDialog();
                return;
            }

            int cleanedCount = 0;

            foreach (var item in TranslationItems)
            {
                if (!string.IsNullOrEmpty(item.TranslatedText) && item.TranslatedText.Contains(TextToClean))
                {
                    item.TranslatedText = item.TranslatedText.Replace(TextToClean, "").Trim();
                    cleanedCount++;
                }
            }

            if (cleanedCount > 0)
            {
                var editableCollectionView = TranslationItemsView as IEditableCollectionView;

                if (editableCollectionView != null)
                {
                    if (editableCollectionView.IsEditingItem)
                        editableCollectionView.CommitEdit();

                    if (editableCollectionView.IsAddingNew)
                        editableCollectionView.CommitEdit();
                }

                TranslationItemsView.Refresh();
                new CustomAlertWindow("Cleanup Complete", $"Text found and removed from {cleanedCount} lines successfully!").ShowDialog();
            }
            else
            {
                new CustomAlertWindow("Search Result", "No texts containing this word or symbol were found.").ShowDialog();
            }
        }

        [RelayCommand]
        private void ClearAllTranslations()
        {
            if (TranslationItems.Count == 0)
            {
                new CustomAlertWindow("Alert", "Please select a game localization file first.").ShowDialog();
                return;
            }

            bool hasTranslations = TranslationItems.Any(item => !string.IsNullOrWhiteSpace(item.TranslatedText));

            if (!hasTranslations)
            {
                new CustomAlertWindow("Information", "There is no translated text yet.\nSo there is nothing need to be clear.").ShowDialog();
                return;
            }

            var confirmWindow = new CustomConfirmWindow("Confirm Clear", "Are you sure you want to clear ALL translations?\nThis action cannot be undone.");

            if (confirmWindow.ShowDialog() == true)
            {
                int count = 0;
                foreach (var item in TranslationItems)
                {
                    if (!string.IsNullOrEmpty(item.TranslatedText))
                    {
                        item.TranslatedText = string.Empty;
                        item.IsError = false;
                        count++;
                    }
                }

                if (count > 0)
                {
                    TranslationItemsView.Refresh();
                    new CustomAlertWindow("Cleared", $"All {count} translations have been cleared successfully.").ShowDialog();
                }
            }
        }

        private void LoadTsvFile(string filePath)
        {
            try
            {
                var lines = File.ReadAllLines(filePath, System.Text.Encoding.UTF8);
                TranslationItems.Clear();
                int lineNumber = 1;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var columns = line.Split('\t');

                    if (columns.Length >= 3)
                    {
                        string keyId = columns[0].Trim();
                        string originalTxt = columns[1].Trim();
                        string tooltipVal = columns[2].Trim();

                        if (keyId.Equals("Key", StringComparison.OrdinalIgnoreCase) ||
                            keyId.StartsWith("TSV") ||
                            keyId.StartsWith("#") ||
                            string.IsNullOrWhiteSpace(keyId))
                        {
                            continue;
                        }

                        if (originalTxt.StartsWith("\"") && originalTxt.EndsWith("\""))
                        {
                            originalTxt = originalTxt.Substring(1, originalTxt.Length - 2);
                        }

                        originalTxt = originalTxt.Replace("\\n", "\n").Replace("\\t", "\t");

                        TranslationItems.Add(new TranslationItem
                        {
                            LineNumber = lineNumber++,
                            Id = keyId,
                            OriginalText = originalTxt,
                            TranslatedText = string.Empty,
                            TooltipValue = string.IsNullOrWhiteSpace(tooltipVal) ? "false" : tooltipVal,
                            IsError = false
                        });
                    }
                }

                TranslationItemsView.Refresh();
            }
            catch (Exception ex)
            {
                new CustomAlertWindow("Error", $"Failed to load TSV file:\n{ex.Message}").ShowDialog();
            }
        }

        [RelayCommand]
        private void FindAndReplace()
        {
            if (string.IsNullOrWhiteSpace(FindText) || TranslationItems.Count == 0)
            {
                new CustomAlertWindow("Alert", "Please enter the text you want to find.").ShowDialog();
                return;
            }

            int replacedCount = 0;

            foreach (var item in TranslationItems)
            {
                if (!string.IsNullOrEmpty(item.TranslatedText) && item.TranslatedText.Contains(FindText))
                {
                    item.TranslatedText = item.TranslatedText.Replace(FindText, ReplaceText);
                    replacedCount++;
                }
            }

            if (replacedCount > 0)
            {
                TranslationItemsView.Refresh();
                new CustomAlertWindow("Success", $"Replaced '{FindText}' with '{ReplaceText}' in {replacedCount} lines.").ShowDialog();
            }
            else
            {
                new CustomAlertWindow("Not Found", $"The text '{FindText}' was not found in the translated column.").ShowDialog();
            }
        }

        [RelayCommand]
        private void OpenFindReplaceWindow()
        {
            var window = new Views.FindAndReplaceWindow(this);
            window.Owner = Application.Current.MainWindow;
            window.ShowDialog();
        }

        [RelayCommand]
        private void CancelTranslation()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }
        }

        // Helper method for smart text checking
        private bool IsOnlyVariablesOrSymbols(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;

            // Remove all content within {} and HTML tags temporarily for evaluation
            string cleanText = Regex.Replace(text, @"\{.*?\}|<.*?>", "");

            // Check if the remaining text contains any alphabetic letters from any language
            bool hasLetters = Regex.IsMatch(cleanText, @"\p{L}");

            // If there are no letters left, the line consists only of symbols or variables
            return !hasLetters;
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

                // Wrap the translation process in a try-catch to ensure the loop never stops on a single failure
                try
                {
                    if (!string.IsNullOrWhiteSpace(item.TranslatedText) && item.TranslatedText != "Translating...")
                    {
                        UpdateProgress(currentIndex, totalItems, stopwatch.Elapsed);
                        continue;
                    }

                    // Smart check to skip lines containing only variables, brackets, or symbols
                    if (IsOnlyVariablesOrSymbols(item.OriginalText))
                    {
                        item.TranslatedText = item.OriginalText;
                        item.IsError = false;
                        LastReport.TranslatedCount++;
                        UpdateProgress(currentIndex, totalItems, stopwatch.Elapsed);
                        continue;
                    }

                    if (item.OriginalText.Length > AppSettings.MaxCharactersPerString)
                    {
                        item.TranslatedText = "[Skipped: Text too long]";
                        item.IsError = true; // Marks the row in red
                        LastReport.SkippedStringIds.Add(item.Id);
                        UpdateProgress(currentIndex, totalItems, stopwatch.Elapsed);
                        continue; // Proceed to the next line without crashing the app
                    }

                    item.TranslatedText = "Translating...";

                    // Send request to the model
                    string result = await _translationService.TranslateTextAsync(
                        item.OriginalText,
                        AppSettings,
                        SelectedSourceLanguage,
                        SelectedTargetLanguage);

                    // Error validation logic
                    bool hasError = false;

                    if (string.IsNullOrWhiteSpace(result) || result.StartsWith("[Error") || result.StartsWith("[Skipped"))
                    {
                        hasError = true;
                    }
                    else if (item.OriginalText.Contains("{") && !result.Contains("{"))
                    {
                        hasError = true; // Model forgot the brackets
                    }

                    item.IsError = hasError;
                    item.TranslatedText = result;

                    if (!hasError)
                    {
                        LastReport.TranslatedCount++;
                        LastReport.TranslatedCharacters += result.Length;
                    }

                    UpdateProgress(currentIndex, totalItems, stopwatch.Elapsed);

                    if (AppSettings.DelayInSeconds > 0)
                    {
                        await Task.Delay(AppSettings.DelayInSeconds * 1000, _cancellationTokenSource.Token);
                    }
                }
                catch (TaskCanceledException)
                {
                    // Manually canceled
                    break;
                }
                catch (Exception ex)
                {
                    // === Robust fail-safe mechanism ===
                    // If any unexpected error occurs, the app will not crash
                    item.IsError = true;
                    item.TranslatedText = $"[Error: {ex.Message}]";
                    UpdateProgress(currentIndex, totalItems, stopwatch.Elapsed);
                    continue; // Skip the broken line and move immediately to the next one
                }
            }

            stopwatch.Stop();
            LastReport.TimeTaken = stopwatch.Elapsed;

            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;

            IsTranslating = false;
            IsTranslationFinished = true;

            System.Media.SystemSounds.Asterisk.Play();
            new CustomAlertWindow("Translation Complete", "The translation process has finished! Check any red rows for errors.").ShowDialog();
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

                if (action == SaveAction.SaveAsNew)
                {
                    string defaultExt = ".txt";
                    string filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";

                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(CurrentFilePath);

                    var saveDialog = new SaveFileDialog
                    {
                        FileName = (fileNameWithoutExt ?? "Translated") + "_ar" + defaultExt,
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
                else if (action == SaveAction.Overwrite)
                {
                    // Forces an overwrite specifically to .txt format
                    targetPath = Path.ChangeExtension(CurrentFilePath, ".txt");
                }

                _translationService.SaveTxt(targetPath, TranslationItems);

                new CustomAlertWindow("Saved", action == SaveAction.Overwrite ? "Text file overwritten successfully!" : "Text file saved successfully!").ShowDialog();
            }
            catch (Exception ex)
            {
                new CustomAlertWindow("Error", $"Error saving file: {ex.Message}").ShowDialog();
            }
        }
    }
}