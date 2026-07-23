using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Traces.Models;

namespace Traces.Services
{
    public class TripService : ITripService
    {
        private readonly TracesDbContext _context;
        private readonly GooglePlacesService _googlePlacesService;
        private readonly GoogleMapsServices _googleMapsServices;
        private readonly ILogger<TripService> _logger;

        public TripService(
            TracesDbContext context,
            GooglePlacesService googlePlacesService,
            GoogleMapsServices googleMapsServices,
            ILogger<TripService> logger
        )
        {
            _context = context;
            _googlePlacesService = googlePlacesService;
            _googleMapsServices = googleMapsServices;
            _logger = logger;
        }
        /// <summary>
        /// calls the Google Places API to get details of a place by its placeId, including optional photos
        /// </summary>
        /// <param name="placeId"></param>
        /// <returns>json</returns>
        public async Task<GooglePlaceResponse?> GetGooglePlaceDetailsAsync(string placeId)
        {
            var json = await _googlePlacesService.GetPlaceDetails(placeId);
            if (
                string.IsNullOrEmpty(json)
                || json.StartsWith("Error calling Google Places API")
                || json.StartsWith("Place ID cannot be null")
            )
            {
                return null;
            }
            return JsonSerializer.Deserialize<GooglePlaceResponse>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }
        /// <summary>
        /// Gets the trip view model for a given tripId, including its days, activities, notes, checklists, and members.
        /// </summary>
        /// <param name="tripId"></param>
        /// <returns> createTripViewModel</returns>
        public async Task<CreateTripViewModel?> GetTripViewModelAsync(int tripId)
        {
            // load the trip with related data
            var trip = await _context
                .Trips.Include(t => t.Checklists)
                .ThenInclude(c => c.ChecklistItems)
                .Include(t => t.Notes)
                .Include(t => t.TripDays)
                .ThenInclude(d => d.TripActivities)
                .ThenInclude(a => a.PlaceFkNavigation)
                .ThenInclude(p => p.PlacePhotos)
                .Include(t => t.TripDays)
                .ThenInclude(d => d.TripActivities)
                .ThenInclude(a => a.RouteToNextFromActivityFkNavigations)
                .Include(t => t.TripDays)
                .ThenInclude(d => d.Notes)
                .Include(t => t.TripDays)
                .ThenInclude(d => d.Checklists)
                .ThenInclude(c => c.ChecklistItems)
                .FirstOrDefaultAsync(t => t.TrIdPk == tripId);

            if (trip == null)
                return null;

            //retrieve members of the trip with their user info
            var members = await _context
                .TripMembers.Where(tm => tm.TripFk == tripId)
                .Include(tm => tm.IdFkNavigation)
                .Select(tm => new TripMemberInfo
                {
                    TripMemberId = tm.IdFk,
                    UserId = tm.IdFkNavigation.UserFk,
                    Email = tm.IdFkNavigation.Email,
                })
                .ToListAsync();

            //map trip days to view models, including activities, notes, and checklists, ordered by day number and activity order index
            var dayViewModels = trip
                .TripDays.OrderBy(d => d.DayNumber)
                .Select(d =>
                {
                    var dayVm = new TripDayViewModel
                    {
                        TripDayId = d.TrDaIdPk,
                        DayNumber = d.DayNumber ?? 0,
                        Date = d.Date == DateOnly.MinValue ? (DateOnly?)null : d.Date,
                        Activities = d
                            .TripActivities.OrderBy(a => a.OrderIndex)
                            .Select(a => new TripActivityViewModel
                            {
                                TripActivityId = a.TrAcIdPk,
                                TripDayId = a.TripDayFk,
                                StartTime = a.StartTime.HasValue
                                    ? d.Date.ToDateTime(a.StartTime.Value)
                                    : null,
                                EndTime = a.EndTime.HasValue
                                    ? d.Date.ToDateTime(a.EndTime.Value)
                                    : null,
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
                                    CoverPhoto = a
                                        .PlaceFkNavigation.PlacePhotos.FirstOrDefault()
                                        ?.GooglePhotoReference,
                                },
                                RouteToNext =
                                    a.RouteToNextFromActivityFkNavigations.FirstOrDefault(),
                            })
                            .ToList(),
                    };

                    var items = new List<TimelineItemViewModel>();

                    foreach (var act in dayVm.Activities)
                    {
                        items.Add(
                            new TimelineItemViewModel
                            {
                                Id = act.TripActivityId,
                                Type = "Activity",
                                OrderIndex = act.OrderIndex,
                                Activity = act,
                            }
                        );
                    }

                    foreach (var note in d.Notes)
                    {
                        items.Add(
                            new TimelineItemViewModel
                            {
                                Id = note.NoIdPk,
                                Type = "Note",
                                OrderIndex = note.OrderIndex ?? 0,
                                Note = note,
                            }
                        );
                    }

                    foreach (var ch in d.Checklists)
                    {
                        items.Add(
                            new TimelineItemViewModel
                            {
                                Id = ch.ChIdPk,
                                Type = "Checklist",
                                OrderIndex = ch.OrderIndex,
                                Checklist = ch,
                            }
                        );
                    }

                    dayVm.TimelineItems = items
                        .OrderBy(i => i.OrderIndex)
                        .ThenBy(i => i.Id)
                        .ToList();
                    return dayVm;
                })
                .ToList();

            var allNotes = trip.Notes.ToList();

            var placesToVisit = dayViewModels
                .Where(d => d.DayNumber == 0)
                .SelectMany(d => d.Activities)
                .Select(a => a.Place)
                .GroupBy(p => p.GooglePlaceId)
                .Select(g => g.First())
                .ToList();

            var allPlaces = dayViewModels
                .SelectMany(d => d.Activities)
                .Select(a => a.Place)
                .ToList();

            var latitude = allPlaces.FirstOrDefault()?.Latitude;
            var longitude = allPlaces.FirstOrDefault()?.Longitude;

