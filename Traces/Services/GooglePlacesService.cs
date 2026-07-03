using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Options;
using Traces.Controllers;
using Traces.Models;

namespace Traces.Services
{
    public class GooglePlacesService
    {
        private readonly Settings _settings;
        private readonly string _googleApiKey;
        private readonly HttpClient _httpClient;
        public GooglePlacesService(IOptions<Settings> settings, HttpClient httpClient)
        {
            _settings = settings.Value;
            _googleApiKey = _settings.GoogleApiKey;
            _httpClient = httpClient;
        }

        //Handle api call then save to db
        //refine look up
        //send autocomplete res to front
        public async Task<string> GetPlaceDetails(string placeId, bool includePhotos = true)
        {
            if (string.IsNullOrWhiteSpace(placeId))
            {
                return   "Place ID cannot be null or empty" ;
            }
            else
            {
                string url = $"https://places.googleapis.com/v1/places/{placeId}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);

                request.Headers.Add("X-Goog-Api-Key", _googleApiKey);
                string fields = "displayName,formattedAddress,location,rating,userRatingCount,reviews,nationalPhoneNumber,websiteUri,regularOpeningHours,editorialSummary,priceLevel";
                if (includePhotos)
                {
                    fields = "displayName,formattedAddress,location,photos,rating," +
                             "userRatingCount,reviews,nationalPhoneNumber,websiteUri,regularOpeningHours,editorialSummary,priceLevel";
                }
                request.Headers.Add("X-Goog-FieldMask", fields);
                try
                {
                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                 
                    return json;

                }
                catch (Exception ex)
                {
                    return   $"Error calling Google Places API: {ex.Message}" ;
                }
            }
        }
        public async Task<string> GetAutocomplete(string textInput, double? latitude = null, double? longitude = null)
        {
            // Call Google Places API to get autocomplete for the given text input
            // Parse the response and save relevant details to the database
            // Return the details as a JSON response or appropriate format for the frontend
            if(textInput  == null) 
            {
                    return "Text input cannot be null";
            }
            else
            {
                string url = "https://places.googleapis.com/v1/places:autocomplete";

                object payload;
                if (latitude.HasValue && longitude.HasValue)
                {
                    payload = new
                    {
                        input = textInput,
                        locationBias = new
                        {
                            circle = new
                            {
                                center = new
                                {
                                    latitude = latitude.Value,
                                    longitude = longitude.Value
                                },
                                radius = 5000.0 // 50 km radius bias
                            }
                        },
                    
                    };
                }
                else
                {
                    payload = new
                    {
                        input = textInput
                    };
                }

                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, url);
                    request.Headers.Add("X-Goog-Api-Key", _googleApiKey);
                    request.Headers.Add("X-Goog-FieldMask", "suggestions.placePrediction.text.text,suggestions.placePrediction.placeId");
                    request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync();

                    return json;

                }
                catch (Exception ex)
                {
                    return   $"Error calling Google Places API: {ex.Message}" ;
                }
            }
        }
    }
}
