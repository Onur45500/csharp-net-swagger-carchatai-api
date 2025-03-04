using System.Web;
using csharp_net_swagger_carchat_api.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Net;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace csharp_net_swagger_carchat_api.Services
{
    public interface ILeboncoinService
    {
        string BuildSearchUrl(SearchParameters parameters);
        Task<List<LeboncoinArticle>> ScrapeSearchResults(string url);
    }

    public class LeboncoinService : ILeboncoinService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<LeboncoinService> _logger;

        private static readonly HashSet<string> ValidBrands = new(StringComparer.OrdinalIgnoreCase)
        {
            "AUDI", "OPEL", "BMW", "CITROEN", "FIAT", "FORD", "MERCEDES-BENZ", "PEUGEOT", "RENAULT", 
            "VOLKSWAGEN", "ALFA ROMEO", "AIXAM", "BELLIER", "CHEVROLET", "CHRYSLER", "DACIA", "DS", 
            "HONDA", "HYUNDAI", "INFINITI", "JAGUAR", "JEEP", "KIA", "LANCIA", "LAND-ROVER", "LEXUS", 
            "LIGIER", "MASERATI", "MAZDA", "MICROCAR", "MINI", "MITSUBISHI", "NISSAN", "PORSCHE", 
            "SAAB", "SEAT", "SKODA", "SMART", "SSANGYONG", "SUZUKI", "TOYOTA", "VOLVO"
        };

        private static readonly Dictionary<string, string> VehicleTypeMapping = new(StringComparer.OrdinalIgnoreCase)
        {
            { "berline", "berline" },
            { "sedan", "berline" },
            { "4x4", "4x4" },
            { "suv", "4x4" },
            { "break", "break" },
            { "station wagon", "break" },
            { "cabriolet", "cabriolet" },
            { "convertible", "cabriolet" },
            { "citadine", "citadine" },
            { "city car", "citadine" },
            { "monospace", "monospace" },
            { "minivan", "monospace" },
            { "coupe", "coupe" },
            { "voiture societe", "voituresociete" },
            { "voiture de societe", "voituresociete" },
            { "utilitaire", "voituresociete" }
        };

        public LeboncoinService(HttpClient httpClient, ILogger<LeboncoinService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public string BuildSearchUrl(SearchParameters parameters)
        {
            // Base URL construction - changed to use /recherche endpoint
            var baseUrl = "https://www.leboncoin.fr/recherche";
            
            // Utiliser StringBuilder pour une meilleure performance avec de nombreuses concaténations
            var queryBuilder = new StringBuilder();
            
            // Always add category for cars
            queryBuilder.Append("category=2");
            
            if (!string.IsNullOrEmpty(parameters.Brand))
            {
                var normalizedBrand = parameters.Brand.ToUpperInvariant();
                if (ValidBrands.Contains(normalizedBrand))
                {
                    queryBuilder.Append($"&u_car_brand={Uri.EscapeDataString(normalizedBrand)}");
                }
            }
            
            if (!string.IsNullOrEmpty(parameters.Model))
                queryBuilder.Append($"&u_car_model={Uri.EscapeDataString(parameters.Model.ToUpper())}");
            
            if (!string.IsNullOrEmpty(parameters.MinPrice))
                queryBuilder.Append($"&price_min={parameters.MinPrice}");
            
            if (!string.IsNullOrEmpty(parameters.MaxPrice))
                queryBuilder.Append($"&price_max={parameters.MaxPrice}");
            
            // Handle location with default radius if coordinates not provided
            if (!string.IsNullOrEmpty(parameters.Location))
            {
                // If location doesn't include coordinates, use simple format
                if (!parameters.Location.Contains("__"))
                    queryBuilder.Append($"&locations={Uri.EscapeDataString(parameters.Location)}");
                else
                    queryBuilder.Append($"&locations={parameters.Location}"); // Already formatted with coordinates
            }
            
            // Add fuel type if specified
            if (!string.IsNullOrEmpty(parameters.FuelType))
                queryBuilder.Append($"&fuel={parameters.FuelType}");
            
            // Add registration date range if specified
            if (!string.IsNullOrEmpty(parameters.RegDateMin) || !string.IsNullOrEmpty(parameters.RegDateMax))
            {
                var regdate = $"{parameters.RegDateMin ?? ""}-{parameters.RegDateMax ?? ""}";
                queryBuilder.Append($"&regdate={regdate}");
            }
            
            // Add vehicle types if specified
            if (parameters.VehicleTypes != null && parameters.VehicleTypes.Any())
            {
                var mappedVehicleTypes = new HashSet<string>();
                
                foreach (var vehicleType in parameters.VehicleTypes)
                {
                    if (VehicleTypeMapping.TryGetValue(vehicleType, out var mappedType))
                    {
                        mappedVehicleTypes.Add(mappedType);
                    }
                    else
                    {
                        // If no mapping found, use the original value (lowercase)
                        mappedVehicleTypes.Add(vehicleType.ToLower());
                    }
                }
                
                if (mappedVehicleTypes.Any())
                {
                    var vehicleTypeParam = string.Join(",", mappedVehicleTypes);
                    queryBuilder.Append($"&vehicle_type={vehicleTypeParam}");
                }
            }
            
            // Add doors if specified
            if (parameters.Doors != null && parameters.Doors.Any())
            {
                var validDoors = parameters.Doors
                    .Where(d => int.TryParse(d, out int doorCount) && doorCount >= 2 && doorCount <= 5)
                    .ToList();
                
                if (validDoors.Any())
                {
                    var doorsParam = string.Join(",", validDoors);
                    queryBuilder.Append($"&doors={doorsParam}");
                }
            }
            
            // Add seats if specified
            if (parameters.Seats != null && parameters.Seats.Any())
            {
                var validSeats = new HashSet<string>();
                
                foreach (var seat in parameters.Seats)
                {
                    var mappedSeat = MapSeatCount(seat);
                    if (!string.IsNullOrEmpty(mappedSeat))
                    {
                        validSeats.Add(mappedSeat);
                    }
                }
                
                if (validSeats.Any())
                {
                    var seatsParam = string.Join(",", validSeats);
                    queryBuilder.Append($"&seats={seatsParam}");
                }
            }
            
            // Add keywords as text search
            if (!string.IsNullOrEmpty(parameters.Keywords))
                queryBuilder.Append($"&text={Uri.EscapeDataString(parameters.Keywords)}");
            
            // Always add sort parameter for consistent results across pages
            queryBuilder.Append("&sort=time");
            
            // Combine URL parts
            return $"{baseUrl}?{queryBuilder}";
        }

        public async Task<List<LeboncoinArticle>> ScrapeSearchResults(string searchUrl)
        {
            try
            {
                _logger.LogInformation("Début du scraping de l'URL: {Url}", searchUrl);
                
                var allArticles = new List<LeboncoinArticle>();
                var maxPages = 10; // Augmenté de 5 à 10 pages pour récupérer plus d'articles
                
                // Préparer les URLs des pages à scraper
                var pageUrls = new List<string>();
                for (int currentPage = 1; currentPage <= maxPages; currentPage++)
                {
                    // Construct the URL for the current page
                    var pageUrl = searchUrl;
                    if (currentPage > 1)
                    {
                        // Add or update the page parameter
                        if (pageUrl.Contains("?"))
                        {
                            if (pageUrl.Contains("page="))
                            {
                                // Replace existing page parameter
                                pageUrl = System.Text.RegularExpressions.Regex.Replace(pageUrl, @"page=\d+", $"page={currentPage}");
                            }
                            else
                            {
                                // Add page parameter
                                pageUrl += $"&page={currentPage}";
                            }
                        }
                        else
                        {
                            // No query parameters yet, add page as the first one
                            pageUrl += $"?page={currentPage}";
                        }
                    }
                    
                    pageUrls.Add(pageUrl);
                }
                
                // Scraper les pages en parallèle avec un maximum de 3 pages simultanées
                var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 3 };
                var articlesByPage = new ConcurrentDictionary<int, List<LeboncoinArticle>>();
                
                await Parallel.ForEachAsync(pageUrls.Select((url, index) => (url, index)), parallelOptions, async (item, token) =>
                {
                    var (pageUrl, pageIndex) = item;
                    var pageNumber = pageIndex + 1;
                    
                    try
                    {
                        _logger.LogInformation("Scraping page {CurrentPage}/{MaxPages}: {Url}", pageNumber, maxPages, pageUrl);
                        
                        // Délai réduit entre les requêtes (1-3 secondes au lieu de 2-10)
                        if (pageNumber > 1)
                        {
                            await Task.Delay(Random.Shared.Next(1000, 3000), token);
                        }
                        
                        var articles = await ScrapePageAsync(pageUrl, pageNumber);
                        
                        if (articles.Count > 0)
                        {
                            articlesByPage[pageIndex] = articles;
                            _logger.LogInformation("Page {CurrentPage}: {Count} articles trouvés", pageNumber, articles.Count);
                        }
                        else
                        {
                            _logger.LogWarning("Page {CurrentPage}: Aucun article trouvé", pageNumber);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erreur lors du scraping de la page {PageNumber}: {Url}", pageNumber, pageUrl);
                    }
                });
                
                // Assembler les résultats dans l'ordre des pages
                for (int i = 0; i < pageUrls.Count; i++)
                {
                    if (articlesByPage.TryGetValue(i, out var pageArticles))
                    {
                        allArticles.AddRange(pageArticles);
                    }
                }
                
                _logger.LogInformation("Scraping terminé, {Count} articles trouvés au total", allArticles.Count);
                return allArticles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du scraping des résultats");
                return new List<LeboncoinArticle>();
            }
        }
        
        // Méthode pour scraper une seule page
        private async Task<List<LeboncoinArticle>> ScrapePageAsync(string pageUrl, int pageNumber)
        {
            var articles = new List<LeboncoinArticle>();
            
            try
            {
                var web = new HtmlWeb();
                web.PreRequest = request =>
                {
                    request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    
                    // En-têtes simplifiés pour réduire la taille de la requête
                    request.Headers.Clear();
                    request.Headers.Add("Accept", "text/html,application/xhtml+xml,*/*");
                    request.Headers.Add("Accept-Encoding", "gzip, deflate");
                    request.Headers.Add("Accept-Language", "fr-FR,fr;q=0.9,en;q=0.8");
                    request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    request.Headers.Add("Referer", "https://www.leboncoin.fr/");
                    
                    return true;
                };

                var doc = await web.LoadFromWebAsync(pageUrl);
                
                var articleNodes = doc.DocumentNode.SelectNodes("//article[@data-test-id='ad']");
                
                if (articleNodes == null || !articleNodes.Any())
                {
                    return articles;
                }

                // Traitement optimisé des articles
                foreach (var node in articleNodes)
                {
                    try
                    {
                        // Extractions optimisées
                        var titleNode = node.SelectSingleNode(".//p[@data-test-id='adcard-title']");
                        if (titleNode == null)
                        {
                            titleNode = node.SelectSingleNode(".//a[@title]");
                            if (titleNode == null)
                            {
                                titleNode = node.SelectSingleNode(".//span[@aria-hidden='true']");
                            }
                        }
                        
                        var priceNode = node.SelectSingleNode(".//p[@data-test-id='price']");
                        if (priceNode == null)
                        {
                            priceNode = node.SelectSingleNode(".//*[contains(@class, 'price')]");
                        }
                        
                        var locationNode = node.SelectSingleNode(".//p[contains(@aria-label, 'Située à')]");
                        if (locationNode == null)
                        {
                            locationNode = node.SelectSingleNode(".//p[contains(@class, 'text-caption')]");
                        }
                        
                        // Extraction de l'URL de l'annonce
                        var linkNode = node.SelectSingleNode(".//a[@title]");
                        var articleUrl = linkNode?.GetAttributeValue("href", "");
                        if (string.IsNullOrEmpty(articleUrl))
                        {
                            // Essayer de trouver n'importe quel lien dans l'article
                            var anyLink = node.SelectSingleNode(".//a");
                            if (anyLink != null)
                            {
                                articleUrl = anyLink.GetAttributeValue("href", "");
                            }
                        }
                        
                        // Amélioration de l'extraction des images pour obtenir les meilleures qualités
                        var images = new List<string>();
                        
                        // Rechercher les balises picture qui contiennent les sources d'images
                        var pictureNodes = node.SelectNodes(".//picture");
                        if (pictureNodes != null)
                        {
                            int imageCount = 0;
                            foreach (var pictureNode in pictureNodes)
                            {
                                if (imageCount >= 3) break; // Limiter à 3 images
                                
                                // Chercher d'abord les sources de haute qualité
                                var sourceNodes = pictureNode.SelectNodes(".//source");
                                string bestImageUrl = "";
                                
                                if (sourceNodes != null && sourceNodes.Any())
                                {
                                    // Priorité aux formats modernes et haute résolution
                                    // Chercher d'abord les images desktop (min-width: 768px)
                                    var desktopSources = sourceNodes
                                        .Where(s => s.GetAttributeValue("media", "").Contains("min-width: 768px"))
                                        .ToList();
                                    
                                    if (desktopSources.Any())
                                    {
                                        // Préférer avif, puis webp, puis jpg
                                        var avifSource = desktopSources.FirstOrDefault(s => s.GetAttributeValue("type", "").Contains("avif"));
                                        var webpSource = desktopSources.FirstOrDefault(s => s.GetAttributeValue("type", "").Contains("webp"));
                                        var jpgSource = desktopSources.FirstOrDefault(s => s.GetAttributeValue("type", "").Contains("jpeg"));
                                        
                                        if (avifSource != null)
                                            bestImageUrl = avifSource.GetAttributeValue("srcset", "");
                                        else if (webpSource != null)
                                            bestImageUrl = webpSource.GetAttributeValue("srcset", "");
                                        else if (jpgSource != null)
                                            bestImageUrl = jpgSource.GetAttributeValue("srcset", "");
                                    }
                                    
                                    // Si aucune source desktop n'est trouvée, utiliser les sources mobiles
                                    if (string.IsNullOrEmpty(bestImageUrl))
                                    {
                                        var mobileSources = sourceNodes
                                            .Where(s => !s.GetAttributeValue("media", "").Contains("min-width: 768px"))
                                            .ToList();
                                        
                                        if (mobileSources.Any())
                                        {
                                            var avifSource = mobileSources.FirstOrDefault(s => s.GetAttributeValue("type", "").Contains("avif"));
                                            var webpSource = mobileSources.FirstOrDefault(s => s.GetAttributeValue("type", "").Contains("webp"));
                                            var jpgSource = mobileSources.FirstOrDefault(s => s.GetAttributeValue("type", "").Contains("jpeg"));
                                            
                                            if (avifSource != null)
                                                bestImageUrl = avifSource.GetAttributeValue("srcset", "");
                                            else if (webpSource != null)
                                                bestImageUrl = webpSource.GetAttributeValue("srcset", "");
                                            else if (jpgSource != null)
                                                bestImageUrl = jpgSource.GetAttributeValue("srcset", "");
                                        }
                                    }
                                }
                                
                                // Si aucune source n'est trouvée, chercher l'image directement
                                if (string.IsNullOrEmpty(bestImageUrl))
                                {
                                    var imgNode = pictureNode.SelectSingleNode(".//img");
                                    if (imgNode != null)
                                    {
                                        bestImageUrl = imgNode.GetAttributeValue("src", "");
                                    }
                                }
                                
                                // Nettoyer l'URL si nécessaire (enlever les paramètres de taille)
                                if (!string.IsNullOrEmpty(bestImageUrl))
                                {
                                    // Si l'URL contient un espace (comme dans un srcset), prendre la première partie
                                    if (bestImageUrl.Contains(" "))
                                    {
                                        bestImageUrl = bestImageUrl.Split(' ')[0];
                                    }
                                    
                                    // Remplacer les règles de redimensionnement par la règle originale si possible
                                    if (bestImageUrl.Contains("rule=classified-"))
                                    {
                                        bestImageUrl = Regex.Replace(bestImageUrl, @"rule=classified-[^&]+", "rule=ad-image");
                                    }
                                    
                                    images.Add(bestImageUrl);
                                    imageCount++;
                                }
                            }
                        }
                        
                        // Si aucune image n'a été trouvée avec les balises picture, essayer avec les balises img directement
                        if (!images.Any())
                        {
                            var imgNodes = node.SelectNodes(".//img");
                            if (imgNodes != null)
                            {
                                int imageCount = 0;
                                foreach (var imgNode in imgNodes)
                                {
                                    if (imageCount >= 3) break; // Limiter à 3 images
                                    
                                    var srcset = imgNode.GetAttributeValue("srcset", "");
                                    var src = imgNode.GetAttributeValue("src", "");
                                    
                                    if (!string.IsNullOrEmpty(srcset))
                                    {
                                        var srcsetParts = srcset.Split(',')
                                            .Select(s => s.Trim().Split(' ')[0])
                                            .Where(s => !string.IsNullOrEmpty(s))
                                            .ToList();
                                        
                                        if (srcsetParts.Any())
                                        {
                                            images.Add(srcsetParts.Last());
                                            imageCount++;
                                            continue;
                                        }
                                    }
                                    
                                    if (!string.IsNullOrEmpty(src) && !src.Contains("data:image"))
                                    {
                                        images.Add(src);
                                        imageCount++;
                                    }
                                }
                            }
                        }

                        // Recherche plus approfondie des attributs
                        var paramsNode = node.SelectSingleNode(".//div[@data-test-id='ad-params-light']");
                        if (paramsNode == null)
                        {
                            // Essayer d'autres sélecteurs si le premier ne fonctionne pas
                            paramsNode = node.SelectSingleNode(".//div[contains(@class, 'adcard_46a669571')]");
                            
                            if (paramsNode == null)
                            {
                                paramsNode = node.SelectSingleNode(".//div[contains(@class, 'adcard_84a5c3b0f')]");
                                
                                if (paramsNode == null)
                                {
                                    // Essayer de trouver n'importe quel div qui pourrait contenir des attributs
                                    var divs = node.SelectNodes(".//div");
                                    if (divs != null)
                                    {
                                        foreach (var div in divs)
                                        {
                                            var text = div.InnerText.Trim();
                                            if (text.Contains("·") && (text.Contains("km") || text.Contains("Diesel") || text.Contains("Essence")))
                                            {
                                                paramsNode = div;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        var article = new LeboncoinArticle
                        {
                            Title = titleNode?.InnerText.Trim() ?? "",
                            Price = priceNode?.InnerText.Trim() ?? "",
                            Location = locationNode?.InnerText.Trim() ?? "",
                            Url = !string.IsNullOrEmpty(articleUrl) ? 
                                (articleUrl.StartsWith("http") ? articleUrl : $"https://www.leboncoin.fr{articleUrl}") : "",
                            Attributes = ExtractAttributes(paramsNode),
                            Images = images.Distinct().ToList()
                        };
                        
                        // Extraire la description si disponible (optionnel pour gagner du temps)
                        var descNode = node.SelectSingleNode(".//div[@data-test-id='ad-description']");
                        if (descNode != null)
                        {
                            article.Description = descNode.InnerText.Trim();
                            
                            // Extraire des attributs supplémentaires à partir de la description
                            ExtractAttributesFromDescription(article);
                        }
                        
                        articles.Add(article);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erreur lors de l'extraction des données d'un article");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du scraping de la page {PageNumber}", pageNumber);
            }
            
            return articles;
        }

        private Dictionary<string, string> ExtractAttributes(HtmlNode? paramsNode)
        {
            var attributes = new Dictionary<string, string>();
            
            if (paramsNode == null)
                return attributes;
            
            try
            {
                // Méthode 1: Essayer d'extraire à partir de la div data-test-id="ad-params-light"
                var text = paramsNode.InnerText.Trim();
                var parts = text.Split('·', StringSplitOptions.RemoveEmptyEntries);
                
                if (parts.Length >= 4)
                {
                    // Format typique des voitures sur Leboncoin: Année · Kilométrage · Carburant · Boîte
                    attributes["Année"] = parts[0].Trim();
                    attributes["Kilométrage"] = parts[1].Trim();
                    attributes["Carburant"] = parts[2].Trim();
                    attributes["Boîte de vitesse"] = parts[3].Trim();
                    
                    // S'il y a plus d'attributs, les ajouter aussi
                    for (int i = 4; i < parts.Length && i < 10; i++)
                    {
                        attributes[$"Attribut{i-3}"] = parts[i].Trim();
                    }
                    
                    return attributes;
                }
                
                // Méthode 2: Essayer d'extraire à partir de la div data-test-id="ad-params-labels"
                var labelsNode = paramsNode.SelectSingleNode(".//*[@data-test-id='ad-params-labels']") ?? 
                                 paramsNode.SelectSingleNode(".//*[contains(@class, 'ad-params-labels')]");
                
                if (labelsNode != null)
                {
                    var divs = labelsNode.SelectNodes(".//div");
                    if (divs != null)
                    {
                        foreach (var div in divs)
                        {
                            var paragraphs = div.SelectNodes(".//p");
                            if (paragraphs != null && paragraphs.Count >= 2)
                            {
                                var key = paragraphs[0].InnerText.Trim();
                                var value = paragraphs[1].InnerText.Trim();
                                
                                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                                {
                                    attributes[key] = value;
                                }
                            }
                        }
                        
                        if (attributes.Count > 0)
                        {
                            return attributes;
                        }
                    }
                }
                
                // Méthode 3: Essayer d'extraire à partir des classes spécifiques
                var pNodes = paramsNode.SelectNodes(".//p");
                if (pNodes != null)
                {
                    foreach (var p in pNodes)
                    {
                        var text2 = p.InnerText.Trim();
                        if (text2.Contains(":"))
                        {
                            var keyValue = text2.Split(':', 2);
                            if (keyValue.Length == 2)
                            {
                                var key = keyValue[0].Trim();
                                var value = keyValue[1].Trim();
                                
                                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                                {
                                    attributes[key] = value;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Juste logger l'erreur et continuer
                Console.WriteLine($"Erreur lors de l'extraction des attributs: {ex.Message}");
            }
            
            return attributes;
        }

        private void ExtractAttributesFromDescription(LeboncoinArticle article)
        {
            if (string.IsNullOrEmpty(article.Description))
                return;

            try
            {
                var description = article.Description.ToLower();
                
                // Si les attributs n'existent pas encore, les initialiser
                if (article.Attributes == null)
                    article.Attributes = new Dictionary<string, string>();
                
                // Extraire le kilométrage s'il n'est pas déjà présent
                if (!article.Attributes.ContainsKey("kilométrage") && !article.Attributes.ContainsKey("kilometrage"))
                {
                    var kmMatches = Regex.Matches(description, @"(\d{1,3}(?:\s?\d{3})*)\s*(?:km|kilometres|kilométres|kilométrage)");
                    if (kmMatches.Count > 0)
                    {
                        var kmValue = kmMatches[0].Groups[1].Value.Replace(" ", "");
                        article.Attributes["kilométrage"] = kmValue + " km";
                    }
                }
                
                // Extraire l'année si elle n'est pas déjà présente
                if (!article.Attributes.ContainsKey("année") && !article.Attributes.ContainsKey("annee"))
                {
                    var yearMatches = Regex.Matches(description, @"(?:année|de)\s+(\d{4})");
                    if (yearMatches.Count > 0)
                    {
                        article.Attributes["année"] = yearMatches[0].Groups[1].Value;
                    }
                }
                
                // Extraire le type de carburant s'il n'est pas déjà présent
                if (!article.Attributes.ContainsKey("carburant") && !article.Attributes.ContainsKey("énergie"))
                {
                    var fuelTypes = new Dictionary<string, string>
                    {
                        { "diesel", "Diesel" },
                        { "essence", "Essence" },
                        { "hybride", "Hybride" },
                        { "électrique", "Électrique" },
                        { "electrique", "Électrique" },
                        { "gpl", "GPL" }
                    };
                    
                    foreach (var fuel in fuelTypes)
                    {
                        if (description.Contains(fuel.Key))
                        {
                            article.Attributes["carburant"] = fuel.Value;
                            break;
                        }
                    }
                }
                
                // Extraire la boîte de vitesse si elle n'est pas déjà présente
                if (!article.Attributes.ContainsKey("boîte") && !article.Attributes.ContainsKey("boite"))
                {
                    if (description.Contains("automatique"))
                    {
                        article.Attributes["boîte"] = "Automatique";
                    }
                    else if (description.Contains("manuelle"))
                    {
                        article.Attributes["boîte"] = "Manuelle";
                    }
                }
                
                // Extraire le nombre de portes s'il n'est pas déjà présent
                if (!article.Attributes.ContainsKey("portes"))
                {
                    var doorMatches = Regex.Matches(description, @"(\d)\s*portes");
                    if (doorMatches.Count > 0)
                    {
                        article.Attributes["portes"] = doorMatches[0].Groups[1].Value;
                    }
                }
                
                // Extraire le nombre de places s'il n'est pas déjà présent
                if (!article.Attributes.ContainsKey("places"))
                {
                    var seatMatches = Regex.Matches(description, @"(\d)\s*places");
                    if (seatMatches.Count > 0)
                    {
                        article.Attributes["places"] = seatMatches[0].Groups[1].Value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'extraction d'attributs depuis la description");
            }
        }

        // Helper method to map seat counts to the appropriate values
        private string MapSeatCount(string seatCount)
        {
            if (int.TryParse(seatCount, out int count))
            {
                // If the count is 7 or more, return 999999 (more than 6)
                if (count >= 7)
                {
                    return "999999";
                }
                
                // Otherwise, return the original count if it's between 1 and 6
                if (count >= 1 && count <= 6)
                {
                    return count.ToString();
                }
            }
            
            return string.Empty;
        }

        // Méthode pour vérifier si une page existe en essayant de la charger
        private async Task<bool> PageExistsAsync(string url)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Head, url);
                
                // Ajouter des en-têtes pour simuler un navigateur
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
                
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la vérification de l'existence de la page {Url}", url);
                return false;
            }
        }
        
        // Méthode pour construire l'URL de la page suivante
        private string GetNextPageUrl(string currentUrl, int nextPageNumber)
        {
            if (currentUrl.Contains("page="))
            {
                // Remplacer le numéro de page existant
                return System.Text.RegularExpressions.Regex.Replace(currentUrl, @"page=\d+", $"page={nextPageNumber}");
            }
            else if (currentUrl.Contains("?"))
            {
                // Ajouter le paramètre de page
                return $"{currentUrl}&page={nextPageNumber}";
            }
            else
            {
                // Aucun paramètre existant, ajouter le premier
                return $"{currentUrl}?page={nextPageNumber}";
            }
        }
    }
} 