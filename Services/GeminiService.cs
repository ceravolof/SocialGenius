using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SocialGenius.Services
{
    public class GeminiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<GeminiService> _logger;
        private readonly string _baseUrl;

        public GeminiService(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory = null,
            ILogger<GeminiService> logger = null)
        {
            _apiKey = configuration["GeminiApi:ApiKey"];
            _logger = logger;
            _httpClientFactory = httpClientFactory;

            // Utilizza il modello che funziona con il piano free
            _baseUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";

            // Usa HttpClientFactory se disponibile, altrimenti crea un client standard
            if (_httpClientFactory != null)
            {
                _httpClient = _httpClientFactory.CreateClient("GeminiAPI");
            }
            else
            {
                _httpClient = new HttpClient();
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                LogError("Chiave API Gemini non configurata");
            }
        }

        public async Task<string> GenerateContentAsync(string platform, List<string> interests, string imageContext = null)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    LogError("API Key non configurata");
                    return "API Key di Gemini non configurata. Controlla il file appsettings.json.";
                }

                LogInfo($"Generazione contenuto per: {platform} - Temi: {string.Join(", ", interests)}");

                // Prompt migliorato con istruzioni più precise per ottenere solo la descrizione con hashtag
                var prompt = $"Crea un post per {platform} sui temi: {string.Join(", ", interests)}. " +
                    $"IMPORTANTE: Rispondi SOLO con una breve descrizione coinvolgente (max 2-3 frasi) " +
                    $"seguita da 3-5 hashtag. NON includere introduzioni, domande o testo aggiuntivo. " +
                    $"Scrivi direttamente il post come se fossi l'utente. " +
                    $"Esempio di formato: 'Questa è la descrizione del post. Ecco un'altra frase. #hashtag1 #hashtag2 #hashtag3'";

                // Costruisco il messaggio nel formato richiesto dall'API di Gemini
                var requestData = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        maxOutputTokens = 800,
                        topK = 40,
                        topP = 0.95
                    }
                };

                // Opzioni di serializzazione JSON
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                // Genera il contenuto JSON della richiesta
                var jsonContent = JsonContent.Create(requestData, options: jsonOptions);

                // URL completo con la chiave API
                var requestUrl = $"{_baseUrl}?key={_apiKey}";

                LogInfo($"Chiamata API a: {_baseUrl}");

                // Invia la richiesta HTTP
                var response = await _httpClient.PostAsync(requestUrl, jsonContent);

                // Leggi la risposta come stringa
                var responseContent = await response.Content.ReadAsStringAsync();
                LogInfo($"Risposta API status code: {(int)response.StatusCode} {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    LogError($"Errore API: {response.StatusCode} - {responseContent}");
                    return GenerateFallbackContent(platform, interests);
                }

                // Deserializza la risposta JSON
                var jsonResponse = JsonSerializer.Deserialize<GeminiResponse>(responseContent, jsonOptions);

                // Estrai il testo generato
                string generatedText = jsonResponse?.Candidates?
                    .FirstOrDefault()?.Content?.Parts?
                    .FirstOrDefault()?.Text ?? string.Empty;

                if (string.IsNullOrEmpty(generatedText))
                {
                    LogWarning("Risposta vuota dall'API");
                    return GenerateFallbackContent(platform, interests);
                }

                // Pulizia e post-processing del testo generato
                string cleanedText = CleanGeneratedText(generatedText);

                LogInfo($"Contenuto generato e pulito con successo! Lunghezza: {cleanedText.Length}");
                return cleanedText;
            }
            catch (Exception ex)
            {
                LogError($"Eccezione durante la chiamata API: {ex.Message}");
                return GenerateFallbackContent(platform, interests);
            }
        }

        /// <summary>
        /// Pulisce il testo generato per mostrare solo la descrizione e gli hashtag
        /// </summary>
        private string CleanGeneratedText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Rimuove eventuali domande sul contesto dell'immagine
            if (text.Contains("immagine") && (text.Contains("?") || text.Contains("Prima di")))
            {
                return GenerateSimpleResponse();
            }

            // Rimuove tutte le parti tra asterischi (spesso usate per note o istruzioni)
            text = Regex.Replace(text, @"\*\*?(.*?)\*\*?", "");

            // Rimuove parti che iniziano con frasi come "Ecco un post..." o "Ecco una descrizione..."
            text = Regex.Replace(text, @"^(Ecco|Ecco un|Ecco una|Di seguito)[^:]*:?\s*", "", RegexOptions.IgnoreCase);

            // Rimuove eventuali virgolette
            text = text.Replace("\"", "").Replace(""", "").Replace(""", "");

            // Rimuove righe vuote multiple
            text = Regex.Replace(text, @"(\r\n|\n|\r){2,}", "\n");

            // Rimuove righe che contengono solo una lettera/numero seguito da punto/parentesi (punti elenco)
            text = Regex.Replace(text, @"^[0-9A-Za-z][.)\]][\s]*.*$", "", RegexOptions.Multiline);

            // Se il testo inizia con trattini, li rimuove
            text = Regex.Replace(text, @"^[-–—]\s*", "");

            return text.Trim();
        }

        private string GenerateSimpleResponse()
        {
            // Risposta generica di esempio in caso di richiesta di dettagli sull'immagine
            var responses = new List<string>
            {
                "L'energia che ci mettiamo in ciò che amiamo ci trasforma ogni giorno. Il percorso non è mai semplice, ma la passione rende tutto possibile. #motivazione #crescitapersonale #obiettivi #determinazione",

                "I momenti più belli sono quelli che condividiamo con chi ci comprende davvero. La connessione autentica è il vero tesoro della vita. #connessione #autenticità #relazioni #condivisione",

                "Ogni nuova sfida è un'opportunità per scoprire quanto possiamo spingerci oltre. I limiti esistono solo nella nostra mente. #sfide #superarelimiti #crescita #opportunità",

                "La bellezza sta nei dettagli che spesso trascuriamo. Prendiamoci il tempo per osservare ciò che ci circonda con occhi nuovi. #consapevolezza #presente #dettagli #mindfulness"
            };

            return responses[new Random().Next(responses.Count)];
        }

        private string GenerateFallbackContent(string platform, List<string> interests)
        {
            LogWarning("Utilizzo generazione fallback offline");

            var random = new Random();

            var templates = new List<string>
            {
                "Oggi condivido con voi alcune riflessioni su {0}. È incredibile quanto possa arricchire la nostra vita! {1}",
                "Ho scoperto qualcosa di straordinario riguardo a {0} che volevo condividere con tutti voi. {1}",
                "Esplorando il mondo di {0} ho trovato ispirazione che volevo condividere. {1}",
                "Pensieri del giorno su {0}: una passione che continua a sorprendermi. {1}",
                "Momenti speciali dedicati a {0}. Cosa ne pensate? {1}"
            };

            var hashtagsByInterest = new Dictionary<string, List<string>>
            {
                { "Viaggi", new List<string>{ "#viaggio", "#travel", "#wanderlust", "#adventure", "#explore" } },
                { "Cibo", new List<string>{ "#food", "#foodie", "#gourmet", "#cooking", "#yummy" } },
                { "Moda", new List<string>{ "#fashion", "#style", "#outfit", "#trendy", "#fashionista" } },
                { "Fitness", new List<string>{ "#fitness", "#workout", "#gym", "#healthy", "#training" } },
                { "Tecnologia", new List<string>{ "#tech", "#technology", "#innovation", "#digital", "#future" } },
                { "Bellezza", new List<string>{ "#beauty", "#skincare", "#makeup", "#cosmetics", "#glow" } },
                { "Arte", new List<string>{ "#arte", "#art", "#artist", "#creative", "#design" } },
                { "Business", new List<string>{ "#business", "#entrepreneur", "#success", "#motivation", "#goals" } },
                { "Natura", new List<string>{ "#natura", "#nature", "#environment", "#green", "#outdoors" } },
                { "Musica", new List<string>{ "#musica", "#music", "#song", "#musician", "#melody" } }
            };

            var mainInterest = interests.FirstOrDefault() ?? "Social Media";
            var template = templates[random.Next(templates.Count)];

            var hashtags = new List<string>();
            foreach (var interest in interests)
            {
                if (hashtagsByInterest.ContainsKey(interest))
                {
                    hashtags.AddRange(hashtagsByInterest[interest].OrderBy(x => random.Next()).Take(random.Next(1, 3)));
                }
            }

            if (!hashtags.Any())
            {
                hashtags.AddRange(new List<string> { "#socialpost", "#condivisione", "#community", "#trend", "#social" });
            }

            var selectedHashtags = string.Join(" ", hashtags.OrderBy(x => random.Next()).Take(4));
            return string.Format(template, mainInterest, selectedHashtags);
        }

        // Metodi di logging semplificati
        private void LogInfo(string message)
        {
            if (_logger != null)
                _logger.LogInformation(message);
            else
                Console.WriteLine($"INFO: {message}");
        }

        private void LogError(string message)
        {
            if (_logger != null)
                _logger.LogError(message);
            else
                Console.WriteLine($"ERROR: {message}");
        }

        private void LogWarning(string message)
        {
            if (_logger != null)
                _logger.LogWarning(message);
            else
                Console.WriteLine($"WARNING: {message}");
        }
    }

    // Classi per deserializzare la risposta di Gemini
    public class GeminiResponse
    {
        public List<Candidate> Candidates { get; set; }
        public FeedbackData PromptFeedback { get; set; }
    }

    // Classi di supporto per la deserializzazione
    public class Candidate
    {
        public Content Content { get; set; }
        public string FinishReason { get; set; }
        public int Index { get; set; }
        public List<SafetyRating> SafetyRatings { get; set; }
    }

    public class Content
    {
        public List<Part> Parts { get; set; }
        public string Role { get; set; }
    }

    public class Part
    {
        public string Text { get; set; }
    }

    public class SafetyRating
    {
        public string Category { get; set; }
        public string Probability { get; set; }
    }

    public class FeedbackData
    {
        public List<SafetyRating> SafetyRatings { get; set; }
    }
}