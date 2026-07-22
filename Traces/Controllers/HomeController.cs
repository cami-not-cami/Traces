using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Traces.Models;
using Traces.Services;

namespace Traces.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        private readonly TracesDbContext _context;
        private readonly GooglePlacesService _googlePlacesService;

        public HomeController(
            ILogger<HomeController> logger,
            IOptions<Settings> settings,
            TracesDbContext context,
            GooglePlacesService googlePlacesService
        )
        {
            _logger = logger;
            _context = context;
            _googlePlacesService = googlePlacesService;
        }

        public async Task<IActionResult> Index()
        {
            var topCountriesData = await _context
                .TripActivities.Include(a => a.PlaceFkNavigation)
                .Where(a =>
                    a.PlaceFkNavigation != null
                    && !string.IsNullOrEmpty(a.PlaceFkNavigation.CountryName)
                )
                .GroupBy(a => a.PlaceFkNavigation.CountryName)
                .Select(g => new
                {
                    CountryName = g.Key,
                    TripCount = g.Select(x => x.TripDayFkNavigation.TripFk).Distinct().Count(),
                    GooglePlaceId = g.Where(x => x.PlaceFkNavigation.PlacePhotos.Any())
                        .Select(x => x.PlaceFkNavigation.GooglePlaceId)
                        .FirstOrDefault() ?? g.Select(x => x.PlaceFkNavigation.GooglePlaceId).FirstOrDefault(),
                    CoverPhoto = g.SelectMany(x => x.PlaceFkNavigation.PlacePhotos)
                        .Select(p => p.GooglePhotoReference)
                        .FirstOrDefault()
                })
                .OrderByDescending(c => c.TripCount)
                .Take(3)
                .ToListAsync();

            var topCountries = new List<ExploreCardViewModel>();

            foreach (var item in topCountriesData)
            {
                var card = new ExploreCardViewModel
                {
                    CountryName = item.CountryName,
                    TripCount = item.TripCount,
                    CoverPhoto = item.CoverPhoto
                };

                if (!string.IsNullOrEmpty(item.GooglePlaceId))
                {
                    try
                    {
                        var json = await _googlePlacesService.GetPlaceDetails(item.GooglePlaceId, includePhotos: true);
                        if (!string.IsNullOrEmpty(json) && !json.StartsWith("Error"))
                        {
                            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                            var placeDetails = JsonSerializer.Deserialize<GooglePlaceResponse>(json, options);
                            var freshPhoto = placeDetails?.Photos?.FirstOrDefault()?.Name;

                            if (!string.IsNullOrEmpty(freshPhoto))
                            {
                                card.CoverPhoto = freshPhoto;

                                // Update the database cache
                                var dbPhotos = await _context.PlacePhotos
                                    .Where(p => p.PlacesFkNavigation.GooglePlaceId == item.GooglePlaceId)
                                    .ToListAsync();

                                if (dbPhotos.Any())
                                {
                                    foreach (var dbPhoto in dbPhotos)
                                    {
                                        dbPhoto.GooglePhotoReference = freshPhoto;
                                    }
                                }
                                else
                                {
                                    var place = await _context.Places.FirstOrDefaultAsync(p => p.GooglePlaceId == item.GooglePlaceId);
                                    if (place != null)
                                    {
                                        _context.PlacePhotos.Add(new PlacePhoto
                                        {
                                            PlacesFk = place.PlIdPk,
                                            GooglePhotoReference = freshPhoto
                                        });
                                    }
                                }
                                await _context.SaveChangesAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error refreshing explore card cover photo for {Country}", item.CountryName);
                    }
                }

                card.CardLabel = card.TripCount == 1 ? "1 Trip" : $"{card.TripCount} Trips";
                card.Description = $"Discover the beauty of {card.CountryName} with {card.TripCount} custom {(card.TripCount == 1 ? "trip" : "trips")} created by our community.";
                
                if (string.IsNullOrEmpty(card.CoverPhoto))
                {
                    card.CoverPhoto = "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800?auto=format&fit=crop&w=800&q=80";
                }

                topCountries.Add(card);
            }

            return View(topCountries);
        }
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(
                new ErrorViewModel
                {
                    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                }
            );
        }

        public async Task<IActionResult> Autocomplete(
            string textInput,
            double? latitude = null,
            double? longitude = null
        )
        {
            var jsonResponse = await _googlePlacesService.GetAutocomplete(
                textInput,
                latitude,
                longitude
            );
            return Content(jsonResponse, "application/json");
        }

        public async Task<IActionResult> PlaceDetails(string placeId)
        {
            if (string.IsNullOrEmpty(placeId))
            {
                return BadRequest("Place ID is required");
            }

            var existingPlace = await _context.Places
                .Include(p => p.PlacePhotos)
                .FirstOrDefaultAsync(p => p.GooglePlaceId == placeId);

            // We always want to fetch fresh photos because Google Photo References expire
            var jsonResponse = await _googlePlacesService.GetPlaceDetails(placeId, includePhotos: true);

            if (existingPlace != null && !jsonResponse.StartsWith("Error"))
            {
                try
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var placeDetails = JsonSerializer.Deserialize<GooglePlaceResponse>(jsonResponse, options);
                    var freshPhoto = placeDetails?.Photos?.FirstOrDefault()?.Name;

                    if (!string.IsNullOrEmpty(freshPhoto))
                    {
                        // Update in database cache
                        var dbPhotos = existingPlace.PlacePhotos.ToList();
                        if (dbPhotos.Any())
                        {
                            foreach (var dbPhoto in dbPhotos)
                            {
                                dbPhoto.GooglePhotoReference = freshPhoto;
                            }
                        }
                        else
                        {
                            _context.PlacePhotos.Add(new PlacePhoto
                            {
                                PlacesFk = existingPlace.PlIdPk,
                                GooglePhotoReference = freshPhoto
                            });
                        }
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error refreshing place details photos in database for placeId {PlaceId}", placeId);
                }
            }

            return Content(jsonResponse, "application/json");
        }

        [HttpPost]
        public async Task<IActionResult> SwitchToTripPlanning(
            string placeId,
            DateOnly? startDate,
            DateOnly? endDate
        )
        {
            if (string.IsNullOrEmpty(placeId))
            {
                ModelState.AddModelError("PlaceId", "Please select a valid location.");
                return View("Index");
            }
            return RedirectToAction(
                "Index",
                "Trip",
                new
                {
                    placeId,
                    startDate = startDate?.ToString("yyyy-MM-dd"),
                    endDate = endDate?.ToString("yyyy-MM-dd"),
                }
            );
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
