using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Traces.Models;

namespace Traces.Services
{
    public interface ITripService
    {
        Task<GooglePlaceResponse?> GetGooglePlaceDetailsAsync(string placeId);
        Task<CreateTripViewModel?> GetTripViewModelAsync(int tripId);
        Task<int> GetOrCreatePlaceAsync(PlaceViewModel placeVm);
        Task UpdateRoutesForDayAsync(int tripDayId);
        Task LinkUserToTripAsync(int tripId, string? userId = null, string? userEmail = null, string tripMemberEmail = "");
        Task MigrateSessionTripsAsync(List<int> sessionTrips, string userId, string? userEmail);
        Task<List<Trip>> GetUserTripsAsync(string userId, string? userEmail);
        Task<List<Trip>> GetSessionTripsAsync(List<int> tripIds);
        Task<int> CreateTripAsync(CreateTripViewModel model, string? userId, string? userEmail);
        Task<int> AddActivityToTripAsync(
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
            string? userEmail);
        Task SetBudgetAsync(int tripId, double budget);
        Task UpdateTripDetailsAsync(
            int tripId,
            string? title,
            string? description,
            string? startDate,
            string? endDate,
            string? placeName,
            string? googlePlaceId,
            string? latitude,
            string? longitude,
            string? address);
        Task ReorderActivitiesAsync(int tripDayId, List<int> activityIds);
        Task<int> AddNoteToDayAsync(int tripId, int tripDayId, string content);
        Task<int> AddChecklistToDayAsync(int tripId, int tripDayId, string title);
        Task<int> AddChecklistItemAsync(int checklistId, string content);
        Task<bool> ToggleChecklistItemAsync(int itemId);
        Task DeleteTimelineItemAsync(int itemId, string type);
        Task ReorderTimelineItemsAsync(int tripDayId, List<ReorderTimelineItemDto> items);
        Task<int> GetNextOrderIndexForDay(int tripDayId);
        Task DeleteTripAsync(int tripId);
        Task RemoveTripMemberAsync(int tripId, int memberId);
        Task UpdateRouteTravelModeAsync(int fromActivityId, int toActivityId, string travelMode);
        Task GeneratePdfAsync();
        Task DownloadPdfAsync();
    }

    public class ReorderTimelineItemDto
    {
        public int Id { get; set; }
        public string Type { get; set; }
    }
}
