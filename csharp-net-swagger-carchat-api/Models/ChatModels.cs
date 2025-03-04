using System.Text.Json.Serialization;

namespace csharp_net_swagger_carchat_api.Models
{
    public class ChatRequest
    {
        public string Prompt { get; set; } = string.Empty;
    }

    public class ChatResponse
    {
        public string Response { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    public class ComparisonRequest
    {
        public List<LeboncoinArticle> Cars { get; set; } = new();
        public string Question { get; set; } = string.Empty;
    }

    public class FilterRequest
    {
        public List<LeboncoinArticle> Cars { get; set; } = new();
        public string FilterQuery { get; set; } = string.Empty;
    }

    public class FilterResponse
    {
        [JsonPropertyName("filtered_car_ids")]
        public List<int> FilteredCarIds { get; set; } = new();

        [JsonPropertyName("explanation")]
        public string Explanation { get; set; } = string.Empty;
    }
} 