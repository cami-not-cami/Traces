using Microsoft.AspNetCore.Mvc;

namespace Traces.Services
{
    public class GoogleMapsServices
    {
        public async Task<IActionResult> InitialiseMaps(string apiKey)
        {
      
            return new JsonResult(new { success = true, data = "" });
        }
        public async Task<IActionResult> PlacePins(double latitude, double longitude)
        {
            //places a pin on the map
            return new JsonResult(new { success = true, data = "Nearby places would be here" });
        }
        /// <summary>
        /// calls the ROUTE MATRIX API to get directions between two routes,also the time, then saves the details to the database
        /// and returns the details as a JSON response or appropriate format for the frontend
        /// </summary>
        /// <returns>directions and time</returns>
        public async Task<IActionResult> GetDirectionsBetweenRoutes()
        {             //get directions between two routes
            return new JsonResult(new { success = true, data = "Directions between routes would be here" });
        }



    }
}
