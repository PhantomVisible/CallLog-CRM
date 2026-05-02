using System.Security.Claims;
using CallLogCRM.Api.DTOs;
using CallLogCRM.Api.Models;
using CallLogCRM.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CallLogCRM.Api.Controllers;

[ApiController]
[Route("api/calllogs")]
[Authorize]
public class CallLogsController(ICallLogService callLogService) : ControllerBase
{
    // POST /api/calllogs
    // Persists a call log to Postgres and triggers an SMS when the outcome warrants one.
    [HttpPost]
    [ProducesResponseType(typeof(CallLog), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateCallLog([FromBody] CreateCallLogDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
            return Unauthorized();

        var created = await callLogService.CreateCallLogAsync(dto, userId, dto.Notes);
        return CreatedAtAction(nameof(GetAllCallLogs), new { id = created.Id }, created);
    }

    // GET /api/calllogs
    // Returns all call logs (admin view), ordered newest-first.
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CallLog>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAllCallLogs()
        => Ok(await callLogService.GetAllCallLogsAsync());

    // GET /api/calllogs/admin
    // Returns all logs with CloserName joined — admin dashboard only.
    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IEnumerable<CallLogAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdminCallLogs()
        => Ok(await callLogService.GetAdminCallLogsAsync());

    // PUT /api/calllogs/{id}/financials
    // Admin-only: overrides Revenue and AmountCollected on a persisted call log.
    [HttpPut("{id}/financials")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateFinancials(Guid id, [FromBody] UpdateFinancialsDto dto)
    {
        var success = await callLogService.UpdateCallLogFinancialsAsync(id, dto);
        return success ? NoContent() : NotFound();
    }

    // GET /api/calllogs/mine
    // Returns only the authenticated closer's own logs, newest-first.
    [HttpGet("mine")]
    [ProducesResponseType(typeof(IEnumerable<CallLog>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyCallLogs()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
            return Unauthorized();

        return Ok(await callLogService.GetMyCallLogsAsync(userId));
    }
}
