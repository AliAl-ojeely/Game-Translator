using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using GameTranslator.Models;

namespace GameTranslator.Services
{
    public class TranslationService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task<string> TranslateTextAsync(string textToTranslate, TranslationSettings settings, string sourceLang, string targetLang)
        {
            try
            {
                string formattedSystemPrompt;

                if (textToTranslate.Contains("{") || textToTranslate.Contains("<lf>") || textToTranslate.Contains("<br />"))
                {
                    formattedSystemPrompt = string.Format(settings.PromptTemplate, sourceLang, targetLang);
                }
                else
                {
                    formattedSystemPrompt = $"You are a video game localizer. Translate the short UI text from {sourceLang} to {targetLang}. Respond ONLY with the raw translation. Do NOT add timestamps, tags, quotes, or explanations.";
                }

                var requestPayload = new ChatRequest
                {
                    Model = settings.ModelName,
                    Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = formattedSystemPrompt },
                new ChatMessage { Role = "user", Content = textToTranslate }
            }
                };

                string jsonContent = JsonSerializer.Serialize(requestPayload);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(settings.EndpointUrl, httpContent);
                response.EnsureSuccessStatusCode();

                string responseString = await response.Content.ReadAsStringAsync();
                var responseObject = JsonSerializer.Deserialize<ChatResponse>(responseString);

                return responseObject?.Choices?.FirstOrDefault()?.Message?.Content?.Trim() ?? "[No Output]";
            }
            catch (HttpRequestException)
            {
                return "[Error: Make sure LM Studio server is running]";
            }
            catch (Exception ex)
            {
                return $"[Error: {ex.Message}]";
            }
        }

        public List<TranslationItem> LoadJson(string filePath)
        {
            var items = new List<TranslationItem>();
            string jsonContent = File.ReadAllText(filePath);
            var parsedData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(jsonContent);

            if (parsedData != null)
            {
                int lineCounter = 1;
                foreach (var group in parsedData)
                {
                    foreach (var item in group.Value)
                    {
                        items.Add(new TranslationItem
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
            return items;
        }

        public void SaveJson(string filePath, IEnumerable<TranslationItem> items)
        {
            var outputData = new Dictionary<string, Dictionary<string, string>>();

            foreach (var item in items)
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
            File.WriteAllText(filePath, jsonOutput);
        }

        public List<TranslationItem> LoadCsv(string filePath)
        {
            var items = new List<TranslationItem>();
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Encoding = Encoding.UTF8,
                BadDataFound = null
            };

            using (var reader = new StreamReader(filePath, Encoding.UTF8))
            using (var csv = new CsvReader(reader, config))
            {
                csv.Read();
                csv.ReadHeader();

                int lineCounter = 1;
                while (csv.Read())
                {
                    string fullKey = csv.GetField<string>(0) ?? string.Empty;
                    string source = csv.GetField<string>(1) ?? string.Empty;
                    string translation = csv.TryGetField<string>(2, out string? t) ? (t ?? string.Empty) : string.Empty;

                    string parentId = fullKey;
                    string id = string.Empty;

                    int separatorIndex = fullKey.IndexOf("::");
                    if (separatorIndex >= 0)
                    {
                        parentId = fullKey.Substring(0, separatorIndex);
                        id = fullKey.Substring(separatorIndex + 2);
                    }

                    items.Add(new TranslationItem
                    {
                        LineNumber = lineCounter++,
                        ParentId = parentId,
                        Id = id,
                        OriginalText = source,
                        TranslatedText = translation
                    });
                }
            }
            return items;
        }

        public void SaveCsv(string filePath, IEnumerable<TranslationItem> items)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Encoding = Encoding.UTF8
            };

            using (var writer = new StreamWriter(filePath, false, new UTF8Encoding(true)))
            using (var csv = new CsvWriter(writer, config))
            {
                csv.WriteField("Key");
                csv.WriteField("Source");
                csv.WriteField("Translation");
                csv.NextRecord();

                foreach (var item in items)
                {
                    string fullKey = string.IsNullOrEmpty(item.Id) ? item.ParentId : $"{item.ParentId}::{item.Id}";

                    csv.WriteField(fullKey);
                    csv.WriteField(item.OriginalText);

                    string finalString = item.OriginalText;
                    if (!string.IsNullOrWhiteSpace(item.TranslatedText) &&
                        item.TranslatedText != "Translating..." &&
                        item.TranslatedText != "Waiting for translation..." &&
                        !item.TranslatedText.StartsWith("[Skipped"))
                    {
                        finalString = item.TranslatedText;
                    }

                    csv.WriteField(finalString);
                    csv.NextRecord();
                }
            }
        }

        public void SaveTsv(string filePath, IEnumerable<TranslationItem> items)
        {
            var utf8WithoutBom = new UTF8Encoding(false);

            using (var writer = new StreamWriter(filePath, false, utf8WithoutBom))
            {
                writer.NewLine = "\n";

                writer.WriteLine("key\ttext\ttooltip");

                string locFileName = Path.GetFileNameWithoutExtension(filePath);
                writer.WriteLine($"#Loc;1;text/db/{locFileName}");

                foreach (var item in items)
                {
                    string finalTxt = string.IsNullOrWhiteSpace(item.TranslatedText)
                                      ? item.OriginalText
                                      : item.TranslatedText;

                    finalTxt = finalTxt.Replace("\t", " ").Replace("\n", "\\n").Replace("\r", "");
                    string tooltip = string.IsNullOrWhiteSpace(item.TooltipValue) ? "false" : item.TooltipValue;

                    writer.WriteLine($"{item.Id}\t{finalTxt}\t{tooltip}");
                }
            }
        }
    }
}