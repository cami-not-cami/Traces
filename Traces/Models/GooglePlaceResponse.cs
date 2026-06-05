using System.Text.Json.Serialization;

namespace Traces.Models
{
    public class GooglePlaceResponse
    {
        [JsonPropertyName("displayName")]
        public GoogleDisplayName DisplayName { get; set; }

        [JsonPropertyName("formattedAddress")]
        public string FormattedAddress { get; set; }

        [JsonPropertyName("location")]
        public GoogleLocation Location { get; set; }

        [JsonPropertyName("photos")]
        public List<GooglePhoto> Photos { get; set; }
    }

    public class GoogleDisplayName
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("languageCode")]
        public string LanguageCode { get; set; }
    }

    public class GoogleLocation
    {
        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }
    }

    public class GooglePhoto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("widthPx")]
        public int WidthPx { get; set; }

        [JsonPropertyName("heightPx")]
        public int HeightPx { get; set; }
    }
}
