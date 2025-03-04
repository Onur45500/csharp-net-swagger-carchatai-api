namespace csharp_net_swagger_carchat_api.Models
{
    public class LeboncoinArticle
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, string> Attributes { get; set; } = new();
        public List<string> Images { get; set; } = new();
    }

    public class SearchParameters
    {
        public string Category { get; set; } = "2"; // 2 is for cars
        public string? Brand { get; set; }
        public string? Model { get; set; }
        public string? MinPrice { get; set; }
        public string? MaxPrice { get; set; }
        public string? Location { get; set; }
        public string? Keywords { get; set; }
        public string? FuelType { get; set; }  // e.g., "2" for Diesel
        public string? RegDateMin { get; set; } // e.g., "2000"
        public string? RegDateMax { get; set; } // e.g., "2020"
        public List<string>? VehicleTypes { get; set; } // e.g., "berline", "SUV", "4x4", etc.
        public List<string>? Doors { get; set; } // e.g., "2", "3", "4", "5"
        public List<string>? Seats { get; set; } // e.g., "2", "4", "5", "999999" (for more than 6)
    }
} 