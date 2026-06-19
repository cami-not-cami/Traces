using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
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
        public async Task<IActionResult> Index()
        {
            var topCountries = await _context.TripActivities
                .Include(a => a.PlaceFkNavigation)
                .Where(a => a.PlaceFkNavigation != null && !string.IsNullOrEmpty(a.PlaceFkNavigation.CountryName))
                .GroupBy(a => a.PlaceFkNavigation.CountryName)
                .Select(g => new ExploreCardViewModel
                {
                    CountryName = g.Key,
                    TripCount = g.Select(x => x.TripDayFkNavigation.TripFk).Distinct().Count(),
                    // Grab first photo if available, otherwise use a placeholder
                    CoverPhoto = g.SelectMany(x => x.PlaceFkNavigation.PlacePhotos)
                                  .Select(p => p.GooglePhotoReference)
                                  .FirstOrDefault()
                })
                .OrderByDescending(c => c.TripCount)
                .Take(3)
                .ToListAsync();

            foreach (var country in topCountries)
            {
                country.CardLabel = country.TripCount == 1 ? "1 Trip" : $"{country.TripCount} Trips";
                country.Description = $"Discover the beauty of {country.CountryName} with {country.TripCount} custom {(country.TripCount == 1 ? "trip" : "trips")} created by our community.";
                if (string.IsNullOrEmpty(country.CoverPhoto))
                {
                    country.CoverPhoto = "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800?auto=format&fit=crop&w=800&q=80";
                }
            }

            return View(topCountries);
        }
        public void ExploreCards()
        {
            var recentTrips = _context.Trips
                          .Include(t => t.TripDays)
                          .ThenInclude(d => d.TripActivities)
                          .ThenInclude(a => a.PlaceFkNavigation)
                          .SelectMany(t => t.TripDays)
                          .SelectMany(d => d.TripActivities)
                          .GroupBy(a => a.PlaceFkNavigation.CountryName)
                          .Select(g => new
                          {
                             CountryName = g.Key,
                             TripCount = g.Count(),
                             Place = g.First().PlaceFkNavigation
                          })
                                 .OrderByDescending(x => x.TripCount)
                                 .Take(3)
                                 .ToList();
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
        [HttpPost]
        public IActionResult ExploreCardNavigationToTripPage(string countryName)
        {
            if (string.IsNullOrEmpty(countryName))
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return RedirectToAction("Index", "Trip", new { exploreCountry = countryName });
        }
    }
}
