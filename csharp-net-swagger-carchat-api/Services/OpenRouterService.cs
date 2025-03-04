using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using csharp_net_swagger_carchat_api.Models;
using System.Net.Http.Headers;

namespace csharp_net_swagger_carchat_api.Services
{
    public interface IOpenRouterService
    {
        Task<string> GetChatResponseAsync(string prompt);
        Task<SearchParameters> AnalyzePromptForSearch(string prompt);
        Task<string> AnalyzeCarComparison(List<LeboncoinArticle> cars, string prompt);
        Task<FilterResponse> FilterCars(List<LeboncoinArticle> cars, string filterQuery);
    }

    public class OpenRouterService : IOpenRouterService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<OpenRouterService> _logger;
        private const string MODEL = "google/gemini-2.0-flash-lite-preview-02-05:free";

        public OpenRouterService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenRouterService> logger)
        {
            _httpClient = httpClient;
            _apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ??
                throw new ArgumentNullException("OPENROUTER_API_KEY not found in environment variables");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _logger = logger;
        }

        private async Task<string> SendRequest(object messages)
        {
            try
            {
                var requestBody = new
                {
                    model = MODEL,
                    messages = messages
                };

                var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });
                
                _logger.LogDebug("Request body: {Body}", jsonContent);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync("https://openrouter.ai/api/v1/chat/completions", content);

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Response status: {Status}", response.StatusCode);
                _logger.LogDebug("Response content: {Content}", responseContent);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"OpenRouter API returned {response.StatusCode}: {responseContent}");
                }

                // Check if the response is HTML (often starting with '<') instead of JSON.
                if (string.IsNullOrWhiteSpace(responseContent))
                {
                    throw new HttpRequestException("Empty response from OpenRouter API.");
                }

                var trimmedContent = responseContent.TrimStart();
                if (trimmedContent.StartsWith("<"))
                {
                    // Likely received an HTML error page instead of JSON.
                    throw new HttpRequestException($"Received HTML response instead of JSON: {responseContent}");
                }

                var responseObject = JsonSerializer.Deserialize<JsonElement>(responseContent);
                return responseObject
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending request to OpenRouter");
                throw;
            }
        }

        public async Task<string> GetChatResponseAsync(string prompt)
        {
            var messages = new[]
            {
                new { role = "user", content = prompt }
            };

            return await SendRequest(messages);
        }

        public async Task<SearchParameters> AnalyzePromptForSearch(string prompt)
        {
            try
            {
                _logger.LogInformation("Début de l'analyse du prompt: {Prompt}", prompt);

                var messages = new[]
                {
                    new { role = "user", content = $@"Voici la requête à analyser: {prompt}

IMPORTANT: Extrais les paramètres de recherche suivants et retourne un objet JSON valide:

1. Marque (brand) - UNIQUEMENT parmi cette liste (en MAJUSCULES):
   AUDI, OPEL, BMW, CITROEN, FIAT, FORD, MERCEDES-BENZ, PEUGEOT, RENAULT, VOLKSWAGEN, 
   ALFA ROMEO, AIXAM, BELLIER, CHEVROLET, CHRYSLER, DACIA, DS, HONDA, HYUNDAI, INFINITI, 
   JAGUAR, JEEP, KIA, LANCIA, LAND-ROVER, LEXUS, LIGIER, MASERATI, MAZDA, MICROCAR, 
   MINI, MITSUBISHI, NISSAN, PORSCHE, SAAB, SEAT, SKODA, SMART, SSANGYONG, SUZUKI, 
   TOYOTA, VOLVO

2. Types de véhicules (vehicleTypes) - Liste de types parmi:
   berline, 4x4, SUV, break, cabriolet, citadine, monospace, coupe, voituresociete
   
   IMPORTANT: Si l'utilisateur mentionne un type de véhicule comme 'berline', 'SUV', '4x4', etc.,
   ajoute-le à la liste vehicleTypes. Cherche des synonymes comme 'sedan' pour 'berline',
   'minivan' pour 'monospace', etc. Si aucun type n'est mentionné, laisse la liste vide.

3. Nombre de portes (doors) - Liste de nombres parmi: 2, 3, 4, 5
   
   IMPORTANT: Si l'utilisateur mentionne un nombre de portes (par exemple '3 portes', '5 portes', etc.),
   ajoute ce nombre à la liste doors. Si aucun nombre de portes n'est mentionné, laisse la liste vide.

4. Nombre de places (seats) - Liste de nombres parmi: 1, 2, 3, 4, 5, 6, 999999
   
   IMPORTANT: Si l'utilisateur mentionne un nombre de places (par exemple '5 places', '7 places', etc.),
   ajoute ce nombre à la liste seats. Pour 7 places ou plus, utilise la valeur 999999.
   Si aucun nombre de places n'est mentionné, laisse la liste vide.

5. Autres paramètres à extraire:
   - model: modèle de la voiture
   - minPrice/maxPrice: fourchette de prix
   - location: lieu
   - fuelType: '1' pour Essence, '2' pour Diesel, '3' pour Électrique
   - regDateMin/regDateMax: années d'immatriculation
   - keywords: mots-clés additionnels

Format de réponse attendu (JSON uniquement):
{{
    ""brand"": ""MARQUE"",
    ""model"": ""modèle"",
    ""minPrice"": ""prix_min"",
    ""maxPrice"": ""prix_max"",
    ""location"": ""lieu"",
    ""fuelType"": ""type_carburant"",
    ""regDateMin"": ""année_min"",
    ""regDateMax"": ""année_max"",
    ""keywords"": ""mots_clés"",
    ""vehicleTypes"": [""type1"", ""type2""],
    ""doors"": [""2"", ""4""],
    ""seats"": [""5"", ""999999""]
}}" }
                };

                var response = await SendRequest(messages);
                _logger.LogDebug("Assistant response: {Response}", response);

                try
                {
                    // Amélioration du nettoyage de la réponse
                    response = response.Trim();
                    
                    // Trouver le début du JSON
                    var jsonStart = response.IndexOf('{');
                    var jsonEnd = response.LastIndexOf('}');
                    
                    if (jsonStart >= 0 && jsonEnd >= 0 && jsonEnd > jsonStart)
                    {
                        response = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    }
                    
                    _logger.LogDebug("Cleaned response for parsing: {Response}", response);

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    var searchParams = JsonSerializer.Deserialize<SearchParameters>(response, options)
                        ?? new SearchParameters();

                    _logger.LogInformation("Paramètres de recherche extraits: {SearchParams}",
                        JsonSerializer.Serialize(searchParams));

                    return searchParams;
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Erreur lors de la désérialisation de la réponse. Réponse brute: {RawResponse}", response);
                    return new SearchParameters();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing prompt");
                return new SearchParameters();
            }
        }

        public async Task<string> AnalyzeCarComparison(List<LeboncoinArticle> cars, string prompt)
        {
            try
            {
                var carsJson = JsonSerializer.Serialize(cars, new JsonSerializerOptions { WriteIndented = true });
                var messages = new[]
                {
                    new { role = "user", content = $@"Voici les voitures à analyser:
                {carsJson}

                Question: {prompt}

                IMPORTANT: Analyse ces voitures et réponds de manière structurée en considérant:
                - L'âge des véhicules
                - Le kilométrage
                - Le prix
                - Le type de carburant
                - Les équipements si mentionnés
                - Le rapport qualité/prix global

                Base ta réponse uniquement sur les données fournies et sois précis dans tes recommandations." }
                };

                var response = await SendRequest(messages);
                _logger.LogDebug("Assistant response: {Response}", response);

                // Nettoyer la réponse des éventuels caractères de formatage
                response = response.Trim();
                if (response.StartsWith("```") && response.EndsWith("```"))
                {
                    response = response.Substring(3, response.Length - 6).Trim();
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'analyse comparative");
                throw;
            }
        }

        public async Task<FilterResponse> FilterCars(List<LeboncoinArticle> cars, string filterQuery)
        {
            try
            {
                var carsJson = JsonSerializer.Serialize(cars, new JsonSerializerOptions { WriteIndented = true });
                var messages = new[]
                {
                    new { role = "user", content = $@"Voici les voitures à filtrer:
                    {carsJson}

                    Critère de filtrage: {filterQuery}

                    IMPORTANT: Réponds UNIQUEMENT avec un objet JSON valide au format suivant, sans texte avant ou après:
                    {{
                        ""filtered_car_ids"": [1, 2, 3],
                        ""explanation"": ""Explication du filtrage""
                    }}" }
                };

                var response = await SendRequest(messages);
                _logger.LogDebug("Assistant response: {Response}", response);

                try
                {
                    // Amélioration du nettoyage de la réponse
                    response = response.Trim();
                    
                    // Trouver le début du JSON
                    var jsonStart = response.IndexOf('{');
                    var jsonEnd = response.LastIndexOf('}');
                    
                    if (jsonStart >= 0 && jsonEnd >= 0 && jsonEnd > jsonStart)
                    {
                        response = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    }
                    
                    _logger.LogDebug("Cleaned response for parsing: {Response}", response);

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var filterResult = JsonSerializer.Deserialize<FilterResponse>(response, options);

                    if (filterResult == null || !filterResult.FilteredCarIds.Any())
                    {
                        return new FilterResponse
                        {
                            FilteredCarIds = cars.Select(c => c.Id).ToList(),
                            Explanation = "Aucun filtre spécifique n'a pu être appliqué. Toutes les voitures sont retournées."
                        };
                    }

                    return filterResult;
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Erreur lors de la désérialisation de la réponse. Réponse brute: {RawResponse}", response);
                    return new FilterResponse
                    {
                        FilteredCarIds = cars.Select(c => c.Id).ToList(),
                        Explanation = "Une erreur est survenue lors du filtrage. Toutes les voitures sont retournées."
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du filtrage des voitures");
                throw;
            }
        }
    }
}
