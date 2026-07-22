using System.ComponentModel.DataAnnotations;

namespace project.Models;

public class Category
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public string? ImagePath { get; set; }

    [StringLength(100)]
    public string? IconClass { get; set; }

    [StringLength(20)]
    public string? ColorHex { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? AdminId { get; set; }
    public ApplicationUser? Admin { get; set; }

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
