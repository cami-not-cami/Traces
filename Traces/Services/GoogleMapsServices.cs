using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;
using Traces.Models;

namespace Traces.Services
{
    public class GoogleMapsServices
    {
        private readonly Settings _settings;
        private readonly string _googleApiKey;
        private readonly HttpClient _httpClient;

        public GoogleMapsServices(IOptions<Settings> settings, HttpClient httpClient)
        {
            _settings = settings.Value;
            _googleApiKey = _settings.GoogleApiKey;
            _httpClient = httpClient;
        }

        /// <summary>
        /// calls the ROUTE MATRIX API to get directions between two routes,also the time
        /// and returns the details as a JSON response or appropriate format for the frontend
        /// </summary>
        /// <returns>directions and time</returns>
        public async Task<RouteDTO> GetDirectionsBetweenRoutes(string originId, string destinationId, string travelMode)
        {          
            if (string.IsNullOrWhiteSpace(originId)) {
                throw new ArgumentException("Origin ID cannot be null or empty", nameof(originId));
            }
            if (string.IsNullOrWhiteSpace(destinationId)) {
                throw new ArgumentException("Destination ID cannot be null or empty", nameof(destinationId));
            }
            if (string.IsNullOrWhiteSpace(travelMode)) {
                travelMode = "DRIVE"; // default 
            }

            string url = "https://routes.googleapis.com/directions/v2:computeRoutes";
            object payload;
            if (travelMode == "DRIVE" || travelMode == "TWO_WHEELER")
            {
                payload = new
                {
                    origin = new { placeId = originId },
                    destination = new { placeId = destinationId },
                    travelMode = travelMode,
                    routingPreference = "TRAFFIC_UNAWARE"
                };
            }
            else
            {
                payload = new
                {
                    origin = new { placeId = originId },
                    destination = new { placeId = destinationId },
                    travelMode = travelMode
                };
            }

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("X-Goog-Api-Key", _googleApiKey);
            request.Headers.Add("X-Goog-FieldMask", "routes.duration,routes.distanceMeters,routes.polyline.encodedPolyline");

            request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();

            // Deserialize using simple strongly-typed DTOs below
            var googleResponse = System.Text.Json.JsonSerializer.Deserialize<GoogleRoutesResponse>(responseContent);
            var route = googleResponse?.Routes?.FirstOrDefault();

            if (route != null)
            {
                int duration = 0;
                if (!string.IsNullOrEmpty(route.Duration) && route.Duration.EndsWith("s"))
                {
                    int.TryParse(route.Duration.Substring(0, route.Duration.Length - 1), out duration);
                }

                return new RouteDTO
                {
                    PolylineEncoded = route.Polyline?.EncodedPolyline ?? "",
                    DistanceMeters = route.DistanceMeters,
                    DurationSeconds = duration
                };
            }

            return null;
        }
    }

    public class RouteDTO
    {
        public string PolylineEncoded { get; set; }
        public int DistanceMeters { get; set; }
        public int DurationSeconds { get; set; }
    }

    public class GoogleRoutesResponse
    {
        [JsonPropertyName("routes")]
        public List<GoogleRoute> Routes { get; set; }
    }

    public class GoogleRoute
    {
        [JsonPropertyName("distanceMeters")]
        public int DistanceMeters { get; set; }

        [JsonPropertyName("duration")]
        public string Duration { get; set; } // Kept as string to handle the "s" suffix

        [JsonPropertyName("polyline")]
        public GooglePolyline Polyline { get; set; }
    }

    public class GooglePolyline
    {
        [JsonPropertyName("encodedPolyline")]
        public string EncodedPolyline { get; set; }
    }
}
