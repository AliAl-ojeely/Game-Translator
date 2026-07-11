using System.IO;
using System.Text.Json;
using GameTranslator.Models;

namespace GameTranslator.Services
{
    // Service responsible for handling local configuration persistence
    public class SettingsService
    {
        private readonly string _settingsFilePath = "settings.json";

        public TranslationSettings LoadSettings()
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new TranslationSettings();
            }

            try
            {
                string jsonString = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<TranslationSettings>(jsonString);
                return settings ?? new TranslationSettings();
            }
            catch
            {
                return new TranslationSettings();
            }
        }

        public void SaveSettings(TranslationSettings settings)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(_settingsFilePath, jsonString);
        }
    }
}