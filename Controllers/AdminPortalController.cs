using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using project.Data;
using project.Models;
using project.Services;

namespace project.Controllers;

[Authorize(Roles = "Admin")]
[Route("Admin/[action]")]
public class AdminPortalController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly FileService _fileService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly WaitlistService _waitlistService;

    public AdminPortalController(ApplicationDbContext context, 
        FileService fileService, 
        UserManager<ApplicationUser> userManager,
        WaitlistService waitlistService)
    {
        _context = context;
        _fileService = fileService;
        _userManager = userManager;
        _waitlistService = waitlistService;
    }

    private string? CurrentAdminId => _userManager.GetUserId(User);

    [Route("/Admin/Dashboard")]
    public async Task<IActionResult> Index()
    {
        var adminId = CurrentAdminId;
        if (adminId == null) return Challenge();

        ViewBag.TotalBookings     = await _context.Bookings.CountAsync(b => b.AdminId == adminId);
        ViewBag.TotalAppointments = await _context.Appointments.CountAsync(a => a.AdminId == adminId);
        ViewBag.TotalCategories   = await _context.Categories.CountAsync(c => c.AdminId == adminId);
        ViewBag.TotalWaitlist     = await _context.Waitlists.CountAsync(w => w.AdminId == adminId && w.Status == "Waiting");

        ViewBag.TotalUsers = await _context.Bookings
            .Where(b => b.AdminId == adminId)
            .Select(b => b.UserId)
            .Distinct()
            .CountAsync();

        var appointments = await _context.Appointments
            .Include(a => a.Bookings)
            .Where(a => a.AdminId == adminId)
            .ToListAsync();

        ViewBag.FullyBookedCount = appointments.Count(a =>
            a.Bookings.Count(b => b.Status != "Cancelled" && b.Status != "Rejected") >= a.MaxBookings);

        ViewBag.RecentBookings = await _context.Bookings
            .Include(b => b.User)
            .Include(b => b.Appointment)
            .Where(b => b.AdminId == adminId)
            .OrderByDescending(b => b.BookingDate)
            .Take(5)
            .ToListAsync();

        var recentUserIds = await _context.Bookings
            .Where(b => b.AdminId == adminId)
            .OrderByDescending(b => b.BookingDate)
            .Select(b => b.UserId)
            .Take(10)
            .Distinct()
            .ToListAsync();

        ViewBag.RecentUsers = await _context.Users
            .Where(u => recentUserIds.Contains(u.Id))
            .Take(5)
            .ToListAsync();

        var bookingsByCategory = await _context.Bookings
            .Include(b => b.Appointment).ThenInclude(a => a!.Category)
            .Where(b => b.AdminId == adminId && b.Appointment != null && b.Appointment.Category != null)
            .GroupBy(b => b.Appointment!.Category!.Name)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToListAsync();

        ViewBag.ChartLabels = bookingsByCategory.Select(x => x.Name).ToList();
        ViewBag.ChartData = bookingsByCategory.Select(x => x.Count).ToList();

        var last7Days = Enumerable.Range(0, 7).Select(i => DateTime.UtcNow.Date.AddDays(-i)).Reverse().ToList();
        var bookingsPerDay = await _context.Bookings
            .Where(b => b.AdminId == adminId && b.BookingDate >= DateTime.UtcNow.Date.AddDays(-7))
            .GroupBy(b => b.BookingDate.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        ViewBag.TrendLabels = last7Days.Select(d => d.ToString("MMM dd")).ToList();
        ViewBag.TrendData = last7Days.Select(d => bookingsPerDay.FirstOrDefault(x => x.Date == d.Date)?.Count ?? 0).ToList();

        return View();
    }

    public async Task<IActionResult> Bookings()
    {
        var adminId = CurrentAdminId;
        var bookings = await _context.Bookings
            .Include(b => b.User)
            .Include(b => b.Appointment).ThenInclude(a => a!.Category)
            .Where(b => b.AdminId == adminId)
            .OrderByDescending(b => b.BookingDate)
            .ToListAsync();
        return View(bookings);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBookingStatus(int bookingId, string status)
    {
        var adminId = CurrentAdminId;
        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId && b.AdminId == adminId);
        if (booking == null) return RedirectWithError(nameof(Bookings), "Booking not found or access denied.");

        string oldStatus = booking.Status;
        booking.Status = status;
        await _context.SaveChangesAsync();

        if ((oldStatus == "Confirmed" || oldStatus == "Pending") && (status == "Cancelled" || status == "Rejected"))
        {
            await _waitlistService.PromoteNextInWaitlistAsync(booking.AppointmentId);
        }

        TempData["SuccessMessage"] = $"Booking status updated to {status}.";
        return RedirectToAction(nameof(Bookings));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBooking(int bookingId)
    {
        var adminId = CurrentAdminId;
        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId && b.AdminId == adminId);
        if (booking == null) return RedirectWithError(nameof(Bookings), "Booking not found or access denied.");

        int appointmentId = booking.AppointmentId;
        string status = booking.Status;

        _context.Bookings.Remove(booking);
        await _context.SaveChangesAsync();

        if (status == "Confirmed" || status == "Pending")
        {
            await _waitlistService.PromoteNextInWaitlistAsync(appointmentId);
        }

        TempData["SuccessMessage"] = "Booking deleted successfully.";
        return RedirectToAction(nameof(Bookings));
    }

    public async Task<IActionResult> Appointments(string search, string dateFilter)
    {
        var adminId = CurrentAdminId;
        var query = _context.Appointments
            .Include(a => a.Category)
            .Include(a => a.Bookings)
            .Include(a => a.WaitlistEntries)
            .Where(a => a.AdminId == adminId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(a => a.Title.Contains(search) || (a.Description != null && a.Description.Contains(search)));

        if (!string.IsNullOrEmpty(dateFilter) && DateOnly.TryParse(dateFilter, out var parsedDate))
            query = query.Where(a => a.Date == parsedDate);

        var appointments = await query.OrderBy(a => a.Date).ThenBy(a => a.Time).ToListAsync();
        ViewBag.Search = search;
        ViewBag.DateFilter = dateFilter;
        return View(appointments);
    }

    [HttpGet]
    public async Task<IActionResult> CreateAppointment()
    {
        var adminId = CurrentAdminId;
        ViewBag.Categories = await _context.Categories
            .Where(c => c.IsActive && c.AdminId == adminId)
            .ToListAsync();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAppointment(Appointment appointment)
    {
        var adminId = CurrentAdminId;
        if (ModelState.IsValid)
        {
            appointment.AdminId = adminId;
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Appointment created successfully.";
            return RedirectToAction(nameof(Appointments));
        }
        ViewBag.Categories = await _context.Categories
            .Where(c => c.IsActive && c.AdminId == adminId)
            .ToListAsync();
        return View(appointment);
    }

    [HttpGet]
    public async Task<IActionResult> EditAppointment(int id)
    {
        var adminId = CurrentAdminId;
        var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.Id == id && a.AdminId == adminId);
        if (appointment == null) return RedirectWithError(nameof(Appointments), "Appointment not found or access denied.");

        ViewBag.Categories = await _context.Categories
            .Where(c => c.IsActive && c.AdminId == adminId)
            .ToListAsync();
        return View(appointment);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAppointment(Appointment appointment)
    {
        var adminId = CurrentAdminId;
        var existing = await _context.Appointments.FirstOrDefaultAsync(a => a.Id == appointment.Id && a.AdminId == adminId);
        if (existing == null) return RedirectWithError(nameof(Appointments), "Access denied.");

        if (ModelState.IsValid)
        {
            existing.Title = appointment.Title;
            existing.Description = appointment.Description;
            existing.Price = appointment.Price;
            existing.Date = appointment.Date;
            existing.Time = appointment.Time;
            existing.DurationMinutes = appointment.DurationMinutes;
            existing.MaxBookings = appointment.MaxBookings;
            existing.CategoryId = appointment.CategoryId;
            existing.Status = appointment.Status;
            existing.IsAvailable = appointment.IsAvailable;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Appointment updated successfully.";
            return RedirectToAction(nameof(Appointments));
        }
        ViewBag.Categories = await _context.Categories
            .Where(c => c.IsActive && c.AdminId == adminId)
            .ToListAsync();
        return View(appointment);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAppointmentAvailability(int id)
    {
        var adminId = CurrentAdminId;
        var a = await _context.Appointments.FirstOrDefaultAsync(x => x.Id == id && x.AdminId == adminId);
        if (a == null) return RedirectWithError(nameof(Appointments), "Access denied.");

        a.IsAvailable = !a.IsAvailable;
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Availability toggled.";
        return RedirectToAction(nameof(Appointments));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAppointment(int id)
    {
        var adminId = CurrentAdminId;
        var a = await _context.Appointments.FirstOrDefaultAsync(x => x.Id == id && x.AdminId == adminId);
        if (a == null) return RedirectWithError(nameof(Appointments), "Access denied.");

        _context.Appointments.Remove(a);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Appointment deleted.";
        return RedirectToAction(nameof(Appointments));
    }

    public async Task<IActionResult> Categories()
    {
        var adminId = CurrentAdminId;
        var categories = await _context.Categories
            .Include(c => c.Appointments)
            .Where(c => c.AdminId == adminId)
            .OrderBy(c => c.Name)
            .ToListAsync();
        return View(categories);
    }

    [HttpGet]
    public IActionResult CreateCategory() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(Category category, IFormFile? imageFile)
    {
        ModelState.Remove(nameof(Category.ImagePath));
        var adminId = CurrentAdminId;

        if (ModelState.IsValid)
        {
            if (imageFile != null && imageFile.Length > 0)
                category.ImagePath = await _fileService.SaveFileAsync(imageFile, "categories");

            category.CreatedAt = DateTime.UtcNow;
            category.AdminId = adminId;

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Category \"{category.Name}\" created successfully.";
            return RedirectToAction(nameof(Categories));
        }
        return View(category);
    }

    [HttpGet]
    public async Task<IActionResult> EditCategory(int id)
    {
        var adminId = CurrentAdminId;
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id && c.AdminId == adminId);
        if (category == null) return RedirectWithError(nameof(Categories), "Category not found or access denied.");
        return View(category);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(int id, Category category, IFormFile? imageFile)
    {
        var adminId = CurrentAdminId;
        var existing = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id && c.AdminId == adminId);
        if (existing == null) return RedirectWithError(nameof(Categories), "Access denied.");

        if (ModelState.IsValid)
        {
            existing.Name        = category.Name;
            existing.Description = category.Description;
            existing.IconClass   = category.IconClass;
            existing.ColorHex    = category.ColorHex;
            existing.IsActive    = category.IsActive;

            if (imageFile != null && imageFile.Length > 0)
            {
                if (!string.IsNullOrEmpty(existing.ImagePath))
                    _fileService.DeleteFile(existing.ImagePath);
                existing.ImagePath = await _fileService.SaveFileAsync(imageFile, "categories");
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Category \"{existing.Name}\" updated successfully.";
            return RedirectToAction(nameof(Categories));
        }
        return View(category);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var adminId = CurrentAdminId;
        var category = await _context.Categories
            .Include(c => c.Appointments)
            .FirstOrDefaultAsync(c => c.Id == id && c.AdminId == adminId);

        if (category == null) return RedirectWithError(nameof(Categories), "Access denied.");

        if (category.Appointments.Any())
        {
            TempData["ErrorMessage"] = "Cannot delete a category that has appointments.";
            return RedirectToAction(nameof(Categories));
        }

        if (!string.IsNullOrEmpty(category.ImagePath))
            _fileService.DeleteFile(category.ImagePath);

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Category deleted.";
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCategoryStatus(int id)
    {
        var adminId = CurrentAdminId;
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id && c.AdminId == adminId);
        if (category == null) return RedirectWithError(nameof(Categories), "Access denied.");

        category.IsActive = !category.IsActive;
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Category \"{category.Name}\" {(category.IsActive ? "activated" : "deactivated")}.";
        return RedirectToAction(nameof(Categories));
    }

    public async Task<IActionResult> Waitlist()
    {
        var adminId = CurrentAdminId;
        var entries = await _context.Waitlists
            .Include(w => w.User)
            .Include(w => w.Appointment).ThenInclude(a => a!.Category)
            .Where(w => w.AdminId == adminId)
            .OrderBy(w => w.AppointmentId)
            .ThenBy(w => w.JoinedAt)
            .ToListAsync();
        return View(entries);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminRemoveFromWaitlist(int id)
    {
        var adminId = CurrentAdminId;
        var entry = await _context.Waitlists.FirstOrDefaultAsync(w => w.Id == id && w.AdminId == adminId);
        if (entry == null) return RedirectWithError(nameof(Waitlist), "Waitlist entry not found or access denied.");

        _context.Waitlists.Remove(entry);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "User removed from waitlist.";
        return RedirectToAction(nameof(Waitlist));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PromoteFromWaitlist(int id)
    {
        var adminId = CurrentAdminId;
        if (adminId == null) return Challenge();

        var result = await _waitlistService.PromoteUserAsync(id, adminId);
        if (result.Success)
            TempData["SuccessMessage"] = result.Message;
        else
            TempData["ErrorMessage"] = result.Message;

        return RedirectToAction(nameof(Waitlist));
    }

    private IActionResult RedirectWithError(string action, string message)
    {
        TempData["ErrorMessage"] = message;
        return RedirectToAction(action);
    }
}
