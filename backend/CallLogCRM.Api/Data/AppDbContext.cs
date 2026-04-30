using CallLogCRM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CallLogCRM.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User>            Users            => Set<User>();
    public DbSet<CallReservation> CallReservations => Set<CallReservation>();
    public DbSet<CallLog>         CallLogs         => Set<CallLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // --- User ---
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).HasMaxLength(256);
            entity.Property(u => u.CloserName).HasMaxLength(128).IsRequired();
            entity.HasIndex(u => u.CloserName).IsUnique();
            entity.Property(u => u.Role).HasMaxLength(32).IsRequired();
        });

        // --- CallLog ---
        modelBuilder.Entity<CallLog>(entity =>
        {
            entity.HasKey(c => c.Id);

            // Store the enum as its string name for human-readable rows in Postgres.
            entity.Property(c => c.Outcome)
                  .HasConversion<string>()
                  .HasMaxLength(64)
                  .IsRequired();

            entity.HasIndex(c => c.CreatedAt);
            entity.HasIndex(c => c.UserId);

            // Optional FK — becomes required once auth is implemented.
            entity.HasOne(c => c.User)
                  .WithMany(u => u.CallLogs)
                  .HasForeignKey(c => c.UserId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // --- CallReservation ---
        modelBuilder.Entity<CallReservation>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.AssignedUserId);
            entity.HasIndex(r => r.AppointmentDate);

            entity.HasOne(r => r.User)
                  .WithMany(u => u.CallReservations)
                  .HasForeignKey(r => r.AssignedUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
