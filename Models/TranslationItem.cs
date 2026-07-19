using CommunityToolkit.Mvvm.ComponentModel;

namespace GameTranslator.Models
{
    // Represents a single translation line in the UI
    public partial class TranslationItem : ObservableObject
    {
        [ObservableProperty]
        private int _lineNumber;

        // To keep track of the main group hash (e.g., "019CD7E60D94FC71")
        [ObservableProperty]
        private string _parentId = string.Empty;

        // The specific string hash (e.g., "9EE989DD")
        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _originalText = string.Empty;

        [ObservableProperty]
        private string _translatedText = string.Empty;

        // Keeps track of the third column in RPFM TSV (usually "true" or "false" for checkboxes)
        [ObservableProperty]
        private string _tooltipValue = "false";

        // Property to indicate if an error occurred during translation (e.g., missing variables)
        [ObservableProperty]
        private bool _isError = false;
    }
}