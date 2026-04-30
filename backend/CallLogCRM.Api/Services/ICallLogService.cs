using CallLogCRM.Api.DTOs;
using CallLogCRM.Api.Models;

namespace CallLogCRM.Api.Services;

// Contract for call log operations.
public interface ICallLogService
{
    Task<CallLog>              CreateCallLogAsync(CreateCallLogDto dto, Guid userId);

    /// <summary>Returns ALL call logs, newest first (admin view).</summary>
    Task<IEnumerable<CallLog>> GetAllCallLogsAsync();

    /// <summary>Returns only the logs belonging to <paramref name="userId"/>, newest first.</summary>
    Task<IEnumerable<CallLog>> GetMyCallLogsAsync(Guid userId);
}
