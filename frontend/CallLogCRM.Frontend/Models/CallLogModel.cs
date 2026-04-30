namespace CallLogCRM.Frontend.Models;

// Response model — mirrors the CallLog the backend returns from GET /api/calllogs.
// Outcome is kept as string because the backend serializes the CallOutcome enum as a
// string (via JsonStringEnumConverter). This avoids keeping a duplicate enum in sync.
public class CallLog
{
    public Guid     Id           { get; set; }
    public string   CustomerName { get; set; } = string.Empty;
    public string   PhoneNumber  { get; set; } = string.Empty;
    public string   Outcome      { get; set; } = string.Empty;
    public DateTime CreatedAt    { get; set; }
}

// Request model — sent to POST /api/calllogs.
// Outcome carries the exact enum member name the backend expects.
public class CreateCallLogRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber  { get; set; } = string.Empty;
    public string Outcome      { get; set; } = string.Empty;
}
