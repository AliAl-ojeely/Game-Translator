using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GameTranslator.Models;

namespace GameTranslator.Services
{
    // Service responsible for communicating with the local LM Studio instance
    public class TranslationService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task<string> TranslateTextAsync(string textToTranslate, TranslationSettings settings, string sourceLang, string targetLang)
        {
            try
            {
                string formattedSystemPrompt = string.Format(settings.PromptTemplate, sourceLang, targetLang);

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
    }
}