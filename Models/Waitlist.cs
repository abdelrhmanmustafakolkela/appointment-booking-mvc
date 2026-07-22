using System.ComponentModel.DataAnnotations;

namespace project.Models;

public class Waitlist
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required]
    public int AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Waiting";

    public string? AdminId { get; set; }
}
