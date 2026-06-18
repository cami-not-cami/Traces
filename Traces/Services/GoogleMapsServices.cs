using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
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
        public async Task<IActionResult> GetDirectionsBetweenRoutes()
        {          
            string url = "https://routes.googleapis.com/directions/v2:computeRoutes"; 


            return new JsonResult(new { success = true, data = "Directions between routes would be here" });
        }



    }
}
