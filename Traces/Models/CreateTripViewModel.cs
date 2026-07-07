using System.Text.Json.Serialization;

namespace Traces.Models
{
    /// <summary>
    /// Represents the view model for creating or editing a trip,
    /// including its details, members, days, activities, notes, checklists, places to visit, and expenses.
    /// </summary>
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
        public bool ViewOnly { get; set; }

        public List<TripMemberInfo> Members { get; set; } = new();
        public List<TripDayViewModel> Days { get; set; } = new();

        public List<Note> Notes { get; set; } = new();
        public List<Checklist> Checklists { get; set; } = new();
        public List<PlaceViewModel> PlacesToVisit { get; set; } = new();
        public List<ExpenseViewModel> Expenses { get; set; } = new();
    }


    /// <summary>
    /// Represents information about a member of a trip, including their ID, user ID, and email address.
    /// </summary>
    public class TripMemberInfo
    {
        public int TripMemberId { get; set; }
        public string UserId { get; set; }
        public string Email { get; set; }
    }

    /// <summary>
    /// Represents a day in the trip, including its activities, notes, and checklists.
    /// </summary>
    public class TripDayViewModel
    {
        public int TripDayId { get; set; }
        public int DayNumber { get; set; }
        public DateOnly? Date { get; set; }
        public string DayLabel => DayNumber == 0
            ? "Unscheduled"
            : Date.HasValue
                ? Date.Value.ToString("ddd d/M")
                : $"Day {DayNumber}";

        public List<TripActivityViewModel> Activities { get; set; } = new();
        public List<TimelineItemViewModel> TimelineItems { get; set; } = new();
    }
    /// <summary>
    /// Represents an item in the trip timeline, 
    /// which can be an activity, note, or checklist, along with its order and associated details.
    /// </summary>
    public class TimelineItemViewModel
    {
        public int Id { get; set; }
        public string Type { get; set; } // "Activity", "Note", "Checklist"
        public int OrderIndex { get; set; }

        public TripActivityViewModel Activity { get; set; }
        public Note Note { get; set; }
        public Checklist Checklist { get; set; }
    }
    /// <summary>
    /// Represents an activity in the trip, including its start and end times, order, 
    /// status, associated place, and route to the next activity.
    /// </summary>
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
    }
    /// <summary>
    /// Represents the details of a place, including its ID, Google Place ID, name, coordinates, category, address, city, cover photo, and country name.
    /// </summary>
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
        public string? CoverPhoto { get; set; }
        public string? CountryName { get; set; }
    }
    /// <summary>
    /// Represents an expense in the trip, including its title, amount, category, date, payer information, associated activity, and splits among members.
    /// </summary>
    public class ExpenseViewModel
    {
        public int ExpenseId { get; set; }
        public string Title { get; set; }
        public decimal Amount { get; set; }
        public string Category { get; set; }
        public DateTime? Date { get; set; }
        public int PaidByUserInfoFk { get; set; }
        public string PaidByEmail { get; set; }
        public int? TripActivityFk { get; set; }
        public string? TripActivityName { get; set; }

        public List<ExpenseSplitViewModel> Splits { get; set; } = new();
    }
    /// <summary>
    /// Represents a split of an expense among trip members, including the user ID, email, and the amount they are responsible for.
    /// </summary>
    public class ExpenseSplitViewModel
    {
        public int ExpenseSplitId { get; set; }
        public int UserInfoFk { get; set; }
        public string UserEmail { get; set; }
        public decimal Amount { get; set; }
    }
}

