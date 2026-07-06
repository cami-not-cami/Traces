using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
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

        public async Task<CreateTripViewModel?> GetTripViewModelAsync(int tripId)
        {
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
                GooglePlaceId = placeVm.GooglePlaceId ?? Guid.NewGuid().ToString(),
                Name = placeVm.Name ?? "Unnamed Place",
                Latitude = placeVm.Latitude,
                Longitude = placeVm.Longitude,
                FormattedAddress = placeVm.FormattedAddress ?? "",
                City = !string.IsNullOrEmpty(placeVm.City) ? placeVm.City : parsedCity,
                PrimaryCategory = placeVm.PrimaryCategory ?? "Attraction",
                CountryName = !string.IsNullOrEmpty(placeVm.CountryName)
                    ? placeVm.CountryName
                    : parsedCountry,
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

        public async Task UpdateRoutesForDayAsync(int tripDayId)
        {
            var activities = await _context
                .TripActivities.Include(a => a.RouteToNextFromActivityFkNavigations)
                .Include(a => a.PlaceFkNavigation)
                .Where(a => a.TripDayFk == tripDayId)
                .OrderBy(a => a.OrderIndex)
                .ToListAsync();

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

            var currentPairs = new HashSet<(int FromId, int ToId)>();
            for (int i = 0; i < activities.Count - 1; i++)
            {
                var fromActivity = activities[i];
                var toActivity = activities[i + 1];
                currentPairs.Add((fromActivity.TrAcIdPk, toActivity.TrAcIdPk));
            }

            var activityDayIds = activities.Select(a => a.TrAcIdPk).ToList();
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

                    if (
                        !string.IsNullOrEmpty(originalPlaceId)
                        && !string.IsNullOrEmpty(destinationPlaceId)
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

        public async Task<List<Trip>> GetSessionTripsAsync(List<int> tripIds)
        {
            return await _context.Trips.Where(t => tripIds.Contains(t.TrIdPk)).ToListAsync();
        }

        public async Task<int> CreateTripAsync(
            CreateTripViewModel model,
            string? userId,
            string? userEmail
        )
        {
            var trip = new Trip
            {
                Title = model.Title ?? "My Trip",
                Description = model.Description ?? "",
                StartDate = model.StartDate,
                EndDate = model.EndDate,
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
            if (tripId == 0)
            {
                var trip = new Trip
                {
                    Title = tripTitle ?? "My Trip",
                    Description = "",
                    StartDate = DateOnly.TryParse(tripStartDate, out var sDate) ? sDate : null,
                    EndDate = DateOnly.TryParse(tripEndDate, out var eDate) ? eDate : null,
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
                Name = placeName ?? "Unnamed Place",
                Latitude = lat,
                Longitude = lng,
                FormattedAddress = formattedAddress ?? "",
                PrimaryCategory = category ?? "Attraction",
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
                Status = category ?? "Attraction",
            };

            _context.TripActivities.Add(activity);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(notes))
            {
                var note = new Note { TripFk = tripId, Content = notes };
                _context.Notes.Add(note);
                await _context.SaveChangesAsync();
            }

            await UpdateRoutesForDayAsync(targetDay.TrDaIdPk);

            return tripId;
        }

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
            var trip = await _context.Trips.FirstOrDefaultAsync(t => t.TrIdPk == tripId);
            if (trip == null)
                throw new KeyNotFoundException("Trip not found");
            if (!string.IsNullOrEmpty(title))
                trip.Title = title;
            if (!string.IsNullOrEmpty(description))
                trip.Description = description;

            DateOnly? sDate = null;
            if (startDate != null && DateOnly.TryParse(startDate, out var parsedStart))
                sDate = parsedStart;

            DateOnly? eDate = null;
            if (endDate != null && DateOnly.TryParse(endDate, out var parsedEnd))
                eDate = parsedEnd;

            bool datesChanged = false;
            if (sDate != trip.StartDate || eDate != trip.EndDate)
            {
                datesChanged = true;
                trip.StartDate = sDate;
                trip.EndDate = eDate;
            }

            if (datesChanged)
            {
                var existingDays = await _context
                    .TripDays.Where(d => d.TripFk == trip.TrIdPk)
                    .ToListAsync();

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

                var activeDays = existingDays
                    .Where(d => d.DayNumber > 0)
                    .OrderBy(d => d.DayNumber)
                    .ToList();

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

                for (int i = 0; i < targetCount; i++)
                {
                    int dayNum = i + 1;
                    var dateVal = targetDates[i];

                    if (i < activeDays.Count)
                    {
                        var dayToUpdate = activeDays[i];
                        dayToUpdate.DayNumber = dayNum;
                        dayToUpdate.Date = dateVal;
                        _context.TripDays.Update(dayToUpdate);
                    }
                    else
                    {
                        var newDay = new TripDay
                        {
                            TripFk = trip.TrIdPk,
                            DayNumber = dayNum,
                            Date = dateVal,
                        };
                        _context.TripDays.Add(newDay);
                    }
                }

                if (activeDays.Count > targetCount)
                {
                    var excessDays = activeDays.Skip(targetCount).ToList();
                    foreach (var extraDay in excessDays)
                    {
                        var activitiesToMove = await _context
                            .TripActivities.Where(a => a.TripDayFk == extraDay.TrDaIdPk)
                            .ToListAsync();
                        if (activitiesToMove.Any())
                        {
                            var activityIds = activitiesToMove.Select(a => a.TrAcIdPk).ToList();
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

                            int nextOrderIndex =
                                (
                                    await _context
                                        .TripActivities.Where(a => a.TripDayFk == day0.TrDaIdPk)
                                        .Select(a => (int?)a.OrderIndex)
                                        .MaxAsync() ?? -1
                                ) + 1;

                            foreach (var act in activitiesToMove)
                            {
                                act.TripDayFk = day0.TrDaIdPk;
                                act.OrderIndex = nextOrderIndex++;
                            }
                        }

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

                        _context.TripDays.Remove(extraDay);
                    }
                }
            }

            if (
                !string.IsNullOrEmpty(placeName)
                || !string.IsNullOrEmpty(googlePlaceId)
                || !string.IsNullOrEmpty(latitude)
                || !string.IsNullOrEmpty(longitude)
            )
            {
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

                var oldPrimaryActivity = day0.TripActivities.FirstOrDefault(a => a.OrderIndex == 0);
                int? oldPlaceId = oldPrimaryActivity?.PlaceFk;

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

                        var routes = await _context
                            .RouteToNexts.Where(r =>
                                activityIds.Contains(r.FromActivityFk)
                                || activityIds.Contains(r.ToActivityFk)
                            )
                            .ToListAsync();
                        _context.RouteToNexts.RemoveRange(routes);

                        var affectedExpenses = await _context.Expenses
                            .Where(e => e.TripActivityFk.HasValue && activityIds.Contains(e.TripActivityFk.Value))
                            .ToListAsync();
                        foreach (var expense in affectedExpenses)
                        {
                            expense.TripActivityFk = null;
                        }

                        _context.TripActivities.RemoveRange(activitiesToDelete);
                        await _context.SaveChangesAsync();

                        foreach (var dayId in dayIdsToUpdate)
                        {
                            await UpdateRoutesForDayAsync(dayId);
                        }
                    }
                }

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

            _context.Trips.Update(trip);
            await _context.SaveChangesAsync();
        }

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

        public async Task<int> AddNoteToDayAsync(int tripId, int tripDayId, string content)
        {
            int maxOrder = await GetNextOrderIndexForDay(tripDayId);
            var note = new Note
            {
                TripFk = tripId,
                TripDayFk = tripDayId,
                Content = content,
                OrderIndex = maxOrder,
            };
            _context.Notes.Add(note);
            await _context.SaveChangesAsync();
            return note.NoIdPk;
        }

        public async Task<int> AddChecklistToDayAsync(int tripId, int tripDayId, string title)
        {
            int maxOrder = await GetNextOrderIndexForDay(tripDayId);
            var checklist = new Checklist
            {
                TripFk = tripId,
                TripDayFk = tripDayId,
                Title = title,
                OrderIndex = maxOrder,
            };
            _context.Checklists.Add(checklist);
            await _context.SaveChangesAsync();
            return checklist.ChIdPk;
        }

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
                Content = content,
                IsCompleted = false,
                OrderIndex = maxOrder + 1,
            };
            _context.ChecklistItems.Add(item);
            await _context.SaveChangesAsync();
            return item.ChItIdPk;
        }

        public async Task<bool> ToggleChecklistItemAsync(int itemId)
        {
            var item = await _context.ChecklistItems.FindAsync(itemId);
            if (item == null)
                throw new KeyNotFoundException("Item not found");
            item.IsCompleted = !item.IsCompleted;
            await _context.SaveChangesAsync();
            return item.IsCompleted;
        }

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

        public async Task<List<ExpenseViewModel>> GetTripExpensesAsync(int tripId)
        {
            var expenses = await _context
                .Expenses.Where(e => e.TripFk == tripId)
                .Include(e => e.PaidByUserInfoFkNavigation)
                .Include(e => e.TripActivityFkNavigation)
                .ThenInclude(ta => ta.PlaceFkNavigation)
                .Include(e => e.ExpenseSplits)
                .ToListAsync();

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
                            Amount = s.Amount,
                        })
                        .ToList(),
                })
                .ToList();
        }

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
            DateTime? parsedDate = null;
            if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var d))
            {
                parsedDate = d;
            }

            var expense = new Expense
            {
                TripFk = tripId,
                Title = title,
                Amount = amount,
                Category = category,
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
    }
}
