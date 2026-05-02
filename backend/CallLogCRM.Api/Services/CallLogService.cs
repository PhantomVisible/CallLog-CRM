using CallLogCRM.Api.Data;
using CallLogCRM.Api.DTOs;
using CallLogCRM.Api.Models;
using CallLogCRM.Api.Services.Sms;
using Microsoft.EntityFrameworkCore;

namespace CallLogCRM.Api.Services;

public class CallLogService(
    AppDbContext db,
    ISmsService sms,
    IGoogleSheetsWritebackService sheetsWriteback) : ICallLogService
{
    public async Task<CallLog> CreateCallLogAsync(CreateCallLogDto dto, Guid userId, string? notes = null)
    {
        var log = new CallLog
        {
            UserId          = userId,
            CustomerName    = dto.CustomerName,
            PhoneNumber     = dto.PhoneNumber,
            Outcome         = dto.Outcome,
            Notes           = notes,
            Revenue         = dto.Revenue,
            AmountCollected = dto.AmountCollected
        };

        db.CallLogs.Add(log);

        // Mark the matching reservation as handled so it disappears from the queue.
        var reservation = await db.CallReservations
            .FirstOrDefaultAsync(r =>
                r.PhoneNumber == dto.PhoneNumber && r.AssignedUserId == userId);

        if (reservation is not null)
            reservation.CurrentStatus = "Traité";

        await db.SaveChangesAsync();

        // Dispatch SMS after the record is safely persisted.
        // TODO (Twilio): sms.SendSms will call the real Twilio API once SmsService is replaced.
        var message = GetSmsMessage(dto.Outcome);
        if (message is not null)
            sms.SendSms(dto.PhoneNumber, message);

        // ── Google Sheet writeback ──────────────────────────
        var statusText = FormatOutcome(dto.Outcome);
        var email = reservation?.Email ?? string.Empty;

        // Fire-and-forget style: the writeback service catches its own exceptions
        // so it never breaks the main call-log flow.
        _ = sheetsWriteback.UpdateCallStatusAsync(dto.PhoneNumber, email, statusText, notes);

        return log;
    }

    public async Task<IEnumerable<CallLog>> GetAllCallLogsAsync()
        => await db.CallLogs
                   .OrderByDescending(l => l.CreatedAt)
                   .ToListAsync();

    public async Task<IEnumerable<CallLogAdminDto>> GetAdminCallLogsAsync()
        => await db.CallLogs
                   .Include(l => l.User)
                   .OrderByDescending(l => l.CreatedAt)
                   .Select(l => new CallLogAdminDto
                   {
                       Id              = l.Id,
                       CloserName      = l.User != null ? l.User.CloserName : "Inconnu",
                       CustomerName    = l.CustomerName,
                       PhoneNumber     = l.PhoneNumber,
                       Outcome         = l.Outcome.ToString(),
                       Notes           = l.Notes,
                       Revenue         = l.Revenue,
                       AmountCollected = l.AmountCollected,
                       CreatedAt       = l.CreatedAt
                   })
                   .ToListAsync();

    public async Task<IEnumerable<CallLog>> GetMyCallLogsAsync(Guid userId)
        => await db.CallLogs
                   .Where(l => l.UserId == userId)
                   .OrderByDescending(l => l.CreatedAt)
                   .ToListAsync();

    private static string? GetSmsMessage(CallOutcome outcome) => outcome switch
    {
        CallOutcome.NotAnswered_VoicemailLeft => "We tried to reach you and left a voicemail. Reply YES to get a callback.",
        CallOutcome.NotAnswered_NoVoicemail   => "We tried to reach you but couldn't leave a message. Reply YES for a callback.",
        CallOutcome.NotAnswered_VoicemailFull => "Your voicemail box is full. Please reply YES so we can call you back.",
        CallOutcome.Answered_NotAvailable     => "As discussed, we will call you back later.",
        CallOutcome.Answered_Available        => null,
        _                                     => null
    };

    private static string FormatOutcome(CallOutcome outcome) => outcome switch
    {
        CallOutcome.Answered_Available        => "Répondu — Disponible",
        CallOutcome.Answered_NotAvailable     => "Répondu — Pas Disponible",
        CallOutcome.NotAnswered_VoicemailLeft => "Pas Répondu — Messagerie Laissée",
        CallOutcome.NotAnswered_NoVoicemail   => "Pas Répondu — Pas de Messagerie",
        CallOutcome.NotAnswered_VoicemailFull => "Pas Répondu — Messagerie Pleine",
        _                                     => outcome.ToString()
    };
}
