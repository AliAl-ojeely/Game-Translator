using System.Windows;
using GameTranslator.Models;

namespace GameTranslator
{
    public partial class SettingsWindow : Window
    {
        // Property to hold the settings modified by the user
        public TranslationSettings CurrentSettings { get; private set; }

        public SettingsWindow(TranslationSettings existingSettings)
        {
            InitializeComponent();

            // Clone the existing settings so we don't modify the original until 'OK' is clicked
            CurrentSettings = existingSettings ?? new TranslationSettings();

            // Populate the UI fields with the current settings
            TxtEndpoint.Text = CurrentSettings.EndpointUrl;
            TxtModel.Text = CurrentSettings.ModelName;
            TxtDelay.Text = CurrentSettings.DelayInSeconds.ToString();
            TxtMaxBytes.Text = CurrentSettings.MaxBytes.ToString();
            TxtPromptTemplate.Text = CurrentSettings.PromptTemplate;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            // Save UI values back to the settings object
            CurrentSettings.EndpointUrl = TxtEndpoint.Text;
            CurrentSettings.ModelName = TxtModel.Text;
            CurrentSettings.PromptTemplate = TxtPromptTemplate.Text;

            if (int.TryParse(TxtDelay.Text, out int delay))
                CurrentSettings.DelayInSeconds = delay;

            if (int.TryParse(TxtMaxBytes.Text, out int maxBytes))
                CurrentSettings.MaxBytes = maxBytes;

            // Mark dialog as successful and close
            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            // Close without saving
            this.DialogResult = false;
            this.Close();
        }
    }
}