using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using System.Net.Http.Headers;

namespace CPTHub.Services
{
    public class LearnLMService : ILearnLMService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LearnLMService> _logger;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly string _model;

        public LearnLMService(HttpClient httpClient, IConfiguration configuration, ILogger<LearnLMService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            
            _apiKey = _configuration["GoogleLearnLM:ApiKey"] ?? throw new InvalidOperationException("Google Learn LM API key not configured");
            _baseUrl = _configuration["GoogleLearnLM:BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta";
            _model = _configuration["GoogleLearnLM:Model"] ?? "models/gemini-1.5-flash-latest";

            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            _httpClient.BaseAddress = new Uri(_baseUrl);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            var timeout = _configuration.GetValue<int>("GoogleLearnLM:TimeoutSeconds", 30);
            _httpClient.Timeout = TimeSpan.FromSeconds(timeout);
        }

        public async Task<string> SendPromptAsync(string prompt, string context = "")
        {
            try
            {
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = BuildNASMContextualPrompt(prompt, context) }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.3,
                        topK = 40,
                        topP = 0.95,
                        maxOutputTokens = 2048,
                        stopSequences = new string[] { }
                    },
                    safetySettings = new[]
                    {
                        new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                        new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                        new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                        new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_MEDIUM_AND_ABOVE" }
                    }
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var endpoint = $"{_model}:generateContent?key={_apiKey}";
                var response = await _httpClient.PostAsync(endpoint, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Learn LM API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                    throw new AIServiceException($"Learn LM API error: {response.StatusCode}", "SendPrompt", true);
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseObj = JsonConvert.DeserializeObject<dynamic>(responseContent);

                if (responseObj?.candidates?[0]?.content?.parts?[0]?.text != null)
                {
                    return responseObj.candidates[0].content.parts[0].text.ToString();
                }

                throw new AIServiceException("Invalid response format from Learn LM", "SendPrompt", false);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error calling Learn LM API");
                throw new AIServiceException("Network error calling Learn LM API", ex, "SendPrompt", true);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout calling Learn LM API");
                throw new AIServiceException("Timeout calling Learn LM API", ex, "SendPrompt", true);
            }
            catch (Exception ex) when (!(ex is AIServiceException))
            {
                _logger.LogError(ex, "Unexpected error calling Learn LM API");
                throw new AIServiceException("Unexpected error calling Learn LM API", ex, "SendPrompt", false);
            }
        }

        public async Task<string> GenerateContentAsync(string prompt, ContentType contentType)
        {
            var contextualPrompt = contentType switch
            {
                ContentType.StudyExplanation => $"As a NASM-certified personal trainer educator, provide a clear, accurate explanation for: {prompt}. Focus on NASM-specific terminology and concepts. Include practical applications where relevant.",
                ContentType.QuizQuestion => $"Generate a NASM CPT exam-style multiple choice question about: {prompt}. Include 4 options (A, B, C, D) with one correct answer. Provide a detailed explanation for the correct answer. Format as JSON with question, options, correctAnswer, and explanation fields.",
                ContentType.Flashcard => $"Create a flashcard for NASM CPT study about: {prompt}. Provide a concise front (question/term) and comprehensive back (answer/definition). Format as JSON with front and back fields.",
                ContentType.Recommendation => $"As a NASM study advisor, provide personalized study recommendations based on: {prompt}. Focus on specific NASM content areas and effective study strategies.",
                ContentType.ExamPrediction => $"Analyze the following study performance data and predict NASM CPT exam readiness: {prompt}. Provide a percentage score and specific recommendations for improvement.",
                _ => prompt
            };

            return await SendPromptAsync(contextualPrompt);
        }

        public async Task<bool> ValidateApiKeyAsync()
        {
            try
            {
                var testPrompt = "Test connection";
                await SendPromptAsync(testPrompt);
                return true;
            }
            catch (AIServiceException ex) when (ex.OperationType == "SendPrompt")
            {
                _logger.LogWarning("API key validation failed: {Message}", ex.Message);
                return false;
            }
        }

        public async Task<bool> CheckServiceAvailabilityAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_model}?key={_apiKey}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Service availability check failed");
                return false;
            }
        }

        private string BuildNASMContextualPrompt(string prompt, string context)
        {
            var systemContext = @"You are an expert NASM (National Academy of Sports Medicine) Certified Personal Trainer educator. 
Your responses must be:
1. Accurate according to NASM CPT textbook and current standards
2. Use proper NASM terminology (e.g., OPT Model, kinetic chain, etc.)
3. Evidence-based and cite NASM principles when relevant
4. Appropriate for CPT exam preparation
5. Clear and educational

NASM Key Concepts to Remember:
- OPT Model: Stabilization, Strength, Power phases
- Kinetic Chain: Integrated movement system
- Assessment protocols: Overhead squat, single-leg squat, etc.
- Training variables: Frequency, Intensity, Time, Type (FITT)
- Special populations considerations
- Professional scope of practice

";

            if (!string.IsNullOrEmpty(context))
            {
                systemContext += $"\nCurrent Context: {context}\n";
            }

            return $"{systemContext}\nUser Question: {prompt}";
        }
    }
}
