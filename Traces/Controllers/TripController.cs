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
        public async Task<ActionResult> Index(string? placeId, string? startDate, string? endDate)
        {

            if (User.Identity.IsAuthenticated == false)
            {
                Guid anonymGuid = Guid.NewGuid();
            }
            var vm = new CreateTripViewModel();
            

            if (placeId != null)
            {
                var json = await _googlePlacesService.GetPlaceDetails(placeId);
                var googlePlace = JsonSerializer.Deserialize<GooglePlaceResponse>(json);

                var place = new PlaceViewModel
                {
                    Name = googlePlace.DisplayName.Text,
                    Latitude = (decimal)googlePlace.Location.Latitude,
                    Longitude = (decimal)googlePlace.Location.Longitude,
                    FormattedAddress = googlePlace.FormattedAddress,
                };

               vm= new CreateTripViewModel
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
            return View(vm);
        }
        private async Task<CreateTripViewModel?> GetTripViewModelAsync(int tripId)
        {
            var trip = await _context.Trips
                .Include(t => t.TripDays)
                    .ThenInclude(d => d.TripActivities)
                        .ThenInclude(a => a.PlaceFkNavigation)
                .Include(t => t.TripDays)
                    .ThenInclude(d => d.TripActivities)
                        .ThenInclude(a => a.Checklists)
                .Include(t => t.TripDays)
                    .ThenInclude(d => d.TripActivities)
                        .ThenInclude(a => a.Notes)
                .FirstOrDefaultAsync(t => t.TrIdPk == tripId);

            if (trip == null) return null;

            // Load members associated with the trip
            var members = await _context.TripMembers
                .Where(tm => tm.TripFk == tripId)
                .Include(tm => tm.IdFkNavigation)
                .Select(tm => new TripMemberInfo
                {
                    TripMemberId = tm.IdFk,
                    UserId = tm.IdFkNavigation.UserFk,
                    FirstName = tm.IdFkNavigation.FirstName,
                    LastName = tm.IdFkNavigation.LastName
                })
                .ToListAsync();

            // Map DB TripDay and TripActivity models to ViewModels
            var dayViewModels = trip.TripDays
                .OrderBy(d => d.DayNumber)
                .Select(d => new TripDayViewModel
                {
                    TripDayId = d.TrDaIdPk,
                    DayNumber = d.DayNumber ?? 0,
                    Date = d.Date,
                    Activities = d.TripActivities
                        .OrderBy(a => a.OrderIndex)
                        .Select(a => new TripActivityViewModel
                        {
                            TripActivityId = a.TrAcIdPk,
                            TripDayId = a.TripDayFk,
                            StartTime = a.StartTime.HasValue ? d.Date.ToDateTime(a.StartTime.Value) : null,
                            EndTime = a.EndTime.HasValue ? d.Date.ToDateTime(a.EndTime.Value) : null,
                            OrderIndex = a.OrderIndex,
                            Status = a.Status,
                            Place = new PlaceViewModel
                            {
                                PlaceId = a.PlaceFkNavigation.PlIdPk,
                                GooglePlaceId = a.PlaceFkNavigation.GooglePlaceId,
                                Name = a.PlaceFkNavigation.Name,
                                Latitude = a.PlaceFkNavigation.Latitude,
                                Longitude = a.PlaceFkNavigation.Longitude,
                                FormattedAddress = a.PlaceFkNavigation.FormattedAddress,
                                City = a.PlaceFkNavigation.City,
                                PrimaryCategory = a.PlaceFkNavigation.PrimaryCategory
                            },
                            ChecklistItems = a.Checklists.ToList()
                        })
                        .ToList()
                })
                .ToList();

            var allNotes = trip.TripDays
                .SelectMany(d => d.TripActivities)
                .SelectMany(a => a.Notes)
                .ToList();

            var placesToVisit = dayViewModels
                .SelectMany(d => d.Activities)
                .Select(a => a.Place)
                .GroupBy(p => p.GooglePlaceId)
                .Select(g => g.First())
                .ToList();

            // Select the first place's coordinates to center the map
            var latitude = placesToVisit.FirstOrDefault()?.Latitude;
            var longitude = placesToVisit.FirstOrDefault()?.Longitude;

            return new CreateTripViewModel
            {
                TripId = trip.TrIdPk,
                Title = trip.Title ?? "",
                Description = trip.Description ?? "",
                StartDate = trip.StartDate,
                EndDate = trip.EndDate,
                Budget = trip.Budget,
                Latitude = latitude,
                Longitude = longitude,
                Members = members,
                Days = dayViewModels,
                Notes = allNotes,
                PlacesToVisit = placesToVisit
            };
        }
        private async Task AddActivityToTrip(int tripId)
        {

        }
        private async Task SetBudget(int tripId)
        {

        }



    }
}
