using System.Text.Json.Serialization;

namespace Traces.Models
{
    public class CreateTripViewModel
    {
     
        public int TripId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public double? Budget { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        public List<TripMemberInfo> Members { get; set; } = new();
        public List<TripDayViewModel> Days { get; set; } = new();

        public List<Note> Notes { get; set; } = new();
        public List<PlaceViewModel> PlacesToVisit { get; set; } = new();
    }

   

    public class TripMemberInfo
    {
        public int TripMemberId { get; set; }
        public string UserId { get; set; }       
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName => $"{FirstName} {LastName}";
    }

    public class TripDayViewModel
    {
        public int TripDayId { get; set; }
        public int DayNumber { get; set; }
        public DateOnly? Date { get; set; }
        public string DayLabel => Date.HasValue
            ? Date.Value.ToString("ddd d/M")
            : $"Day {DayNumber}";

        public List<TripActivityViewModel> Activities { get; set; } = new();
    }

    public class TripActivityViewModel
    {
        public int TripActivityId { get; set; }
        public int TripDayId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int OrderIndex { get; set; }
        public string Status { get; set; }
        

        public PlaceViewModel Place { get; set; }
        public RouteToNext RouteToNext { get; set; }

        public List<Checklist> ChecklistItems { get; set; } = new();
    }

    public class PlaceViewModel
    {
        public int PlaceId { get; set; }
        public string? GooglePlaceId { get; set; }
        public string Name { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string PrimaryCategory { get; set; }
        public string FormattedAddress { get; set; }
        public string City { get; set; }
    }
}

