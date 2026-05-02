using CallLogCRM.Api.DTOs;
using CallLogCRM.Api.Models;

namespace CallLogCRM.Api.Services;

// Contract for call log operations.
public interface ICallLogService
{
    Task<CallLog>              CreateCallLogAsync(CreateCallLogDto dto, Guid userId, string? notes = null);

    /// <summary>Returns ALL call logs, newest first (admin view).</summary>
    Task<IEnumerable<CallLog>> GetAllCallLogsAsync();

    /// <summary>Returns all call logs with closer name joined, for the admin dashboard.</summary>
    Task<IEnumerable<CallLogAdminDto>> GetAdminCallLogsAsync();

    /// <summary>Returns only the logs belonging to <paramref name="userId"/>, newest first.</summary>
    Task<IEnumerable<CallLog>> GetMyCallLogsAsync(Guid userId);
}