            var expenses = await GetTripExpensesAsync(trip.TrIdPk);

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
                Checklists = trip.Checklists.ToList(),
                PlacesToVisit = placesToVisit,
                Expenses = expenses,
            };
        }
        private static string TruncateString(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var trimmed = value.Trim();
            return trimmed.Length > maxLength ? trimmed.Substring(0, maxLength) : trimmed;
        }

        private static DateOnly? SanitizeDate(DateOnly? date)
        {
            if (!date.HasValue) return null;
            int maxYear = DateTime.Today.Year + 5;
            if (date.Value.Year < 1900 || date.Value.Year > maxYear) return null;
            return date;
        }

        private static DateOnly? ParseSafeDate(string? dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr)) return null;
            int maxYear = DateTime.Today.Year + 5;
            if (DateOnly.TryParse(dateStr, out var d) && d.Year >= 1900 && d.Year <= maxYear)
                return d;
            return null;
        }

        private static DateTime? ParseSafeDateTime(string? dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr)) return null;
            int maxYear = DateTime.Today.Year + 5;
            if (DateTime.TryParse(dateStr, out var d) && d.Year >= 1900 && d.Year <= maxYear)
                return d;
            return null;
        }

        /// <summary>
        /// Gets or creates a place in the database based on the provided PlaceViewModel.
        /// If the place already exists (based on GooglePlaceId), it updates the cover photo if necessary. 
        /// If the place does not exist, it creates a new entry in the database.
        /// </summary>
        /// <param name="placeVm"></param>
        /// <returns>existing or new place ID</returns>
        public async Task<int> GetOrCreatePlaceAsync(PlaceViewModel placeVm)
        {
            var existing = await _context
                .Places.Include(p => p.PlacePhotos)
                .FirstOrDefaultAsync(p => p.GooglePlaceId == placeVm.GooglePlaceId);
            if (existing != null)
            {
                bool updated = false;

                if (!existing.PlacePhotos.Any() && !string.IsNullOrEmpty(placeVm.CoverPhoto))
                {
                    var newPhoto = new PlacePhoto
                    {
                        PlacesFk = existing.PlIdPk,
                        GooglePhotoReference = placeVm.CoverPhoto,
                    };
                    _context.PlacePhotos.Add(newPhoto);
                    updated = true;
                }

                if (updated)
                {
                    await _context.SaveChangesAsync();
                }
                return existing.PlIdPk;
            }

            string parsedCountry = "";
            string parsedCity = "";
            if (!string.IsNullOrEmpty(placeVm.FormattedAddress))
            {
                var parts = placeVm.FormattedAddress.Split(',').Select(p => p.Trim()).ToList();
                if (parts.Count > 0)
                {
                    parsedCountry = parts.Last();
                    if (parts.Count > 1)
                    {
                        var cityCandidate = parts[parts.Count - 2];
                        if (
                            parts.Count > 2
                            && (
                                parsedCountry.Equals("USA", StringComparison.OrdinalIgnoreCase)
                                || parsedCountry.Equals(
                                    "United States",
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                        )
                        {
                            cityCandidate = parts[parts.Count - 3];
                        }
                        parsedCity = Regex.Replace(cityCandidate, @"\b\w*\d\w*\b", "").Trim();
                        parsedCity = Regex.Replace(parsedCity, @"\s+", " ");
                    }
                }
            }

            var newPlace = new Place
            {
                GooglePlaceId = TruncateString(placeVm.GooglePlaceId, 255),
                Name = TruncateString(placeVm.Name ?? "Unnamed Place", 150),
                Latitude = placeVm.Latitude,
                Longitude = placeVm.Longitude,
                FormattedAddress = TruncateString(placeVm.FormattedAddress ?? "", 300),
                City = TruncateString(!string.IsNullOrEmpty(placeVm.City) ? placeVm.City : parsedCity, 100),
                PrimaryCategory = TruncateString(placeVm.PrimaryCategory ?? "Attraction", 20),
                CountryName = TruncateString(!string.IsNullOrEmpty(placeVm.CountryName)
                    ? placeVm.CountryName
                    : parsedCountry, 60),
            };

            _context.Places.Add(newPlace);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(placeVm.CoverPhoto))
            {
                var newPhoto = new PlacePhoto
                {
                    PlacesFk = newPlace.PlIdPk,
                    GooglePhotoReference = placeVm.CoverPhoto,
                };
                _context.PlacePhotos.Add(newPhoto);
                await _context.SaveChangesAsync();
            }

            return newPlace.PlIdPk;
        }
        /// <summary>
        /// Updates the routes between activities for a specific trip day.
        /// It calculates the routes between consecutive activities using the Google Maps API and updates the database .
        /// If there are fewer than two activities, it removes any existing routes for that day.
        /// </summary>
        /// <param name="tripDayId"></param>
        /// <returns></returns>
        public async Task UpdateRoutesForDayAsync(int tripDayId)
        {
            var activities = await _context
                .TripActivities.Include(a => a.RouteToNextFromActivityFkNavigations)
                .Include(a => a.PlaceFkNavigation)
                .Where(a => a.TripDayFk == tripDayId)
                .OrderBy(a => a.OrderIndex)
                .ToListAsync();

            //if there are fewer than 2 activities, remove any existing routes for that day since they cant be paired
            if (activities.Count < 2)
            {
                var activityIds = activities.Select(a => a.TrAcIdPk).ToList();
                var routesToDelete = await _context
                    .RouteToNexts.Where(r =>
                        activityIds.Contains(r.FromActivityFk)
                        || activityIds.Contains(r.ToActivityFk)
                    )
                    .ToListAsync();
                if (routesToDelete.Any())
                {
                    _context.RouteToNexts.RemoveRange(routesToDelete);
                    await _context.SaveChangesAsync();
                }
                return;
            }
            // create a set of current activity pairs to track which routes should exist
            //A HashSet is a data structure used to store a collection of unique items in an unordered manner. It automatically ignores duplicates 
            var currentPairs = new HashSet<(int FromId, int ToId)>();
            // iterate through the activities to create pairs of consecutive activities
            for (int i = 0; i < activities.Count - 1; i++)
            {
                var fromActivity = activities[i];
                var toActivity = activities[i + 1];
                currentPairs.Add((fromActivity.TrAcIdPk, toActivity.TrAcIdPk));
            }

            var activityDayIds = activities.Select(a => a.TrAcIdPk).ToList();
            // retrieve existing routes for the activities in the current day
            var existingRoutes = await _context
                .RouteToNexts.Where(r =>
                    activityDayIds.Contains(r.FromActivityFk)
                    || activityDayIds.Contains(r.ToActivityFk)
                )
                .ToListAsync();
            foreach (var route in existingRoutes)
            {
                if (!currentPairs.Contains((route.FromActivityFk, route.ToActivityFk)))
                {
                    _context.RouteToNexts.Remove(route);
                }
            }
            await _context.SaveChangesAsync();

            // iterate through the activities to calculate and store routes for consecutive activities
            // that don't already have a route in the database
            for (int i = 0; i < activities.Count - 1; i++)
            {
                var fromActivity = activities[i];
                var toActivity = activities[i + 1];

                var routeFromDb = existingRoutes.Any(r =>
                    r.FromActivityFk == fromActivity.TrAcIdPk
                    && r.ToActivityFk == toActivity.TrAcIdPk
                );
                if (!routeFromDb)
                {
                    var originalPlaceId = fromActivity.PlaceFkNavigation?.GooglePlaceId;
                    var destinationPlaceId = toActivity.PlaceFkNavigation?.GooglePlaceId;

                    if (!string.IsNullOrEmpty(originalPlaceId) && !string.IsNullOrEmpty(destinationPlaceId)
                    )
                    {
                        try
                        {
                            var routeInfo = await _googleMapsServices.GetDirectionsBetweenRoutes(
                                originalPlaceId,
                                destinationPlaceId,
                                "DRIVE"
                            );
                            if (routeInfo != null)
                            {
                                var route = new RouteToNext
                                {
                                    FromActivityFk = fromActivity.TrAcIdPk,
                                    ToActivityFk = toActivity.TrAcIdPk,
                                    TravelMode = "DRIVE",
                                    PolylineEncoded = routeInfo.PolylineEncoded,
                                    DurationSeconds = routeInfo.DurationSeconds,
                                    DistanceMeters = routeInfo.DistanceMeters,
                                };
                                _context.RouteToNexts.Add(route);
                                await _context.SaveChangesAsync();
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                $"Failed to calculate route between activities {fromActivity.TrAcIdPk} and {toActivity.TrAcIdPk}"
                            );
                        }
                    }
                }
            }
            await _context.SaveChangesAsync();
        }
        /// <summary>
        /// Links a user to a trip by either their userId, userEmail, or tripMemberEmail. If the user does not exist in the database, it creates a new UserInfo entry.
        /// It then checks if the user is already linked to the trip and adds them as a TripMember if they are not.
        /// </summary>
        /// <param name="tripId"></param>
        /// <param name="userId"></param>
        /// <param name="userEmail"></param>
        /// <param name="tripMemberEmail"></param>
        /// <returns></returns>
        public async Task LinkUserToTripAsync(
            int tripId,
            string? userId = null,
            string? userEmail = null,
            string tripMemberEmail = ""
        )
        {
            UserInfo userInfo = null;

            if (!string.IsNullOrEmpty(tripMemberEmail))
            {
                userInfo = await _context.UserInfos.FirstOrDefaultAsync(u =>
                    u.Email == tripMemberEmail
                );
                if (userInfo == null)
                {
                    userInfo = new UserInfo
                    {
                        UserFk = "pending_" + Guid.NewGuid().ToString(),
                        Email = tripMemberEmail,
                    };
                    _context.UserInfos.Add(userInfo);
                    await _context.SaveChangesAsync();
                }
            }
            else if (!string.IsNullOrEmpty(userId))
            {
                userInfo = await _context.UserInfos.FirstOrDefaultAsync(u => u.UserFk == userId);
                if (userInfo == null)
                {
                    if (!string.IsNullOrEmpty(userEmail))
                    {
                        userInfo = await _context.UserInfos.FirstOrDefaultAsync(u =>
                            u.Email == userEmail
                        );
                    }

                    if (userInfo == null)
                    {
                        userInfo = new UserInfo { UserFk = userId, Email = userEmail ?? "User" };
                        _context.UserInfos.Add(userInfo);
                    }
                    else
                    {
                        userInfo.UserFk = userId;
                    }
                    await _context.SaveChangesAsync();
                }
                else if (!string.IsNullOrEmpty(userEmail) && userInfo.Email != userEmail)
                {
                    userInfo.Email = userEmail;
                    await _context.SaveChangesAsync();
                }
            }

            if (userInfo != null)
            {
                var alreadyLinked = await _context.TripMembers.AnyAsync(tm =>
                    tm.TripFk == tripId && tm.IdFk == userInfo.IdPk
                );
                if (!alreadyLinked)
                {
                    var member = new TripMember { TripFk = tripId, IdFk = userInfo.IdPk };
                    _context.TripMembers.Add(member);
                    await _context.SaveChangesAsync();
                }
            }
        }
        /// <summary>
        /// Migrates session trips to a user's account.
        /// It checks if the user exists in the database based on their userId or userEmail. If the user does not exist, it creates a new UserInfo entry.
        /// Then, for each trip in the sessionTrips list, it checks if the user is already linked to that trip and adds them as a TripMember if they are not.
        /// </summary>
        /// <param name="sessionTrips"></param>
        /// <param name="userId"></param>
        /// <param name="userEmail"></param>
        /// <returns></returns>
        public async Task MigrateSessionTripsAsync(
            List<int> sessionTrips,
            string userId,
            string? userEmail
        )
        {
            var userInfo = await _context.UserInfos.FirstOrDefaultAsync(u => u.UserFk == userId);
            if (userInfo == null)
            {
                if (!string.IsNullOrEmpty(userEmail))
                {
                    userInfo = await _context.UserInfos.FirstOrDefaultAsync(u =>
                        u.Email == userEmail
                    );
                }

                if (userInfo == null)
                {
                    userInfo = new UserInfo { UserFk = userId, Email = userEmail ?? "User" };
                    _context.UserInfos.Add(userInfo);
                }
                else
                {
                    userInfo.UserFk = userId;
                }
                await _context.SaveChangesAsync();
            }

            foreach (var tripIdToLink in sessionTrips)
            {
                var alreadyLinked = await _context.TripMembers.AnyAsync(tm =>
                    tm.TripFk == tripIdToLink && tm.IdFk == userInfo.IdPk
                );
                if (!alreadyLinked)
                {
                    var member = new TripMember { TripFk = tripIdToLink, IdFk = userInfo.IdPk };
                    _context.TripMembers.Add(member);
                }
            }
            await _context.SaveChangesAsync();
        }
        /// <summary>
        /// Retrieves all trips associated with a user based on their userId or userEmail. If the user does not exist in the database, it creates a new UserInfo entry.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="userEmail"></param>
        /// <returns></returns>
        public async Task<List<Trip>> GetUserTripsAsync(string userId, string? userEmail)
        {
            var userInfo = await _context.UserInfos.FirstOrDefaultAsync(u => u.UserFk == userId);
            if (userInfo == null)
            {
                if (!string.IsNullOrEmpty(userEmail))
                {
                    userInfo = await _context.UserInfos.FirstOrDefaultAsync(u =>
                        u.Email == userEmail
                    );
                }

                if (userInfo == null)
                {
                    userInfo = new UserInfo { UserFk = userId, Email = userEmail ?? "User" };
                    _context.UserInfos.Add(userInfo);
                }
                else
                {
                    userInfo.UserFk = userId;
                }
                await _context.SaveChangesAsync();
            }

            return await _context
                .TripMembers.Where(tm => tm.IdFk == userInfo.IdPk)
                .Select(tm => tm.TripFkNavigation)
                .Distinct()
                .ToListAsync();
        }
        /// <summary>
        /// Retrieves trips based on a list of trip IDs,
        /// typically used for fetching trips stored in a user's session.
        /// </summary>
        /// <param name="tripIds"></param>
        /// <returns></returns>
        public async Task<List<Trip>> GetSessionTripsAsync(List<int> tripIds)
        {
            return await _context.Trips.Where(t => tripIds.Contains(t.TrIdPk)).ToListAsync();
        }
        /// <summary>
        /// Creates a new trip based on the provided CreateTripViewModel and links it to the user specified by userId or userEmail.
        /// </summary>
        /// <param name="model"></param>
        /// <param name="userId"></param>
        /// <param name="userEmail"></param>
        /// <returns></returns>
        public async Task<int> CreateTripAsync(
            CreateTripViewModel model,
            string? userId,
            string? userEmail
        )
        {
            var sDate = SanitizeDate(model.StartDate);
            var eDate = SanitizeDate(model.EndDate);

            var trip = new Trip
            {
                Title = TruncateString(model.Title ?? "My Trip", 50),
                Description = TruncateString(model.Description ?? "", 150),
                StartDate = sDate,
                EndDate = eDate,
                Budget = model.Budget ?? 0.0d,
            };

            _context.Trips.Add(trip);
            await _context.SaveChangesAsync();

            var day0 = new TripDay
            {
                TripFk = trip.TrIdPk,
                DayNumber = 0,
                Date = DateOnly.MinValue,
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
                        Date = currentDate,
                    };
                    _context.TripDays.Add(tripDay);

                    dayNumber++;
                    currentDate = currentDate.AddDays(1);
                }
            }

            await _context.SaveChangesAsync();

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
                        OrderIndex = 0,
                    };
                    _context.TripActivities.Add(activity);
                }
                await _context.SaveChangesAsync();
            }

            await LinkUserToTripAsync(trip.TrIdPk, userId, userEmail);

            return trip.TrIdPk;
        }
        /// <summary>
        /// adds an activity to the trip
        /// </summary>
        /// <param name="tripId"></param>
        /// <param name="tripTitle"></param>
        /// <param name="tripStartDate"></param>
        /// <param name="tripEndDate"></param>
        /// <param name="placeName"></param>
        /// <param name="googlePlaceId"></param>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <param name="formattedAddress"></param>
        /// <param name="dayNumber"></param>
        /// <param name="category"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <param name="notes"></param>
        /// <param name="userId"></param>
        /// <param name="userEmail"></param>
        /// <param name="coverPhoto"></param>
        /// <returns></returns>
        public async Task<int> AddActivityToTripAsync(
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
            string? notes,
            string? userId,
            string? userEmail,
            string? coverPhoto
        )
        {
            var sDate = ParseSafeDate(tripStartDate);
            var eDate = ParseSafeDate(tripEndDate);

            if (tripId == 0)
            {
                var trip = new Trip
                {
                    Title = TruncateString(tripTitle ?? "My Trip", 50),
                    Description = "",
                    StartDate = sDate,
                    EndDate = eDate,
                    Budget = 0.0d,
                };

                _context.Trips.Add(trip);
                await _context.SaveChangesAsync();
                tripId = trip.TrIdPk;

                var day0 = new TripDay
                {
                    TripFk = tripId,
                    DayNumber = 0,
                    Date = DateOnly.MinValue,
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
                            Date = currentDate,
                        };
                        _context.TripDays.Add(tripDay);

                        dn++;
                        currentDate = currentDate.AddDays(1);
                    }
                }
                await _context.SaveChangesAsync();

                await LinkUserToTripAsync(tripId, userId, userEmail);
            }

            var targetDay = await _context.TripDays.FirstOrDefaultAsync(d =>
                d.TripFk == tripId && d.DayNumber == dayNumber
            );

            if (targetDay == null)
            {
                var trip = await _context.Trips.FirstOrDefaultAsync(t => t.TrIdPk == tripId);
                targetDay = new TripDay
                {
                    TripFk = tripId,
                    DayNumber = dayNumber,
                    Date =
                        (trip?.StartDate != null && dayNumber > 0)
                            ? trip.StartDate.Value.AddDays(dayNumber - 1)
                            : DateOnly.MinValue,
                };
                _context.TripDays.Add(targetDay);
                await _context.SaveChangesAsync();
            }

            decimal? lat = null;
            if (
                decimal.TryParse(
                    latitude,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var parsedLat
                )
            )
            {
                lat = parsedLat;
            }

            decimal? lng = null;
            if (
                decimal.TryParse(
                    longitude,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var parsedLng
                )
            )
            {
                lng = parsedLng;
            }

            var placeVm = new PlaceViewModel
            {
                GooglePlaceId = googlePlaceId,
                Name = TruncateString(placeName ?? "Unnamed Place", 150),
                Latitude = lat,
                Longitude = lng,
                FormattedAddress = TruncateString(formattedAddress ?? "", 300),
                PrimaryCategory = TruncateString(category ?? "Attraction", 20),
                CoverPhoto = coverPhoto
            };
            int placeId = await GetOrCreatePlaceAsync(placeVm);

            int maxOrderIndex =
                await _context
                    .TripActivities.Where(a => a.TripDayFk == targetDay.TrDaIdPk)
                    .Select(a => (int?)a.OrderIndex)
                    .MaxAsync() ?? -1;
            int nextOrderIndex = maxOrderIndex + 1;

            TimeOnly? start = null;
            if (TimeOnly.TryParse(startTime, out var st))
                start = st;
            TimeOnly? end = null;
            if (TimeOnly.TryParse(endTime, out var et))
                end = et;

            var place = await _context.Places.FindAsync(placeId);
            var activity = new TripActivity
            {
                TripDayFk = targetDay.TrDaIdPk,
                PlaceFk = placeId,
                PlaceFkNavigation = place,
                StartTime = start,
                EndTime = end,
                OrderIndex = nextOrderIndex,
                Status = TruncateString(category ?? "Attraction", 20),
            };

            _context.TripActivities.Add(activity);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(notes))
            {
                int noteOrder = await GetNextOrderIndexForDay(targetDay.TrDaIdPk);
                var note = new Note
                {
                    TripFk = tripId,
                    TripDayFk = targetDay.TrDaIdPk,
                    Content = TruncateString(notes, 500),
                    OrderIndex = noteOrder,
                };
                _context.Notes.Add(note);
                await _context.SaveChangesAsync();
            }

            await UpdateRoutesForDayAsync(targetDay.TrDaIdPk);

            return tripId;
        }
        /// <summary>
        /// sets the budget and saves it to the db
        /// </summary>
        /// <param name="tripId"></param>
        /// <param name="budget"></param>
        /// <returns></returns>
        public async Task SetBudgetAsync(int tripId, double budget)
        {
            var currentTrip = await _context.Trips.FirstOrDefaultAsync(x => x.TrIdPk == tripId);
            if (currentTrip != null)
            {
                currentTrip.Budget = budget;
                _context.Trips.Update(currentTrip);
                await _context.SaveChangesAsync();
            }
        }
        /// <summary>
        /// Updates the main details of a trip (title, description, dates, and primary location/place).
        /// If dates are changed, it shifts existing days, adds new ones, or moves  elements from removed days to Day 0.
        /// If the primary place changes, existing activities referencing the old place are cleaned up.
        /// </summary>
        /// <param name="tripId">The primary key of the trip being updated.</param>
        /// <param name="title">The new title of the trip, if any.</param>
        /// <param name="description">The new description of the trip, if any.</param>
        /// <param name="startDate">The new start date string, parsed to DateOnly.</param>
        /// <param name="endDate">The new end date string, parsed to DateOnly.</param>
        /// <param name="placeName">The new primary place name, if any.</param>
        /// <param name="googlePlaceId">The new Google Place ID, if any.</param>
        /// <param name="latitude">The new latitude string, parsed to decimal.</param>
        /// <param name="longitude">The new longitude string, parsed to decimal.</param>
        /// <param name="address">The new formatted address string, if any.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the trip with the specified ID is not found.</exception>
        public async Task UpdateTripDetailsAsync(
            int tripId,
            string? title,
            string? description,
            string? startDate,
            string? endDate,
            string? placeName,
            string? googlePlaceId,
            string? latitude,
            string? longitude,
            string? address
        )
        {
            // 1. Fetch the trip  from the database.
            var trip = await _context.Trips.FirstOrDefaultAsync(t => t.TrIdPk == tripId);
            if (trip == null)
                throw new KeyNotFoundException("Trip not found");

            // Update basic text properties if new values are provided.
            if (!string.IsNullOrEmpty(title))
                trip.Title = TruncateString(title, 50);
            if (!string.IsNullOrEmpty(description))
                trip.Description = TruncateString(description, 150);

            // 2. Parse input start and end dates.
            DateOnly? sDate = ParseSafeDate(startDate);
            DateOnly? eDate = ParseSafeDate(endDate);

            // Check if there is any change in the trip's date range.
            bool datesChanged = false;
            if (sDate != trip.StartDate || eDate != trip.EndDate)
            {
                datesChanged = true;
                trip.StartDate = sDate;
                trip.EndDate = eDate;
            }

            // 3. Handle restructuring of trip days if the dates changed.
            if (datesChanged)
            {
                var existingDays = await _context
                    .TripDays.Where(d => d.TripFk == trip.TrIdPk)
                    .ToListAsync();

                // Ensure Day 0 (reservoir for unscheduled activities, notes, checklists) exists.
                var day0 = existingDays.FirstOrDefault(d => d.DayNumber == 0);
                if (day0 == null)
                {
                    day0 = new TripDay
                    {
                        TripFk = trip.TrIdPk,
                        DayNumber = 0,
                        Date = DateOnly.MinValue,
                    };
                    _context.TripDays.Add(day0);
                    await _context.SaveChangesAsync();
                }

                // Filter out the scheduled active days.
                var activeDays = existingDays
                    .Where(d => d.DayNumber > 0)
                    .OrderBy(d => d.DayNumber)
                    .ToList();

                // Generate the list of target dates for the new range.
                var targetDates = new List<DateOnly>();
                if (
                    trip.StartDate.HasValue
                    && trip.EndDate.HasValue
                    && trip.StartDate.Value <= trip.EndDate.Value
                )
                {
                    var current = trip.StartDate.Value;
                    while (current <= trip.EndDate.Value)
                    {
                        targetDates.Add(current);
                        current = current.AddDays(1);
                    }
                }

                int targetCount = targetDates.Count;

                // Re-align or create days to match the target dates.
                for (int i = 0; i < targetCount; i++)
                {
                    int dayNum = i + 1;
                    var dateVal = targetDates[i];

                    if (i < activeDays.Count)
                    {
                        // Update existing day's date and number.
                        var dayToUpdate = activeDays[i];
                        dayToUpdate.DayNumber = dayNum;
                        dayToUpdate.Date = dateVal;
                        _context.TripDays.Update(dayToUpdate);
                    }
                    else
                    {
                        // Create a new day if the range expanded.
                        var newDay = new TripDay
                        {
                            TripFk = trip.TrIdPk,
                            DayNumber = dayNum,
                            Date = dateVal,
                        };
                        _context.TripDays.Add(newDay);
                    }
                }

                // If the date range shrank, handle the excess/deleted days.
                if (activeDays.Count > targetCount)
                {
                    var excessDays = activeDays.Skip(targetCount).ToList();
                    foreach (var extraDay in excessDays)
                    {
                        // A. Move activities from the removed day to Day 0.
                        var activitiesToMove = await _context
                            .TripActivities.Where(a => a.TripDayFk == extraDay.TrDaIdPk)
                            .ToListAsync();
                        if (activitiesToMove.Any())
                        {
                            var activityIds = activitiesToMove.Select(a => a.TrAcIdPk).ToList();
                            
                            // Delete travel routes associated with these activities since their order is changing.
                            var routesToDelete = await _context
                                .RouteToNexts.Where(r =>
                                    activityIds.Contains(r.FromActivityFk)
                                    || activityIds.Contains(r.ToActivityFk)
                                )
                                .ToListAsync();
                            if (routesToDelete.Any())
                            {
                                _context.RouteToNexts.RemoveRange(routesToDelete);
                            }

                            // Calculate next available order index in Day 0.
                            int nextOrderIndex =
                                (
                                    await _context
                                        .TripActivities.Where(a => a.TripDayFk == day0.TrDaIdPk)
                                        .Select(a => (int?)a.OrderIndex)
                                        .MaxAsync() ?? -1
                                ) + 1;

                            // Reassign activities to Day 0.
                            foreach (var act in activitiesToMove)
                            {
                                act.TripDayFk = day0.TrDaIdPk;
                                act.OrderIndex = nextOrderIndex++;
                            }
                        }

                        // B. Move notes from the removed day to Day 0.
                        var notesToMove = await _context
                            .Notes.Where(n => n.TripDayFk == extraDay.TrDaIdPk)
                            .ToListAsync();
                        if (notesToMove.Any())
                        {
                            int nextNoteOrder =
                                (
                                    await _context
                                        .Notes.Where(n => n.TripDayFk == day0.TrDaIdPk)
                                        .Select(n => n.OrderIndex)
                                        .MaxAsync() ?? -1
                                ) + 1;

                            foreach (var note in notesToMove)
                            {
                                note.TripDayFk = day0.TrDaIdPk;
                                note.OrderIndex = nextNoteOrder++;
                            }
                        }

                        // C. Move checklists from the removed day to Day 0.
                        var checklistsToMove = await _context
                            .Checklists.Where(c => c.TripDayFk == extraDay.TrDaIdPk)
                            .ToListAsync();
                        if (checklistsToMove.Any())
                        {
                            int nextChecklistOrder =
                                (
                                    await _context
                                        .Checklists.Where(c => c.TripDayFk == day0.TrDaIdPk)
                                        .Select(c => (int?)c.OrderIndex)
                                        .MaxAsync() ?? -1
                                ) + 1;

                            foreach (var checklist in checklistsToMove)
                            {
                                checklist.TripDayFk = day0.TrDaIdPk;
                                checklist.OrderIndex = nextChecklistOrder++;
                            }
                        }

                        // Remove the excess Day entity.
                        _context.TripDays.Remove(extraDay);
                    }
                }
            }

            // 4. Update the primary location/place details if provided.
            if (
                !string.IsNullOrEmpty(placeName)
                || !string.IsNullOrEmpty(googlePlaceId)
                || !string.IsNullOrEmpty(latitude)
                || !string.IsNullOrEmpty(longitude)
            )
            {
                // Parse coordinates safely.
                decimal? lat = null;
                if (!string.IsNullOrEmpty(latitude))
                {
                    if (
                        decimal.TryParse(
                            latitude,
                            NumberStyles.Any,
                            CultureInfo.InvariantCulture,
                            out var parsedLat
                        )
                    )
                    {
                        lat = parsedLat;
                    }
                }
                decimal? lng = null;
                if (!string.IsNullOrEmpty(longitude))
                {
                    if (
                        decimal.TryParse(
                            longitude,
                            NumberStyles.Any,
                            CultureInfo.InvariantCulture,
                            out var parsedLng
                        )
                    )
                    {
                        lng = parsedLng;
                    }
                }

                // Create or fetch the Place entity.
                var placeVm = new PlaceViewModel
                {
                    GooglePlaceId = googlePlaceId,
                    Name = placeName ?? "Unnamed Place",
                    Latitude = lat,
                    Longitude = lng,
                    FormattedAddress = address,
                    PrimaryCategory = "Attraction",
                };
                int placeId = await GetOrCreatePlaceAsync(placeVm);

                // Fetch or initialize Day 0.
                var day0 = await _context
                    .TripDays.Include(d => d.TripActivities)
                    .FirstOrDefaultAsync(d => d.TripFk == tripId && d.DayNumber == 0);
                if (day0 == null)
                {
                    day0 = new TripDay
                    {
                        TripFk = tripId,
                        DayNumber = 0,
                        Date = DateOnly.MinValue,
                    };
                    _context.TripDays.Add(day0);
                    await _context.SaveChangesAsync();
                }

                // Identify the existing primary activity.
                var oldPrimaryActivity = day0.TripActivities.FirstOrDefault(a => a.OrderIndex == 0);
                int? oldPlaceId = oldPrimaryActivity?.PlaceFk;

                // If the primary place has changed, clean up references to the old place.
                if (oldPlaceId.HasValue && oldPlaceId.Value != placeId)
                {
                    var activitiesToDelete = await _context
                        .TripActivities.Where(a =>
                            a.TripDayFkNavigation.TripFk == tripId && a.PlaceFk == oldPlaceId.Value
                        )
                        .ToListAsync();

                    if (activitiesToDelete.Any())
                    {
                        var activityIds = activitiesToDelete.Select(a => a.TrAcIdPk).ToList();
                        var dayIdsToUpdate = activitiesToDelete
                            .Select(a => a.TripDayFk)
                            .Distinct()
                            .ToList();

                        // Remove routes involving deleted activities.
                        var routes = await _context
                            .RouteToNexts.Where(r =>
                                activityIds.Contains(r.FromActivityFk)
                                || activityIds.Contains(r.ToActivityFk)
                            )
                            .ToListAsync();
                        _context.RouteToNexts.RemoveRange(routes);

                        // Disassociate expenses linked to deleted activities.
                        var affectedExpenses = await _context.Expenses
                            .Where(e => e.TripActivityFk.HasValue && activityIds.Contains(e.TripActivityFk.Value))
                            .ToListAsync();
                        foreach (var expense in affectedExpenses)
                        {
                            expense.TripActivityFk = null;
                        }

                        // Remove activities and update routes for impacted days.
                        _context.TripActivities.RemoveRange(activitiesToDelete);
                        await _context.SaveChangesAsync();

                        foreach (var dayId in dayIdsToUpdate)
                        {
                            await UpdateRoutesForDayAsync(dayId);
                        }
                    }
                }

                // Add or assign the new primary activity at OrderIndex = 0.
                var existingActivity = await _context.TripActivities.FirstOrDefaultAsync(a =>
                    a.TripDayFk == day0.TrDaIdPk && a.PlaceFk == placeId
                );
                if (existingActivity == null)
                {
                    var activity = new TripActivity
                    {
                        TripDayFk = day0.TrDaIdPk,
                        PlaceFk = placeId,
                        Status = "Attraction",
                        OrderIndex = 0,
                    };
                    _context.TripActivities.Add(activity);
                }
                else
                {
                    existingActivity.OrderIndex = 0;
                }
            }

            // Save all trip modifications to the database.
            _context.Trips.Update(trip);
            await _context.SaveChangesAsync();
        }
        /// <summary>
        /// Reorders activities for a specific trip day based on the provided list of activity IDs.
        /// </summary>
        /// <param name="tripDayId"></param>
        /// <param name="activityIds"></param>
        /// <returns></returns>
        public async Task ReorderActivitiesAsync(int tripDayId, List<int> activityIds)
        {
            var affectedDayIds = new HashSet<int> { tripDayId };

            for (int i = 0; i < activityIds.Count; i++)
            {
                var activityId = activityIds[i];
                var activity = await _context.TripActivities.FindAsync(activityId);
                if (activity != null)
                {
                    activity.OrderIndex = i;
                    if (activity.TripDayFk != tripDayId)
                    {
                        affectedDayIds.Add(activity.TripDayFk);
                        activity.TripDayFk = tripDayId;
                    }
                }
            }

            await _context.SaveChangesAsync();

            foreach (var dayId in affectedDayIds)
            {
                await UpdateRoutesForDayAsync(dayId);
            }
        }
        /// <summary>
        /// Adds a note to a specific trip day, determining the next order index for the note and saving it to the database.
        /// </summary>
        /// <param name="tripId"></param>
        /// <param name="tripDayId"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public async Task<int> AddNoteToDayAsync(int tripId, int tripDayId, string content)
        {
            int maxOrder = await GetNextOrderIndexForDay(tripDayId);
            var note = new Note
            {
                TripFk = tripId,
                TripDayFk = tripDayId,
                Content = TruncateString(content, 500),
                OrderIndex = maxOrder,
            };
            _context.Notes.Add(note);
            await _context.SaveChangesAsync();
            return note.NoIdPk;
        }
        /// <summary>
        /// Adds a checklist to a specific trip day, determining the next order index for the checklist and saving it to the database.
        /// </summary>
        /// <param name="tripId"></param>
        /// <param name="tripDayId"></param>
        /// <param name="title"></param>
        /// <returns></returns>
        public async Task<int> AddChecklistToDayAsync(int tripId, int tripDayId, string title)
        {
            int maxOrder = await GetNextOrderIndexForDay(tripDayId);
            var checklist = new Checklist
            {
                TripFk = tripId,
                TripDayFk = tripDayId,
                Title = TruncateString(title, 100),
                OrderIndex = maxOrder,
            };
            _context.Checklists.Add(checklist);
            await _context.SaveChangesAsync();
            return checklist.ChIdPk;
        }
        /// <summary>
        /// Adds an item to a specific checklist, determining the next order index for the item and saving it to the database.
        /// </summary>
        /// <param name="checklistId"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public async Task<int> AddChecklistItemAsync(int checklistId, string content)
        {
            int maxOrder =
                await _context
                    .ChecklistItems.Where(ci => ci.ChecklistFk == checklistId)
                    .Select(ci => (int?)ci.OrderIndex)
                    .MaxAsync() ?? -1;

            var item = new ChecklistItem
            {
                ChecklistFk = checklistId,
                Content = TruncateString(content, 150),
                IsCompleted = false,
                OrderIndex = maxOrder + 1,
            };
            _context.ChecklistItems.Add(item);
            await _context.SaveChangesAsync();
            return item.ChItIdPk;
        }
        /// <summary>
        /// Toggles the completion status of a specific checklist item.
        /// If the item is found, its IsCompleted property is flipped and saved to the database. 
        /// </summary>
        /// <param name="itemId"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        public async Task<bool> ToggleChecklistItemAsync(int itemId)
        {
            var item = await _context.ChecklistItems.FindAsync(itemId);
            if (item == null)
                throw new KeyNotFoundException("Item not found");
            item.IsCompleted = !item.IsCompleted;
            await _context.SaveChangesAsync();
            return item.IsCompleted;
        }
        /// <summary>
        /// Deletes a timeline item (Activity, Note, or Checklist) based on its ID and type.
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public async Task DeleteTimelineItemAsync(int itemId, string type)
        {
            if (type == "Activity")
            {
                var act = await _context.TripActivities.FindAsync(itemId);
                if (act != null)
                {
                    var routes = await _context
                        .RouteToNexts.Where(r =>
                            r.FromActivityFk == itemId || r.ToActivityFk == itemId
                        )
                        .ToListAsync();
                    _context.RouteToNexts.RemoveRange(routes);

                    var affectedExpenses = await _context.Expenses
                        .Where(e => e.TripActivityFk == itemId)
                        .ToListAsync();
                    foreach (var expense in affectedExpenses)
                    {
                        expense.TripActivityFk = null;
                    }

                    _context.TripActivities.Remove(act);
                    await _context.SaveChangesAsync();
                    await UpdateRoutesForDayAsync(act.TripDayFk);
                }
            }
            else if (type == "Note")
            {
                var note = await _context.Notes.FindAsync(itemId);
                if (note != null)
                {
                    _context.Notes.Remove(note);
                    await _context.SaveChangesAsync();
                }
            }
            else if (type == "Checklist")
            {
                var ch = await _context.Checklists.FindAsync(itemId);
                if (ch != null)
                {
                    _context.Checklists.Remove(ch);
                    await _context.SaveChangesAsync();
                }
            }
        }
        /// <summary>
        /// Reorders timeline items (Activities, Notes, and Checklists) for a specific trip day based on the provided list of ReorderTimelineItemDto objects.
        /// </summary>
        /// <param name="tripDayId"></param>
        /// <param name="items"></param>
        /// <returns></returns>
        public async Task ReorderTimelineItemsAsync(
            int tripDayId,
            List<ReorderTimelineItemDto> items
        )
        {
            var affectedDayIds = new HashSet<int> { tripDayId };

            for (int i = 0; i < items.Count; i++)
            {
                var itemReq = items[i];
                if (itemReq.Type == "Activity")
                {
                    var entity = await _context.TripActivities.FindAsync(itemReq.Id);
                    if (entity != null)
                    {
                        entity.OrderIndex = i;
                        if (entity.TripDayFk != tripDayId)
                        {
                            affectedDayIds.Add(entity.TripDayFk);
                            entity.TripDayFk = tripDayId;
                        }
                    }
                }
                else if (itemReq.Type == "Note")
                {
                    var entity = await _context.Notes.FindAsync(itemReq.Id);
                    if (entity != null)
                    {
                        entity.OrderIndex = i;
                        if (entity.TripDayFk != tripDayId)
                        {
                            if (entity.TripDayFk.HasValue)
                                affectedDayIds.Add(entity.TripDayFk.Value);
                            entity.TripDayFk = tripDayId;
                        }
                    }
                }
                else if (itemReq.Type == "Checklist")
                {
                    var entity = await _context.Checklists.FindAsync(itemReq.Id);
                    if (entity != null)
                    {
                        entity.OrderIndex = i;
                        if (entity.TripDayFk != tripDayId)
                        {
                            if (entity.TripDayFk.HasValue)
                                affectedDayIds.Add(entity.TripDayFk.Value);
                            entity.TripDayFk = tripDayId;
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();

            foreach (var dayId in affectedDayIds)
            {
                await UpdateRoutesForDayAsync(dayId);
            }
        }
        /// <summary>
        /// Calculates the next order index for a specific trip day by finding the maximum order index among activities, notes, and checklists,
        /// and returning the next available index.
        /// </summary>
        /// <param name="tripDayId"></param>
        /// <returns></returns>
        public async Task<int> GetNextOrderIndexForDay(int tripDayId)
        {
            int maxAct =
                await _context
                    .TripActivities.Where(a => a.TripDayFk == tripDayId)
                    .Select(a => (int?)a.OrderIndex)
                    .MaxAsync() ?? -1;
            int maxNote =
                await _context
                    .Notes.Where(n => n.TripDayFk == tripDayId)
                    .Select(n => n.OrderIndex)
                    .MaxAsync() ?? -1;
            int maxCh =
                await _context
                    .Checklists.Where(c => c.TripDayFk == tripDayId)
                    .Select(c => (int?)c.OrderIndex)
                    .MaxAsync() ?? -1;
            return Math.Max(maxAct, Math.Max(maxNote, maxCh)) + 1;
        }
        /// <summary>
        /// Deletes a trip and all its associated data, including trip days, trip members, activities, routes, checklists, checklist items, notes, and expenses.
        /// </summary>
        /// <param name="tripId"></param>
        /// <returns></returns>
        public async Task DeleteTripAsync(int tripId)
        {
            var trip = await _context
                .Trips.Include(t => t.TripDays)
                .Include(t => t.TripMembers)
                .FirstOrDefaultAsync(t => t.TrIdPk == tripId);
            if (trip == null)
                return;

            var dayIds = trip.TripDays.Select(d => d.TrDaIdPk).ToList();
            var activityIds = await _context
                .TripActivities.Where(a => dayIds.Contains(a.TripDayFk))
                .Select(a => a.TrAcIdPk)
                .ToListAsync();

            var routes = await _context
                .RouteToNexts.Where(r =>
                    activityIds.Contains(r.FromActivityFk) || activityIds.Contains(r.ToActivityFk)
                )
                .ToListAsync();
            _context.RouteToNexts.RemoveRange(routes);

            var checklists = await _context
                .Checklists.Where(c => c.TripFk == tripId || dayIds.Contains(c.TripDayFk ?? 0))
                .ToListAsync();
            var checklistIds = checklists.Select(c => c.ChIdPk).ToList();

            var checklistItems = await _context
                .ChecklistItems.Where(ci => checklistIds.Contains(ci.ChecklistFk))
                .ToListAsync();
            _context.ChecklistItems.RemoveRange(checklistItems);
            _context.Checklists.RemoveRange(checklists);

            var notes = await _context
                .Notes.Where(n => n.TripFk == tripId || dayIds.Contains(n.TripDayFk ?? 0))
                .ToListAsync();
            _context.Notes.RemoveRange(notes);

            var expenses = await _context
                .Expenses.Include(e => e.ExpenseSplits)
                .Where(e => e.TripFk == tripId)
                .ToListAsync();
            var splits = expenses.SelectMany(e => e.ExpenseSplits).ToList();
            _context.ExpenseSplits.RemoveRange(splits);
            _context.Expenses.RemoveRange(expenses);

            var activities = await _context
                .TripActivities.Where(a => dayIds.Contains(a.TripDayFk))
                .ToListAsync();
            _context.TripActivities.RemoveRange(activities);

            _context.TripDays.RemoveRange(trip.TripDays);
            _context.TripMembers.RemoveRange(trip.TripMembers);
            _context.Trips.Remove(trip);

            await _context.SaveChangesAsync();
        }
        /// <summary>
        /// Removes a member from a specific trip by deleting the corresponding TripMember entry from the database.
        /// </summary>
        /// <param name="tripId"></param>
        /// <param name="memberId"></param>
        /// <returns></returns>
        public async Task RemoveTripMemberAsync(int tripId, int memberId)
        {
            var member = await _context.TripMembers.FirstOrDefaultAsync(tm =>
                tm.TripFk == tripId && tm.IdFk == memberId
            );
            if (member != null)
            {
                _context.TripMembers.Remove(member);
                await _context.SaveChangesAsync();
            }
        }
        /// <summary>
        /// Updates the travel mode for a specific route between two activities, fetching new route information from Google Maps Services and saving it to the database.
        /// </summary>
        /// <param name="fromActivityId"></param>
        /// <param name="toActivityId"></param>
        /// <param name="travelMode"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="Exception"></exception>
        public async Task UpdateRouteTravelModeAsync(
            int fromActivityId,
            int toActivityId,
            string travelMode
        )
        {
            var route = await _context.RouteToNexts.FirstOrDefaultAsync(r =>
                r.FromActivityFk == fromActivityId && r.ToActivityFk == toActivityId
            );

            if (route == null)
            {
                throw new KeyNotFoundException("Route not found");
            }

            var fromActivity = await _context
                .TripActivities.Include(a => a.PlaceFkNavigation)
                .FirstOrDefaultAsync(a => a.TrAcIdPk == fromActivityId);

            var toActivity = await _context
                .TripActivities.Include(a => a.PlaceFkNavigation)
                .FirstOrDefaultAsync(a => a.TrAcIdPk == toActivityId);

            if (fromActivity == null || toActivity == null)
            {
                throw new KeyNotFoundException("Activities associated with the route not found");
            }

            var originalPlaceId = fromActivity.PlaceFkNavigation?.GooglePlaceId;
            var destinationPlaceId = toActivity.PlaceFkNavigation?.GooglePlaceId;

            if (!string.IsNullOrEmpty(originalPlaceId) && !string.IsNullOrEmpty(destinationPlaceId))
            {
                var routeInfo = await _googleMapsServices.GetDirectionsBetweenRoutes(
                    originalPlaceId,
                    destinationPlaceId,
                    travelMode
                );
                if (routeInfo != null)
                {
                    route.TravelMode = travelMode;
                    route.PolylineEncoded = routeInfo.PolylineEncoded;
                    route.DurationSeconds = routeInfo.DurationSeconds;
                    route.DistanceMeters = routeInfo.DistanceMeters;

                    _context.RouteToNexts.Update(route);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    throw new Exception("Failed to fetch routes from Google Maps Services");
                }
            }
        }
        /// <summary>
        /// Retrieves all expenses associated with a specific trip, including details about the user who paid, the related trip activity, and any expense splits
        /// . The results are returned as a list of ExpenseViewModel objects.
        /// </summary>
        /// <param name="tripId"></param>
        /// <returns></returns>
        public async Task<List<ExpenseViewModel>> GetTripExpensesAsync(int tripId)
        {
            var expenses = await _context
                .Expenses.Where(e => e.TripFk == tripId)
                .Include(e => e.PaidByUserInfoFkNavigation)
                .Include(e => e.TripActivityFkNavigation)
                .ThenInclude(ta => ta.PlaceFkNavigation)
                .Include(e => e.ExpenseSplits)
                .ToListAsync();

            var userInfos = await _context.UserInfos.ToDictionaryAsync(u => u.IdPk);

            return expenses
                .Select(e => new ExpenseViewModel
                {
                    ExpenseId = e.ExIdPk,
                    Title = e.Title,
                    Amount = e.Amount,
                    Category = e.Category,
                    Date = e.Date,
                    PaidByUserInfoFk = e.PaidByUserInfoFk,
                    PaidByEmail = e.PaidByUserInfoFkNavigation?.Email ?? "",
                    TripActivityFk = e.TripActivityFk,
                    TripActivityName = e.TripActivityFkNavigation?.PlaceFkNavigation?.Name,
                    Splits = e
                        .ExpenseSplits.Select(s => new ExpenseSplitViewModel
                        {
                            ExpenseSplitId = s.ExSpIdPk,
                            UserInfoFk = s.UserInfoFk,
                            UserEmail = userInfos.TryGetValue(s.UserInfoFk, out var u) ? (u.Email ?? "") : "",
                            Amount = s.Amount,
                        })
                        .ToList(),
                })
                .ToList();
        }
        /// <summary>
        /// Adds a new expense to a specific trip, calculates the expense splits based on the provided split type, and saves the expense and its splits to the database.
        /// </summary>
        /// <param name="tripId"></param>
        /// <param name="title"></param>
        /// <param name="amount"></param>
        /// <param name="category"></param>
        /// <param name="date"></param>
        /// <param name="paidByUserIdPk"></param>
        /// <param name="splitType"></param>
        /// <param name="tripActivityFk"></param>
        /// <returns></returns>
        public async Task<int> AddExpenseAsync(
            int tripId,
            string title,
            decimal amount,
            string category,
            string? date,
            int paidByUserIdPk,
            string splitType,
            int? tripActivityFk
        )
        {
            DateTime? parsedDate = ParseSafeDateTime(date);

            var expense = new Expense
            {
                TripFk = tripId,
                Title = TruncateString(title, 100),
                Amount = amount,
                Category = TruncateString(category, 50),
                Date = parsedDate,
                PaidByUserInfoFk = paidByUserIdPk,
                TripActivityFk = tripActivityFk,
            };

            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync();

            var splits = new List<ExpenseSplit>();
            if (splitType == "Split equally")
            {
                var members = await _context
                    .TripMembers.Where(tm => tm.TripFk == tripId)
                    .Select(tm => tm.IdFk)
                    .ToListAsync();

                if (members.Count > 0)
                {
                    decimal splitAmount = amount / members.Count;
                    decimal sumShares = 0;
                    for (int i = 0; i < members.Count; i++)
                    {
                        var share =
                            (i == members.Count - 1)
                                ? (amount - sumShares)
                                : Math.Round(splitAmount, 2, MidpointRounding.AwayFromZero);
                        sumShares += share;

                        splits.Add(
                            new ExpenseSplit
                            {
                                ExpenseFk = expense.ExIdPk,
                                UserInfoFk = members[i],
                                Amount = share,
                            }
                        );
                    }
                }
                else
                {
                    splits.Add(
                        new ExpenseSplit
                        {
                            ExpenseFk = expense.ExIdPk,
                            UserInfoFk = paidByUserIdPk,
                            Amount = amount,
                        }
                    );
                }
            }
            else
            {
                splits.Add(
                    new ExpenseSplit
                    {
                        ExpenseFk = expense.ExIdPk,
                        UserInfoFk = paidByUserIdPk,
                        Amount = amount,
                    }
                );
            }

            _context.ExpenseSplits.AddRange(splits);
            await _context.SaveChangesAsync();

            return expense.ExIdPk;
        }
        /// <summary>
        /// Deletes an expense and its associated splits from the database based on the provided expense ID.
        /// </summary>
        /// <param name="expenseId"></param>
        /// <returns></returns>
        public async Task DeleteExpenseAsync(int expenseId)
        {
            var expense = await _context
                .Expenses.Include(e => e.ExpenseSplits)
                .FirstOrDefaultAsync(e => e.ExIdPk == expenseId);

            if (expense != null)
            {
                _context.ExpenseSplits.RemoveRange(expense.ExpenseSplits);
                _context.Expenses.Remove(expense);
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Verifies whether the registered user (member of the trip) or anonymous user (has trip in session) can access the trip.
        /// </summary>
        public async Task<bool> HasAccessToTripAsync(int tripId, string? userId, List<int>? sessionTripIds)
        {
            var tripExists = await _context.Trips.AnyAsync(t => t.TrIdPk == tripId);
            if (!tripExists)
                return false;

            // Check if the trip is claimed by registered users
            var hasMembers = await _context.TripMembers.AnyAsync(tm => tm.TripFk == tripId);

            if (hasMembers)
            {
                // If claimed, the user must be logged in and part of this trip
                if (string.IsNullOrEmpty(userId))
                    return false;

                return await _context.TripMembers.AnyAsync(tm => tm.TripFk == tripId && tm.IdFkNavigation.UserFk == userId);
            }
            else
            {
                // If unclaimed, it's a guest trip and must exist in their session
                return sessionTripIds != null && sessionTripIds.Contains(tripId);
            }
        }
    }
}
