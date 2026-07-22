using System.ComponentModel.DataAnnotations;

namespace project.Models;

public class Booking
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    [Required]
    public int AppointmentId { get; set; }

    public Appointment? Appointment { get; set; }

    [Required]
    public DateTime BookingDate { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Pending";

    public string? AdminId { get; set; }
}
