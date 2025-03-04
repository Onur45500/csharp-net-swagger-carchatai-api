using Microsoft.AspNetCore.Mvc;
using csharp_net_swagger_carchat_api.Models;
using csharp_net_swagger_carchat_api.Services;
using System.Text.Json;
using System.Linq;

namespace csharp_net_swagger_carchat_api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IOpenRouterService _openRouterService;
        private readonly ILeboncoinService _leboncoinService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(
            IOpenRouterService openRouterService, 
            ILeboncoinService leboncoinService,
            ILogger<ChatController> logger)
        {
            _openRouterService = openRouterService;
            _leboncoinService = leboncoinService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Prompt))
                {
                    return BadRequest(new ChatResponse 
                    { 
                        Success = false, 
                        Error = "Prompt cannot be empty" 
                    });
                }

                // Analyser le prompt pour obtenir les paramètres de recherche
                var searchParams = await _openRouterService.AnalyzePromptForSearch(request.Prompt);
                
                // If the search parameters are empty, return an empty result
                if (IsEmptySearch(searchParams))
                {
                    return Ok(new ChatResponse 
                    { 
                        Success = true,
                        Response = JsonSerializer.Serialize(new { 
                            message = "Désolé, je n'ai pas compris votre recherche de voiture. Pourriez-vous être plus précis ?",
                            articles = new List<LeboncoinArticle>() 
                        })
                    });
                }
                
                // Construire l'URL de recherche
                var searchUrl = _leboncoinService.BuildSearchUrl(searchParams);
                
                // Scraper les résultats
                var articles = await _leboncoinService.ScrapeSearchResults(searchUrl);
                
                _logger.LogInformation("Nombre total d'articles trouvés: {Count}", articles.Count);
                
                // Ajouter les IDs incrémentaux
                for (int i = 0; i < articles.Count; i++)
                {
                    articles[i].Id = i + 1;
                }
                
                // Retourner tous les articles sans limitation
                return Ok(new ChatResponse 
                { 
                    Success = true, 
                    Response = JsonSerializer.Serialize(new { 
                        searchUrl, 
                        totalResults = articles.Count,
                        articles = articles
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat request");
                return StatusCode(500, new ChatResponse 
                { 
                    Success = false, 
                    Error = "An error occurred while processing your request" 
                });
            }
        }

        [HttpPost("compare")]
        public async Task<IActionResult> Compare([FromBody] ComparisonRequest request)
        {
            try
            {
                if (!request.Cars.Any())
                {
                    return BadRequest(new ChatResponse 
                    { 
                        Success = false, 
                        Error = "No cars provided for comparison" 
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Question))
                {
                    return BadRequest(new ChatResponse 
                    { 
                        Success = false, 
                        Error = "Question cannot be empty" 
                    });
                }

                // S'assurer que les IDs sont corrects
                for (int i = 0; i < request.Cars.Count; i++)
                {
                    request.Cars[i].Id = i + 1;
                }

                var analysis = await _openRouterService.AnalyzeCarComparison(request.Cars, request.Question);
                
                return Ok(new ChatResponse 
                { 
                    Success = true, 
                    Response = analysis  // La réponse est déjà nettoyée dans le service
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing comparison request");
                return StatusCode(500, new ChatResponse 
                { 
                    Success = false, 
                    Error = "An error occurred while processing your request" 
                });
            }
        }

        [HttpPost("filter")]
        public async Task<IActionResult> Filter([FromBody] FilterRequest request)
        {
            try
            {
                // Log the complete request body
                _logger.LogInformation("Received filter request: {Request}", 
                    JsonSerializer.Serialize(request, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    }));

                if (!request.Cars.Any())
                {
                    return BadRequest(new ChatResponse 
                    { 
                        Success = false, 
                        Error = "No cars provided for filtering" 
                    });
                }

                if (string.IsNullOrWhiteSpace(request.FilterQuery))
                {
                    return BadRequest(new ChatResponse 
                    { 
                        Success = false, 
                        Error = "Filter query cannot be empty" 
                    });
                }

                // S'assurer que les IDs sont corrects
                for (int i = 0; i < request.Cars.Count; i++)
                {
                    request.Cars[i].Id = i + 1;
                }

                _logger.LogInformation("Processing filter query: {Query} for {Count} cars", 
                    request.FilterQuery, 
                    request.Cars.Count);

                var filterResult = await _openRouterService.FilterCars(request.Cars, request.FilterQuery);
                
                _logger.LogInformation("Filter result: {Result}", 
                    JsonSerializer.Serialize(filterResult, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    }));

                return Ok(new ChatResponse 
                { 
                    Success = true, 
                    Response = JsonSerializer.Serialize(filterResult)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing filter request");
                return StatusCode(500, new ChatResponse 
                { 
                    Success = false, 
                    Error = "An error occurred while processing your request" 
                });
            }
        }

        private bool IsEmptySearch(SearchParameters parameters)
        {
            return string.IsNullOrWhiteSpace(parameters.Brand) 
                && string.IsNullOrWhiteSpace(parameters.Model)
                && string.IsNullOrWhiteSpace(parameters.Keywords)
                && string.IsNullOrWhiteSpace(parameters.FuelType)
                && string.IsNullOrWhiteSpace(parameters.RegDateMin)
                && string.IsNullOrWhiteSpace(parameters.RegDateMax)
                && (parameters.VehicleTypes == null || !parameters.VehicleTypes.Any())
                && (parameters.Doors == null || !parameters.Doors.Any())
                && (parameters.Seats == null || !parameters.Seats.Any());
        }
    }
} 