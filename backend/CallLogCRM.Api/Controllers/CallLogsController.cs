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

        var created = await callLogService.CreateCallLogAsync(dto, userId);
        return CreatedAtAction(nameof(GetAllCallLogs), new { id = created.Id }, created);
    }

    // GET /api/calllogs
    // Returns all call logs from Postgres, ordered newest-first.
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CallLog>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAllCallLogs()
        => Ok(await callLogService.GetAllCallLogsAsync());
}
