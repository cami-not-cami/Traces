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
        public async Task<string> GetPlaceDetails(string placeId)
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
                request.Headers.Add("X-Goog-FieldMask", "displayName,formattedAddress,location,photos");
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
        public async Task<string> GetAutocomplete(string textInput)
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

                var payload = new
                {
                    input = textInput,
                    
                };

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
