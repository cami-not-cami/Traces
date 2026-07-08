using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Traces.Models;
using Traces.Services;

namespace Traces.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TracesApiController : ControllerBase
    {
        public readonly ITripService _tripService;
        public TracesApiController(ITripService tripService)
        {
            _tripService = tripService;

        }
        /// <summary>
        /// Retrieves the details of a specific trip by its ID, including its members, days, activities, notes, checklists, places to visit, and expenses.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<CreateTripViewModel>> GetTrip(int id)
        {
            var tripViewModel = await _tripService.GetTripViewModelAsync(id);
            if (tripViewModel == null)
            {
                return NotFound(new { message = $"Trip with ID {id} not found." });
            }
            return Ok(tripViewModel);
        }
        /// <summary>
        /// Retrieves the expenses associated with a specific trip by its ID,
        /// including details such as expense title, amount, category, date, payer information, and any associated trip activities.
        /// </summary>
        /// <param name="tripId"></param>
        /// <returns></returns>
        [HttpGet("expense/{tripId}")]
        public async Task<ActionResult<ExpenseViewModel>> GetTripExpenses(int tripId)
        {
            var expenseViewModel = await _tripService.GetTripExpensesAsync(tripId);
            if (expenseViewModel == null)
            {
                return NotFound(new { message = $"Trip with ID {tripId} not found." });
            }
            return Ok(expenseViewModel);
        }
    }
}
