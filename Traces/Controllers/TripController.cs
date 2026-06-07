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
        public async Task<ActionResult> Index(int? tripId, string? placeId, string? startDate, string? endDate)
        {
            if (User.Identity.IsAuthenticated == false)
            {
                Guid anonymGuid = Guid.NewGuid();
            }

            if (tripId.HasValue && tripId.Value > 0)
            {
                var savedVm = await GetTripViewModelAsync(tripId.Value);
                if (savedVm != null)
                {
                    return View(savedVm);
                }
            }

            var vm = new CreateTripViewModel();

            if (placeId != null)
            {
                var json = await _googlePlacesService.GetPlaceDetails(placeId);
                var googlePlace = JsonSerializer.Deserialize<GooglePlaceResponse>(json);

                ViewBag.CoverPhoto = googlePlace.Photos?.FirstOrDefault()?.Name;

                var place = new PlaceViewModel
                {
                    Name = googlePlace.DisplayName.Text,
                    Latitude = (decimal)googlePlace.Location.Latitude,
                    Longitude = (decimal)googlePlace.Location.Longitude,
                    FormattedAddress = googlePlace.FormattedAddress,
                };

                vm = new CreateTripViewModel
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

        [HttpPost]
        public async Task<IActionResult> Save(CreateTripViewModel model)
        {
            var trip = new Trip
            {
                Title = model.Title ?? "My Trip",
                Description = model.Description ?? "",
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                Budget = model.Budget ?? 0.0d
            };

            _context.Trips.Add(trip);
            await _context.SaveChangesAsync();

            // Create Day 0 for Unscheduled activities
            var day0 = new TripDay
            {
                TripFk = trip.TrIdPk,
                DayNumber = 0,
                Date = DateOnly.MinValue
            };
            _context.TripDays.Add(day0);

            if (model.StartDate.HasValue && model.EndDate.HasValue)
            {
                int dayNumber = 1;
                var currentDate = model.StartDate.Value;
                while (currentDate <= model.EndDate.Value)
                {
                    var tripDay = new TripDay
                    {
                        TripFk = trip.TrIdPk,
                        DayNumber = dayNumber,
                        Date = currentDate
                    };
                    _context.TripDays.Add(tripDay);

                    dayNumber++;
                    currentDate = currentDate.AddDays(1);
                }
            }

            await _context.SaveChangesAsync();

            // Add initial place to Day 0 if any
            if (model.PlacesToVisit != null && model.PlacesToVisit.Any())
            {
                foreach (var placeVm in model.PlacesToVisit)
                {
                    int placeId = await GetOrCreatePlaceAsync(placeVm);
                    var activity = new TripActivity
                    {
                        TripDayFk = day0.TrDaIdPk,
                        PlaceFk = placeId,
                        Status = "Attraction",
                        OrderIndex = 0
                    };
                    _context.TripActivities.Add(activity);
                }
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index", new { tripId = trip.TrIdPk });
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
                    Date = d.Date == DateOnly.MinValue ? (DateOnly?)null : d.Date,
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
        [HttpPost]
        public async Task<IActionResult> AddActivityToTrip(
            int tripId,
            string? tripTitle,
            string? tripStartDate,
            string? tripEndDate,
            string? placeName,
            string? googlePlaceId,
            decimal? latitude,
            decimal? longitude,
            string? formattedAddress,
            int dayNumber,
            string? category,
            string? startTime,
            string? endTime,
            string? notes)
        {
            if (tripId == 0)
            {
                // Create the Trip dynamically
                var trip = new Trip
                {
                    Title = tripTitle ?? "My Trip",
                    Description = "",
                    StartDate = DateOnly.TryParse(tripStartDate, out var sDate) ? sDate : null,
                    EndDate = DateOnly.TryParse(tripEndDate, out var eDate) ? eDate : null,
                    Budget = 0.0d
                };

                _context.Trips.Add(trip);
                await _context.SaveChangesAsync();
                tripId = trip.TrIdPk;

                var day0 = new TripDay
                {
                    TripFk = tripId,
                    DayNumber = 0,
                    Date = DateOnly.MinValue
                };
                _context.TripDays.Add(day0);

                if (trip.StartDate.HasValue && trip.EndDate.HasValue)
                {
                    int dn = 1;
                    var currentDate = trip.StartDate.Value;
                    while (currentDate <= trip.EndDate.Value)
                    {
                        var tripDay = new TripDay
                        {
                            TripFk = tripId,
                            DayNumber = dn,
                            Date = currentDate
                        };
                        _context.TripDays.Add(tripDay);

                        dn++;
                        currentDate = currentDate.AddDays(1);
                    }
                }
                await _context.SaveChangesAsync();
            }

            var targetDay = await _context.TripDays
                .FirstOrDefaultAsync(d => d.TripFk == tripId && d.DayNumber == dayNumber);

            if (targetDay == null)
            {
                var trip = await _context.Trips.FirstOrDefaultAsync(t => t.TrIdPk == tripId);
                targetDay = new TripDay
                {
                    TripFk = tripId,
                    DayNumber = dayNumber,
                    Date = (trip?.StartDate != null && dayNumber > 0)
                        ? trip.StartDate.Value.AddDays(dayNumber - 1)
                        : DateOnly.MinValue
                };
                _context.TripDays.Add(targetDay);
                await _context.SaveChangesAsync();
            }

            var placeVm = new PlaceViewModel
            {
                GooglePlaceId = googlePlaceId,
                Name = placeName ?? "Unnamed Place",
                Latitude = latitude,
                Longitude = longitude,
                FormattedAddress = formattedAddress ?? "",
                PrimaryCategory = category ?? "Attraction"
            };
            int placeId = await GetOrCreatePlaceAsync(placeVm);

            int maxOrderIndex = await _context.TripActivities
                .Where(a => a.TripDayFk == targetDay.TrDaIdPk)
                .Select(a => (int?)a.OrderIndex)
                .MaxAsync() ?? -1;
            int nextOrderIndex = maxOrderIndex + 1;

            TimeOnly? start = null;
            if (TimeOnly.TryParse(startTime, out var st)) start = st;
            TimeOnly? end = null;
            if (TimeOnly.TryParse(endTime, out var et)) end = et;

            var activity = new TripActivity
            {
                TripDayFk = targetDay.TrDaIdPk,
                PlaceFk = placeId,
                StartTime = start,
                EndTime = end,
                OrderIndex = nextOrderIndex,
                Status = category ?? "Attraction"
            };

            _context.TripActivities.Add(activity);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(notes))
            {
                var note = new Note
                {
                    NoIdPk = (await _context.Notes.Select(n => (int?)n.NoIdPk).MaxAsync() ?? 0) + 1,
                    TripActivityFk = activity.TrAcIdPk,
                    Content = notes
                };
                _context.Notes.Add(note);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index", new { tripId = tripId });
        }

        private async Task<int> GetOrCreatePlaceAsync(PlaceViewModel placeVm)
        {
            var existing = await _context.Places.FirstOrDefaultAsync(p => p.GooglePlaceId == placeVm.GooglePlaceId);
            if (existing != null)
            {
                return existing.PlIdPk;
            }

            var newPlace = new Place
            {
                GooglePlaceId = placeVm.GooglePlaceId ?? Guid.NewGuid().ToString(),
                Name = placeVm.Name ?? "Unnamed Place",
                Latitude = placeVm.Latitude,
                Longitude = placeVm.Longitude,
                FormattedAddress = placeVm.FormattedAddress ?? "",
                City = placeVm.City ?? "",
                PrimaryCategory = placeVm.PrimaryCategory ?? "Attraction",
                CountryName = ""
            };

            int nextId = (await _context.Places.Select(p => (int?)p.PlIdPk).MaxAsync() ?? 0) + 1;
            newPlace.PlIdPk = nextId;

            _context.Places.Add(newPlace);
            await _context.SaveChangesAsync();
            return newPlace.PlIdPk;
        }

        private async Task SetBudget(int tripId)
        {

        }



    }
}
