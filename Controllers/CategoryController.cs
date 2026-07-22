using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using project.Data;

namespace project.Controllers;

public class CategoryController : Controller
{
    private readonly ApplicationDbContext _context;

    public CategoryController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var categories = await _context.Categories
            .Include(c => c.Appointments)
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return View(categories);
    }

    [HttpGet]
    public async Task<IActionResult> GetAppointments(int? categoryId, string? search)
    {
        var query = _context.Appointments
            .Include(a => a.Category)
            .Include(a => a.Bookings)
            .Where(a => a.IsAvailable && a.Status == "Active")
            .AsQueryable();

        if (categoryId.HasValue && categoryId > 0)
            query = query.Where(a => a.CategoryId == categoryId.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(a => a.Title.Contains(search) ||
                                     (a.Description != null && a.Description.Contains(search)));

        var appointments = await query
            .OrderBy(a => a.Date)
            .ThenBy(a => a.Time)
            .Select(a => new
            {
                a.Id,
                a.Title,
                a.Description,
                a.Price,
                Date = a.Date.ToShortDateString(),
                Time = a.Time.ToString("hh:mm tt"),
                a.DurationMinutes,
                a.MaxBookings,
                CategoryName = a.Category != null ? a.Category.Name : "",
                CategoryColor = a.Category != null ? a.Category.ColorHex : "#6366f1",
                CategoryIcon  = a.Category != null ? a.Category.IconClass : "fa-solid fa-calendar",
                ActiveBookings = a.Bookings.Count(b => b.Status != "Cancelled" && b.Status != "Rejected"),
                IsFull = a.Bookings.Count(b => b.Status != "Cancelled" && b.Status != "Rejected") >= a.MaxBookings,
                IsPast = a.Date.ToDateTime(a.Time) < DateTime.Now
            })
            .ToListAsync();

        return Json(appointments);
    }
}
