using System.ComponentModel.DataAnnotations;

namespace project.Models;

public class Appointment
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateOnly Date { get; set; }

    [Required]
    [DataType(DataType.Time)]
    public TimeOnly Time { get; set; }

    [Range(1, 480)]
    public int DurationMinutes { get; set; } = 60;

    [Range(1, 1000)]
    public int MaxBookings { get; set; } = 1;

    [Range(0, 999999)]
    [DataType(DataType.Currency)]
    public decimal Price { get; set; }

    [StringLength(50)]
    public string Status { get; set; } = "Active";

    public bool IsAvailable { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Waitlist> WaitlistEntries { get; set; } = new List<Waitlist>();

    public string? AdminId { get; set; }
    public ApplicationUser? Admin { get; set; }
}
