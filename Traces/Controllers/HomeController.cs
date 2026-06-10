using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using Traces.Models;
using Traces.Services;

namespace Traces.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        
        private readonly TracesDbContext _context;
        private readonly GooglePlacesService _googlePlacesService;
      
        public HomeController(ILogger<HomeController> logger, IOptions<Settings> settings, TracesDbContext context, GooglePlacesService googlePlacesService)
        {
            _logger = logger;
            _context = context;
            _googlePlacesService = googlePlacesService;
        }
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
       
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        public async Task<IActionResult> Autocomplete(string textInput, double? latitude = null, double? longitude = null)
        {
         
            var jsonResponse = await _googlePlacesService.GetAutocomplete(textInput, latitude, longitude);
            return Content(jsonResponse, "application/json");
        }
        public async Task<IActionResult> PlaceDetails(string placeId)
        {
            var jsonResponse = await _googlePlacesService.GetPlaceDetails(placeId);
            return Content(jsonResponse, "application/json");
        }
        [HttpPost]
        public async Task<IActionResult> SwitchToTripPlanning(string placeId, DateOnly? startDate, DateOnly? endDate)
        {
           
            if (string.IsNullOrEmpty(placeId))
            {
                ModelState.AddModelError("PlaceId", "Please select a valid location.");
                return View("Index");
            }
            return RedirectToAction("Index", "Trip", new
            {
                placeId,
                startDate = startDate?.ToString("yyyy-MM-dd"),
                endDate = endDate?.ToString("yyyy-MM-dd")
            });
        }
    }
}
