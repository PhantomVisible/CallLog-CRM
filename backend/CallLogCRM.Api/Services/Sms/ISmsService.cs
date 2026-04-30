namespace CallLogCRM.Api.Services.Sms;

// Abstraction over SMS delivery.
// Swap SmsService for a TwilioSmsService implementation once Twilio credentials
// are available — no other code needs to change.
public interface ISmsService
{
    void SendSms(string phoneNumber, string message);
}
