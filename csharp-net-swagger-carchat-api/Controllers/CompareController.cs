using Microsoft.AspNetCore.Mvc;
using csharp_net_swagger_carchat_api.Models;
using csharp_net_swagger_carchat_api.Services;
using System.Text.Json;

namespace csharp_net_swagger_carchat_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CompareController : ControllerBase
    {
        private readonly IOpenRouterService _openRouterService;
        private readonly ILogger<CompareController> _logger;

        public CompareController(
            IOpenRouterService openRouterService,
            ILogger<CompareController> logger)
        {
            _openRouterService = openRouterService;
            _logger = logger;
        }

        [HttpPost("analyze")]
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
                    Response = JsonSerializer.Serialize(new { 
                        analysis = analysis,
                        cars = request.Cars
                    })
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

        [HttpPost("suggest")]
        public async Task<IActionResult> SuggestBestCar([FromBody] ComparisonRequest request)
        {
            try
            {
                if (!request.Cars.Any())
                {
                    return BadRequest(new ChatResponse 
                    { 
                        Success = false, 
                        Error = "Aucune voiture fournie pour la suggestion" 
                    });
                }

                // Add specific criteria to the question if not provided
                var question = string.IsNullOrWhiteSpace(request.Question)
                    ? "Parmi ces voitures, quelle est la meilleure en termes de rapport qualité/prix ? " +
                      "Prends en compte l'âge, le kilométrage, le prix et donne une justification détaillée."
                    : request.Question;

                _logger.LogInformation("Début de l'analyse pour suggestion de la meilleure voiture parmi {Count} options", 
                    request.Cars.Count);
                
                var analysis = await _openRouterService.AnalyzeCarComparison(request.Cars, question);
                
                return Ok(new ChatResponse 
                { 
                    Success = true, 
                    Response = JsonSerializer.Serialize(new { 
                        suggestion = analysis,
                        cars = request.Cars,
                        criteria = question
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la suggestion de la meilleure voiture");
                return StatusCode(500, new ChatResponse 
                { 
                    Success = false, 
                    Error = "Une erreur s'est produite lors de l'analyse des voitures" 
                });
            }
        }
    }
} 