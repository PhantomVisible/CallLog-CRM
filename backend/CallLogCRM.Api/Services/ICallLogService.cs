using CallLogCRM.Api.DTOs;
using CallLogCRM.Api.Models;

namespace CallLogCRM.Api.Services;

// Contract for call log operations.
// Both methods are async to support database I/O without blocking the request thread.
public interface ICallLogService
{
    Task<CallLog>              CreateCallLogAsync(CreateCallLogDto dto, Guid userId);
    Task<IEnumerable<CallLog>> GetAllCallLogsAsync();
}
