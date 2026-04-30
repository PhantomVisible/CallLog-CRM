namespace CallLogCRM.Api.Services.Sms;

// Mock SMS implementation — logs to console so the flow is testable without Twilio.
// TODO (Twilio): Replace Console.WriteLine with:
//   await _twilioClient.Messages.CreateAsync(
//       to:   new PhoneNumber(phoneNumber),
//       from: new PhoneNumber(_config["Twilio:FromNumber"]),
//       body: message);
public class SmsService : ISmsService
{
    public void SendSms(string phoneNumber, string message)
    {
        Console.WriteLine($"[SMS MOCK] To: {phoneNumber} | {message}");
    }
}
