using CallLogCRM.Api.Data;
using CallLogCRM.Api.DTOs;
using CallLogCRM.Api.Models;
using CallLogCRM.Api.Services.Sms;
using Microsoft.EntityFrameworkCore;

namespace CallLogCRM.Api.Services;

public class CallLogService(AppDbContext db, ISmsService sms) : ICallLogService
{
    public async Task<CallLog> CreateCallLogAsync(CreateCallLogDto dto, Guid userId)
    {
        var log = new CallLog
        {
            UserId       = userId,
            CustomerName = dto.CustomerName,
            PhoneNumber  = dto.PhoneNumber,
            Outcome      = dto.Outcome
        };

        db.CallLogs.Add(log);
        await db.SaveChangesAsync();

        // Dispatch SMS after the record is safely persisted.
        // TODO (Twilio): sms.SendSms will call the real Twilio API once SmsService is replaced.
        var message = GetSmsMessage(dto.Outcome);
        if (message is not null)
            sms.SendSms(dto.PhoneNumber, message);

        return log;
    }

    public async Task<IEnumerable<CallLog>> GetAllCallLogsAsync()
        => await db.CallLogs
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
}
