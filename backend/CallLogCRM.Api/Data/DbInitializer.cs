using CallLogCRM.Api.Models;

namespace CallLogCRM.Api.Data;

public static class DbInitializer
{
    public static void Initialize(AppDbContext context)
    {
        if (context.Users.Any())
            return;

        string[] closerNames =
        [
            "Hayat Mahir", "Amel K", "Sakina Slimani", "Kowsar Abdi",
            "Jennifer", "Batoul", "Ines", "Amel"
        ];

        foreach (var name in closerNames)
            context.Users.Add(new User { CloserName = name, Role = "Closer" });

        context.Users.Add(new User
        {
            CloserName   = "Bachir",
            Role         = "Admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Bachir123!")
        });

        context.SaveChanges();
    }
}
