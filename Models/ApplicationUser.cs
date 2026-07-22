using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace project.Models;

public class ApplicationUser : IdentityUser
{
    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    public int? Age { get; set; }

    [StringLength(100)]
    public string? JobProfession { get; set; }

    [StringLength(200)]
    public string? UniversityName { get; set; }

    public string? ProfileImage { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<Waitlist> WaitlistEntries { get; set; } = new List<Waitlist>();
}
