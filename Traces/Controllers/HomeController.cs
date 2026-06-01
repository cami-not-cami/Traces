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
        //public IActionResult Trip()
        //{
        //    return RedirectToAction("Index", "TripPlanner");
        //}
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        public async Task<IActionResult> Autocomplete(string textInput)
        {
         
            var jsonResponse = await _googlePlacesService.GetAutocomplete(textInput);
            return Content(jsonResponse, "application/json");
        }
        [HttpPost]
        public async Task<IActionResult> SwitchToTripPlanning(string placeId, DateTime? startDate, DateTime? endDate)
        {
            if (User.Identity.IsAuthenticated == false)
            {
                Guid anonymGuid = Guid.NewGuid();
            }

            if (string.IsNullOrEmpty(placeId))
            {
                ModelState.AddModelError("PlaceId", "Please select a valid location.");
                return View("Index");
            }
            TempData["placeId"] = placeId; 
            TempData["startDate"] = startDate?.ToString("yyyy-MM-dd"); 
            TempData["endDate"] = endDate?.ToString("yyyy-MM-dd");
            return RedirectToAction("Index", "Trip");
        }
    }
}
