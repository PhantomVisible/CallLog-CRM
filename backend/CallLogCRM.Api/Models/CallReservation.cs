namespace CallLogCRM.Api.Models;

// Represents a single row synced from the Google Sheet — a pre-assigned lead
// that a specific closer is expected to call on a given day.
// Populated by the sync service (Phase 2); read-only from the closer's perspective.
public class CallReservation
{
    public Guid     Id              { get; set; } = Guid.NewGuid();
    public Guid     AssignedUserId  { get; set; }
    public string   CustomerName    { get; set; } = string.Empty;
    public string   PhoneNumber     { get; set; } = string.Empty;
    public string   Email           { get; set; } = string.Empty;
    public DateTime AppointmentDate { get; set; }

    // Identifies where the reservation came from (e.g. "GoogleSheet", "Manual").
    public string   Source          { get; set; } = string.Empty;

    // Live status synced from the Google Sheet "Statut Call" column (H).
    // Examples: "Vente", "Pas de vente", "RDV confirmé", etc. Null = not yet set.
    public string?  CurrentStatus   { get; set; }

    // Call notes written by the closeuse, synced from column I of the sheet.
    public string?  Notes           { get; set; }

    public User User { get; set; } = null!;
}
