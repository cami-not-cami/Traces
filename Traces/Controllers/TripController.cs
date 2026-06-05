using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Traces.Models;
using Traces.Services;

namespace Traces.Controllers
{
    public class TripController : Controller
    {
        private readonly TracesDbContext _context;
        private readonly GooglePlacesService _googlePlacesService;

        public TripController(TracesDbContext context, GooglePlacesService googlePlacesService)
        {
            _context = context;
            _googlePlacesService = googlePlacesService;
        }
        // GET: TripController
        public async Task<ActionResult> Index(string placeId, string startDate, string endDate)
        {

            if (User.Identity.IsAuthenticated == false)
            {
                Guid anonymGuid = Guid.NewGuid();
            }

            if (string.IsNullOrEmpty(placeId))
                return RedirectToAction("Index", "Home");

            var json = await _googlePlacesService.GetPlaceDetails(placeId);
            var googlePlace = JsonSerializer.Deserialize<GooglePlaceResponse>(json);

            var place = new PlaceViewModel
            {
                Name = googlePlace.DisplayName.Text,
                Latitude = (decimal)googlePlace.Location.Latitude,
                Longitude = (decimal)googlePlace.Location.Longitude,
                FormattedAddress = googlePlace.FormattedAddress,
            };

            var vm = new CreateTripViewModel
            {
                Title = $"Trip to {place.Name}",
                StartDate = DateOnly.TryParse(startDate, out var start) ? start : null,
                EndDate = DateOnly.TryParse(endDate, out var end) ? end : null,
                Budget = 0.0d,
                Latitude = place.Latitude,
                Longitude = place.Longitude,
                PlacesToVisit = new List<PlaceViewModel> { place },
            };
            return View(vm);
        }

        // GET: TripController/Details/5
        public async Task<ActionResult> Details(int id)
        {
            var trip = await _context.Trips.FirstOrDefaultAsync(t => t.TrIdPk == id);

            if (trip == null) return NotFound();

            var days = await _context.TripDays
                .Where(d => d.TripFk == id)
                .OrderBy(d => d.DayNumber)
                .ToListAsync();

            var vm = new CreateTripViewModel
            {
                TripId = trip.TrIdPk,
                Title = trip.Title ?? "",
                Description = trip.Description ?? "",
                StartDate = trip.StartDate,
                EndDate = trip.EndDate,
                Budget = trip.Budget
            };

            return View(vm);
        }

        
    }
}
