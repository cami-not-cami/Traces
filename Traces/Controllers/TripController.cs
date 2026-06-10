using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Security.Claims;
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
            // 1. Session to Database Migration (if authenticated)
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userId != null)
                {
                    var sessionTripsStr = HttpContext.Session.GetString("SessionTrips");
                    if (!string.IsNullOrEmpty(sessionTripsStr))
                    {
                        try
                        {
                            var sessionTrips = JsonSerializer.Deserialize<List<int>>(sessionTripsStr);
                            if (sessionTrips != null && sessionTrips.Any())
                            {
                                var userInfo = await _context.UserInfos.FirstOrDefaultAsync(u => u.UserFk == userId);
                                if (userInfo == null)
                                {
                                    userInfo = new UserInfo
                                    {
                                        UserFk = userId,
                                        FirstName = User.Identity.Name ?? "User",
                                        LastName = ""
                                    };
                                    _context.UserInfos.Add(userInfo);
                                    await _context.SaveChangesAsync();
                                }

                                foreach (var tripIdToLink in sessionTrips)
                                {
                                    var alreadyLinked = await _context.TripMembers.AnyAsync(tm => tm.TripFk == tripIdToLink && tm.IdFk == userInfo.IdPk);
                                    if (!alreadyLinked)
                                    {
                                        var member = new TripMember
                                        {
                                            TripFk = tripIdToLink,
                                            IdFk = userInfo.IdPk
                                        };
                                        _context.TripMembers.Add(member);
                                    }
                                }
                                await _context.SaveChangesAsync();
                                HttpContext.Session.Remove("SessionTrips");
                            }
                        }
                        catch (Exception e)
                        {
                           return Content($"Error during session migration: {e.Message}");
                        }
                    }
                }
            }

            // 2. Fetch User Trips for Side Menu
            List<Trip> userTrips = new List<Trip>();
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                //same  as doing injection with usermanager
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userId != null)
                {
                    var userInfo = await _context.UserInfos.FirstOrDefaultAsync(u => u.UserFk == userId);
                    if (userInfo == null)
                    {
                        userInfo = new UserInfo
                        {
                            UserFk = userId,
                            FirstName = User.Identity.Name ?? "User",
                            LastName = ""
                        };
                        _context.UserInfos.Add(userInfo);
                        await _context.SaveChangesAsync();
                    }

                    userTrips = await _context.TripMembers
                        .Where(tm => tm.IdFk == userInfo.IdPk)
                        .Select(tm => tm.TripFkNavigation)
                        .Distinct()
                        .ToListAsync();
                }
            }
            else
            {
                var sessionTripsStr = HttpContext.Session.GetString("SessionTrips");
                if (!string.IsNullOrEmpty(sessionTripsStr))
                {
                    try
                    {
                        var sessionTrips = JsonSerializer.Deserialize<List<int>>(sessionTripsStr);
                        if (sessionTrips != null && sessionTrips.Any())
                        {
                            userTrips = await _context.Trips
                                .Where(t => sessionTrips.Contains(t.TrIdPk))
                                .ToListAsync();
                        }
                    }
                    catch (Exception e)
                    {
                        return Content($"Error during at session migration: {e.Message}");
                    }
                }
            }
            ViewBag.UserTrips = userTrips;

            if (tripId.HasValue && tripId.Value > 0)
            {
                var savedVm = await GetTripViewModelAsync(tripId.Value);
                if (savedVm != null)
                {
                    // Fetch cover photo dynamically from the database or API fallback
                    var firstPlace = savedVm.PlacesToVisit.FirstOrDefault();
                    if (firstPlace != null)
                    {
                        if (!string.IsNullOrEmpty(firstPlace.CoverPhoto))
                        {
                            ViewBag.CoverPhoto = firstPlace.CoverPhoto;
                        }
                        else if (!string.IsNullOrEmpty(firstPlace.GooglePlaceId))
                        {
                            try
                            {
                                var json = await _googlePlacesService.GetPlaceDetails(firstPlace.GooglePlaceId);
                                var googlePlace = JsonSerializer.Deserialize<GooglePlaceResponse>(json);
                                ViewBag.CoverPhoto = googlePlace?.Photos?.FirstOrDefault()?.Name;
                            }
                            catch (Exception e)
                            {
                                // Ignore API fetch errors and fallback to default
                                ViewBag.CoverPhoto = "~/default_cover_photo.jpg"; // Fallback 
                            }
                        }
                    }
                    return View(savedVm);
                }
            }

            var vm = new CreateTripViewModel();

            if (placeId != null)
            {
                // 1. Try to fetch existing place from database to avoid API call
                var existingPlace = await _context.Places
                    .Include(p => p.PlacePhotos)
                    .FirstOrDefaultAsync(p => p.GooglePlaceId == placeId);

                if (existingPlace != null)
                {
                    ViewBag.CoverPhoto = existingPlace.PlacePhotos.FirstOrDefault()?.GooglePhotoReference;

                    var place = new PlaceViewModel
                    {
                        PlaceId = existingPlace.PlIdPk,
                        GooglePlaceId = existingPlace.GooglePlaceId,
                        Name = existingPlace.Name,
                        Latitude = existingPlace.Latitude,
                        Longitude = existingPlace.Longitude,
                        FormattedAddress = existingPlace.FormattedAddress,
                        PrimaryCategory = existingPlace.PrimaryCategory,
                        City = existingPlace.City,
                        CoverPhoto = existingPlace.PlacePhotos.FirstOrDefault()?.GooglePhotoReference
                    };

                    vm = new CreateTripViewModel
                    {
                        Title = $"Trip to {place.Name}",
                        StartDate = DateOnly.TryParse(startDate, out var start1) ? start1 : null,
                        EndDate = DateOnly.TryParse(endDate, out var end1) ? end1 : null,
                        Budget = 0.0d,
                        Latitude = place.Latitude,
                        Longitude = place.Longitude,
                        PlacesToVisit = new List<PlaceViewModel> { place },
                    };
                    return View(vm);
                }

                // 2. Fallback to API call if not in database
                var json = await _googlePlacesService.GetPlaceDetails(placeId);
                var googlePlace = JsonSerializer.Deserialize<GooglePlaceResponse>(json);

                ViewBag.CoverPhoto = googlePlace.Photos?.FirstOrDefault()?.Name;

                var placeVm = new PlaceViewModel
                {
                    GooglePlaceId = placeId,
                    Name = googlePlace.DisplayName.Text,
                    Latitude = (decimal)googlePlace.Location.Latitude,
                    Longitude = (decimal)googlePlace.Location.Longitude,
                    FormattedAddress = googlePlace.FormattedAddress,
                    PrimaryCategory = "Attraction",
                    CoverPhoto = googlePlace.Photos?.FirstOrDefault()?.Name
                };

                vm = new CreateTripViewModel
                {
                    Title = $"Trip to {placeVm.Name}",
                    StartDate = DateOnly.TryParse(startDate, out var start2) ? start2 : null,
                    EndDate = DateOnly.TryParse(endDate, out var end2) ? end2 : null,
                    Budget = 0.0d,
                    Latitude = placeVm.Latitude,
                    Longitude = placeVm.Longitude,
                    PlacesToVisit = new List<PlaceViewModel> { placeVm },
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

            await LinkUserToTripAsync(trip.TrIdPk);

            return RedirectToAction("Index", new { tripId = trip.TrIdPk });
        }
        private async Task<CreateTripViewModel?> GetTripViewModelAsync(int tripId)
        {
            var trip = await _context.Trips
                .Include(t => t.TripDays)
                    .ThenInclude(d => d.TripActivities)
                        .ThenInclude(a => a.PlaceFkNavigation)
                            .ThenInclude(p => p.PlacePhotos)
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
                                PrimaryCategory = a.PlaceFkNavigation.PrimaryCategory,
                                CoverPhoto = a.PlaceFkNavigation.PlacePhotos.FirstOrDefault()?.GooglePhotoReference
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
            string? latitude,
            string? longitude,
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

                await LinkUserToTripAsync(tripId);
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

            decimal? lat = null;
            if (!string.IsNullOrEmpty(latitude))
            {
                if (decimal.TryParse(latitude, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedLat))
                {
                    lat = parsedLat;
                }
                else if (decimal.TryParse(latitude, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out parsedLat))
                {
                    lat = parsedLat;
                }
            }

            decimal? lng = null;
            if (!string.IsNullOrEmpty(longitude))
            {
                if (decimal.TryParse(longitude, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedLng))
                {
                    lng = parsedLng;
                }
                else if (decimal.TryParse(longitude, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out parsedLng))
                {
                    lng = parsedLng;
                }
            }

            var placeVm = new PlaceViewModel
            {
                GooglePlaceId = googlePlaceId,
                Name = placeName ?? "Unnamed Place",
                Latitude = lat,
                Longitude = lng,
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
            var existing = await _context.Places.Include(p => p.PlacePhotos).FirstOrDefaultAsync(p => p.GooglePlaceId == placeVm.GooglePlaceId);
            if (existing != null)
            {
                if (!existing.PlacePhotos.Any() && !string.IsNullOrEmpty(placeVm.CoverPhoto))
                {
                    var maxPhotoId = await _context.PlacePhotos.Select(p => (int?)p.PlPhIdPk).MaxAsync() ?? 0;
                    var newPhoto = new PlacePhoto
                    {
                        PlPhIdPk = maxPhotoId + 1,
                        PlacesFk = existing.PlIdPk,
                        GooglePhotoReference = placeVm.CoverPhoto
                    };
                    _context.PlacePhotos.Add(newPhoto);
                    await _context.SaveChangesAsync();
                }
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

            _context.Places.Add(newPlace);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(placeVm.CoverPhoto))
            {
                var maxPhotoId = await _context.PlacePhotos.Select(p => (int?)p.PlPhIdPk).MaxAsync() ?? 0;
                var newPhoto = new PlacePhoto
                {
                    PlPhIdPk = maxPhotoId + 1,
                    PlacesFk = newPlace.PlIdPk,
                    GooglePhotoReference = placeVm.CoverPhoto
                };
                _context.PlacePhotos.Add(newPhoto);
                await _context.SaveChangesAsync();
            }

            return newPlace.PlIdPk;
        }

        [HttpPost]
        public async Task<IActionResult> SetBudget(int tripId, double budget)
        {
            if(tripId != null && User.Identity.IsAuthenticated)
            {
                var currentTrip = await _context.Trips.FirstOrDefaultAsync(x => x.TrIdPk == tripId);
                try
                {
                    currentTrip.Budget = budget;
                    _context.Trips.Update(currentTrip);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Error setting budget: {ex.Message}");
                }
                return StatusCode(200, "Budget updated successfully");
            }
            else
            {
                return StatusCode(401, "Must be logged in to set a budget for the trip.");
            }
        }

        private async Task LinkUserToTripAsync(int tripId)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? User.FindFirst("sub")?.Value 
                          ?? User.FindFirst(ClaimTypes.Name)?.Value;
                if (userId != null)
                {
                    var userInfo = await _context.UserInfos.FirstOrDefaultAsync(u => u.UserFk == userId);
                    if (userInfo == null)
                    {
                        userInfo = new UserInfo
                        {
                            UserFk = userId,
                            FirstName = User.Identity.Name ?? "User",
                            LastName = ""
                        };
                        _context.UserInfos.Add(userInfo);
                        await _context.SaveChangesAsync();
                    }

                    var alreadyLinked = await _context.TripMembers.AnyAsync(tm => tm.TripFk == tripId && tm.IdFk == userInfo.IdPk);
                    if (!alreadyLinked)
                    {
                        var member = new TripMember
                        {
                            TripFk = tripId,
                            IdFk = userInfo.IdPk
                        };
                        _context.TripMembers.Add(member);
                        await _context.SaveChangesAsync();
                    }
                }
            }
            else
            {
                var sessionTripsStr = HttpContext.Session.GetString("SessionTrips");
                var sessionTrips = string.IsNullOrEmpty(sessionTripsStr)
                    ? new List<int>()
                    : JsonSerializer.Deserialize<List<int>>(sessionTripsStr) ?? new List<int>();

                if (!sessionTrips.Contains(tripId))
                {
                    sessionTrips.Add(tripId);
                    HttpContext.Session.SetString("SessionTrips", JsonSerializer.Serialize(sessionTrips));
                }
            }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ReorderActivities([FromBody] ReorderRequest request)
        {
            if (request == null || request.ActivityIds == null)
            {
                return BadRequest("Invalid request data.");
            }

            // Update the order index and day for each activity
            for (int i = 0; i < request.ActivityIds.Count; i++)
            {
                var activityId = request.ActivityIds[i];
                var activity = await _context.TripActivities.FindAsync(activityId);
                if (activity != null)
                {
                    activity.OrderIndex = i;
                    if (activity.TripDayFk != request.TripDayId)
                    {
                        activity.TripDayFk = request.TripDayId;
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        public class ReorderRequest
        {
            public int TripDayId { get; set; }
            public List<int> ActivityIds { get; set; }
        }
    }
}
