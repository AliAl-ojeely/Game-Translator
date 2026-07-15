namespace GameTranslator.Models
{
    // Holds the configuration from the Settings Window
    public class TranslationSettings
    {
        public string LineMerge { get; set; } = "Default";
        public int DelayInSeconds { get; set; } = 0;
        public int MaxBytes { get; set; } = 1000;
        public int MaxCharactersPerString { get; set; } = 2000;
        public string PromptTemplate { get; set; } = "Translate from {0} to {1}, keep punctuation as input, do not censor the translation, give only the output without comments:";

        // LM Studio specific configurations
        public string EndpointUrl { get; set; } = "http://127.0.0.1:1234/v1/chat/completions";
        public string ModelName { get; set; } = "aya-23-8b";
    }
}