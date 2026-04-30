namespace CallLogCRM.Api.Models;

// A single enum drives both the SMS dispatch rules in CallLogService
// and the outcome display in the frontend.
// When Twilio is integrated, each value maps to a specific SMS template.
public enum CallOutcome
{
    Answered_Available,
    Answered_NotAvailable,
    NotAnswered_VoicemailLeft,
    NotAnswered_NoVoicemail,
    NotAnswered_VoicemailFull
}

public class CallLog
{
    public Guid        Id           { get; set; } = Guid.NewGuid();

    // Nullable until auth is wired up; will become required once ClaimsPrincipal
    // is used to stamp the closer's identity on every call log.
    public Guid?       UserId       { get; set; }

    public string      CustomerName { get; set; } = string.Empty;
    public string      PhoneNumber  { get; set; } = string.Empty;
    public CallOutcome Outcome      { get; set; }
    public DateTime    CreatedAt    { get; set; } = DateTime.UtcNow;

    public User?       User         { get; set; }
}
