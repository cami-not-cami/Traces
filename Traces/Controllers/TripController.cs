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
        private readonly ITripService _tripService;
        private readonly ILogger<TripController> _logger;

        public TripController(TracesDbContext context, ITripService tripService, ILogger<TripController> logger)
        {
            _context = context;
            _tripService = tripService;
            _logger = logger;
        }

        // GET: TripController
        public async Task<ActionResult> Index(int? tripId, string? placeId, string? startDate, string? endDate, string? exploreCountry = null)
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
                                 await _tripService.MigrateSessionTripsAsync(sessionTrips, userId, User.Identity.Name);
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
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userId != null)
                {
                    userTrips = await _tripService.GetUserTripsAsync(userId, User.Identity.Name);
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
                            userTrips = await _tripService.GetSessionTripsAsync(sessionTrips);
                        }
                    }
                    catch (Exception e)
                    {
                        return Content($"Error during session migration: {e.Message}");
                    }
                }
            }
            ViewBag.UserTrips = userTrips;

            if (!string.IsNullOrEmpty(exploreCountry))
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value
                          ?? User.FindFirst(ClaimTypes.Name)?.Value;
                int? currentUserIdPk = null;
                if (userId != null)
                {
                    var userInfo = await _context.UserInfos.FirstOrDefaultAsync(u => u.UserFk == userId);
                    if (userInfo != null)
                    {
                        currentUserIdPk = userInfo.IdPk;
                    }
                }

                var query = _context.TripActivities
                    .Include(a => a.PlaceFkNavigation)
                        .ThenInclude(p => p.PlacePhotos)
                    .Include(a => a.TripDayFkNavigation)
                    .Where(a => a.PlaceFkNavigation.CountryName == exploreCountry);

                if (currentUserIdPk.HasValue)
                {
                    query = query.Where(a => !_context.TripMembers.Any(tm => tm.TripFk == a.TripDayFkNavigation.TripFk && tm.IdFk == currentUserIdPk.Value));
                }

                var activitiesFromCountry = await query.ToListAsync();

                var places = activitiesFromCountry
                    .Select(a => a.PlaceFkNavigation)
                    .Where(p => p != null)
                    .GroupBy(p => p.GooglePlaceId)
                    .Select(g => g.First())
                    .ToList();

                if (places.Count == 0)
                {
                    var countryPlace = await _context.Places.Include(p => p.PlacePhotos).FirstOrDefaultAsync(p => p.CountryName == exploreCountry);
                    if (countryPlace != null)
                    {
                        places.Add(countryPlace);
                    }
                }

                var placeVms = places.Select(p => new PlaceViewModel
                {
                    PlaceId = p.PlIdPk,
                    GooglePlaceId = p.GooglePlaceId,
                    Name = p.Name,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    FormattedAddress = p.FormattedAddress,
                    PrimaryCategory = p.PrimaryCategory,
                    City = p.City,
                    CoverPhoto = p.PlacePhotos.FirstOrDefault()?.GooglePhotoReference
                }).ToList();

                var firstPlace = placeVms.FirstOrDefault();
                if (firstPlace != null)
                {
                    ViewBag.CoverPhoto = firstPlace.CoverPhoto;
                }

                var exploreVm = new CreateTripViewModel
                {
                    TripId = 0,
                    Title = $"Explore {exploreCountry}",
                    Description = $"Trip generated with ideas from other users' activities in {exploreCountry}.",
                    StartDate = null,
                    EndDate = null,
                    Budget = 0.0,
                    Latitude = firstPlace?.Latitude,
                    Longitude = firstPlace?.Longitude,
                    PlacesToVisit = placeVms,
                    ViewOnly = true
                };

                return View(exploreVm);
            }

            if (tripId.HasValue && tripId.Value > 0)
            {
                var dayIds = await _context.TripDays
                    .Where(d => d.TripFk == tripId.Value && d.DayNumber > 0)
                    .Select(d => d.TrDaIdPk)
                    .ToListAsync();

                foreach (var dayId in dayIds)
                {
                    await _tripService.UpdateRoutesForDayAsync(dayId);
                }

                var savedVm = await _tripService.GetTripViewModelAsync(tripId.Value);
                if (savedVm != null)
                {
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
                                var googlePlace = await _tripService.GetGooglePlaceDetailsAsync(firstPlace.GooglePlaceId);
                                ViewBag.CoverPhoto = googlePlace?.Photos?.FirstOrDefault()?.Name;
                            }
                            catch (Exception e)
                            {
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

                var googlePlace = await _tripService.GetGooglePlaceDetailsAsync(placeId);
                if (googlePlace != null)
                {
                    ViewBag.CoverPhoto = googlePlace.Photos?.FirstOrDefault()?.Name;

                    var placeVm = new PlaceViewModel
                    {
                        GooglePlaceId = placeId,
                        Name = googlePlace.DisplayName?.Text ?? "Unnamed Place",
                        Latitude = googlePlace.Location != null ? (decimal)googlePlace.Location.Latitude : 0,
                        Longitude = googlePlace.Location != null ? (decimal)googlePlace.Location.Longitude : 0,
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
                }

                return View(vm);
            }

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Save(CreateTripViewModel model)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst("sub")?.Value
                      ?? User.FindFirst(ClaimTypes.Name)?.Value;
            var userEmail = User.Identity?.Name;

            var tripId = await _tripService.CreateTripAsync(model, userId, userEmail);

            // Handle session if not authenticated
            if (User.Identity == null || !User.Identity.IsAuthenticated)
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

            return RedirectToAction("Index", new { tripId = tripId });
        }

        [HttpPatch]
        public async Task<IActionResult> UpdateTripDetails(
            int tripId,
            string? title,
            string? description,
            string? startDate,
            string? endDate,
            string? placeName,
            string? googlePlaceId,
            string? latitude,
            string? longitude,
            string? address)
        {
            try
            {
                await _tripService.UpdateTripDetailsAsync(
                    tripId, title, description, startDate, endDate, placeName, googlePlaceId, latitude, longitude, address);
                return StatusCode(200);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
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
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst("sub")?.Value
                      ?? User.FindFirst(ClaimTypes.Name)?.Value;
            var userEmail = User.Identity?.Name;

            var resultTripId = await _tripService.AddActivityToTripAsync(
                tripId, tripTitle, tripStartDate, tripEndDate, placeName, googlePlaceId, latitude, longitude, formattedAddress,
                dayNumber, category, startTime, endTime, notes, userId, userEmail);

            // Handle session if newly created trip and user is not authenticated
            if (tripId == 0 && (User.Identity == null || !User.Identity.IsAuthenticated))
            {
                var sessionTripsStr = HttpContext.Session.GetString("SessionTrips");
                var sessionTrips = string.IsNullOrEmpty(sessionTripsStr)
                    ? new List<int>()
                    : JsonSerializer.Deserialize<List<int>>(sessionTripsStr) ?? new List<int>();

                if (!sessionTrips.Contains(resultTripId))
                {
                    sessionTrips.Add(resultTripId);
                    HttpContext.Session.SetString("SessionTrips", JsonSerializer.Serialize(sessionTrips));
                }
            }

            return RedirectToAction("Index", new { tripId = resultTripId });
        }

        [HttpPost]
        public async Task<IActionResult> SetBudget(int tripId, double budget)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                try
                {
                    await _tripService.SetBudgetAsync(tripId, budget);
                    return StatusCode(200, "Budget updated successfully");
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Error setting budget: {ex.Message}");
                }
            }
            else
            {
                return StatusCode(401, "Must be logged in to set a budget for the trip.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> InviteTripMember(int tripId, string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest("Email is required.");
            }

            try
            {
                await _tripService.LinkUserToTripAsync(tripId, tripMemberEmail: email);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inviting trip member");
                return Json(new { success = false, message = "Could not invite user: " + ex.Message });
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

            await _tripService.ReorderActivitiesAsync(request.TripDayId, request.ActivityIds);
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> AddNoteToDay(int tripId, int tripDayId, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return BadRequest("Content is required.");
            var id = await _tripService.AddNoteToDayAsync(tripId, tripDayId, content);
            return Json(new { success = true, id = id });
        }

        [HttpPost]
        public async Task<IActionResult> AddChecklistToDay(int tripId, int tripDayId, string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return BadRequest("Title is required.");
            var id = await _tripService.AddChecklistToDayAsync(tripId, tripDayId, title);
            return Json(new { success = true, id = id });
        }

        [HttpPost]
        public async Task<IActionResult> AddChecklistItem(int checklistId, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return BadRequest("Content is required.");
            var id = await _tripService.AddChecklistItemAsync(checklistId, content);
            return Json(new { success = true, id = id });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleChecklistItem(int itemId)
        {
            try
            {
                var isCompleted = await _tripService.ToggleChecklistItemAsync(itemId);
                return Json(new { success = true, isCompleted = isCompleted });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTimelineItem(int itemId, string type)
        {
            await _tripService.DeleteTimelineItemAsync(itemId, type);
            return Json(new { success = true });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ReorderTimelineItems([FromBody] ReorderTimelineRequest request)
        {
            if (request == null || request.Items == null) return BadRequest("Invalid request.");
            var itemsDto = request.Items.Select(i => new ReorderTimelineItemDto { Id = i.Id, Type = i.Type }).ToList();
            await _tripService.ReorderTimelineItemsAsync(request.TripDayId, itemsDto);
            return Json(new { success = true });
        }
        [HttpPost]
        public async Task<IActionResult> DeleteTrip(int tripId)
        {
            try
            {
                await _tripService.DeleteTripAsync(tripId);
                // Clean session if this trip was in session
                var sessionTripsStr = HttpContext.Session.GetString("SessionTrips");
                if (!string.IsNullOrEmpty(sessionTripsStr))
                {
                    var sessionTrips = JsonSerializer.Deserialize<List<int>>(sessionTripsStr);
                    if (sessionTrips != null && sessionTrips.Contains(tripId))
                    {
                        sessionTrips.Remove(tripId);
                        HttpContext.Session.SetString("SessionTrips", JsonSerializer.Serialize(sessionTrips));
                    }
                }
                return RedirectToAction("Index", new { tripId = 0 });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting trip");
                return StatusCode(500, "Error deleting trip: " + ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveTripMember(int tripId, int memberId)
        {
            try
            {
                await _tripService.RemoveTripMemberAsync(tripId, memberId);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing trip member");
                return Json(new { success = false, message = "Could not remove member: " + ex.Message });
            }
        }
        [HttpPost]
        public async Task<IActionResult> UpdateTravelMode(int fromActivityId, int toActivityId, string travelMode)
        {
            try
            {
                await _tripService.UpdateRouteTravelModeAsync(fromActivityId, toActivityId, travelMode);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating travel mode");
                return Json(new { success = false, message = ex.Message });
            }
        }

        public class ReorderTimelineRequest
        {
            public int TripDayId { get; set; }
            public List<ReorderTimelineItem> Items { get; set; }
        }

        public class ReorderTimelineItem
        {
            public int Id { get; set; }
            public string Type { get; set; }
        }

        public class ReorderRequest
        {
            public int TripDayId { get; set; }
            public List<int> ActivityIds { get; set; }
        }
    }
}
