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
            _googleApiKey = _settings.ApiKey;
            _httpClient = httpClient;
        }


        //Handle api call then save to db
        //refine look up
        //send autocomplete res to front
        public async Task<IActionResult> GetPlaceDetails(string placeId)
        {
            
            return new JsonResult(new { success = true, data = "Place details would be here" });
        }
        public async Task<IActionResult> GetAutocomplete(string textInput)
        {
            // Call Google Places API to get autocomplete for the given text input
            // Parse the response and save relevant details to the database
            // Return the details as a JSON response or appropriate format for the frontend
            if(textInput  == null) 
            {
                    return new JsonResult(new { success = false, message = "Text input cannot be null" });
            }
            else
            {
                string url = "https://places.googleapis.com/v1/places:autocomplete";

                var payload = new
                {
                    input = textInput,
                    locationBias = new
                    {
                        circle = new
                        {
                            center = new
                            {
                                latitude = 47.200,
                                longitude = 13.200
                            },
                            radius = 5000.0
                        }
                    }
                };

                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, url);
                    request.Headers.Add("X-Goog-Api-Key", _googleApiKey);
                    request.Headers.Add("X-Goog-FieldMask", "suggestions.placePrediction.text.text");
                    request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync();

                    return new ContentResult { Content = json, ContentType = "application/json" };

                }
                catch (Exception ex)
                {
                    return new JsonResult(new { success = false, message = $"Error calling Google Places API: {ex.Message}" });
                }
            }
        }
        public async Task<IActionResult> GetNearbyPlaces(double latitude, double longitude)
        {

            return new JsonResult(new { success = true, data = "Nearby places would be here" });
        }
        public async Task<IActionResult> GetPlacePhoto(string photoReference)
        {
            //get photos from google api, dont know how i will handle the saving 
            return new JsonResult(new { success = true, data = "Place photo would be here" });
        } 
    }
}
