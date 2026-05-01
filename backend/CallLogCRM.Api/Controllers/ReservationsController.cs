using System.Security.Claims;
using CallLogCRM.Api.Data;
using CallLogCRM.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CallLogCRM.Api.Controllers;

[ApiController]
[Route("api/reservations")]
[Authorize]
public class ReservationsController(AppDbContext db) : ControllerBase
{
    // GET /api/reservations/mine
    // Returns all reservations assigned to the authenticated closer, soonest first.
    [HttpGet("mine")]
    [ProducesResponseType(typeof(IEnumerable<CallReservation>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyReservations()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
            return Unauthorized();

        var reservations = await db.CallReservations
            .Where(r => r.AssignedUserId == userId && r.CurrentStatus != "Traité")
            .OrderByDescending(r => r.AppointmentDate)
            .AsNoTracking()
            .ToListAsync();

        return Ok(reservations);
    }

    // GET /api/reservations/{id}
    // Returns a single reservation by ID (must belong to the authenticated user).
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CallReservation), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetReservation(Guid id)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
            return Unauthorized();

        var reservation = await db.CallReservations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.AssignedUserId == userId);

        return reservation is null ? NotFound() : Ok(reservation);
    }
}
