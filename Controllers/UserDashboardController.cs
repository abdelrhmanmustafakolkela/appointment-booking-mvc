using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using project.Data;
using project.Models;
using project.Services;

namespace project.Controllers;

[Authorize(Roles = "User")]
public class UserDashboardController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly WaitlistService _waitlistService;

    public UserDashboardController(UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        WaitlistService waitlistService)
    {
        _userManager = userManager;
        _context = context;
        _waitlistService = waitlistService;
    }

    [HttpGet("/User/Dashboard")]
    public async Task<IActionResult> Dashboard(string? search, int? categoryId)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var user = await _context.Users
                .Include(u => u.Notifications)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return Challenge();

            var now = DateTime.Now;

            var allBookings = await _context.Bookings
                .AsNoTracking()
                .Include(b => b.Appointment).ThenInclude(a => a!.Category)
                .Where(b => b.UserId == userId)
                .ToListAsync();

            ViewBag.UpcomingBookings = allBookings
                .Where(b => b.Appointment != null &&
                            b.Status != "Cancelled" &&
                            b.Status != "Rejected" &&
                            b.Appointment.Date.ToDateTime(b.Appointment.Time) >= now)
                .OrderBy(b => b.Appointment!.Date)
                .ThenBy(b => b.Appointment!.Time)
                .ToList();

            ViewBag.CompletedBookings = allBookings
                .Where(b => b.Status == "Confirmed" &&
                            b.Appointment != null &&
                            b.Appointment.Date.ToDateTime(b.Appointment.Time) < now)
                .OrderByDescending(b => b.Appointment!.Date)
                .ToList();

            ViewBag.CancelledBookings = allBookings
                .Where(b => b.Status == "Cancelled" || b.Status == "Rejected")
                .OrderByDescending(b => b.BookingDate)
                .ToList();

            ViewBag.FavoriteCategories = allBookings
                .Where(b => b.Appointment?.Category != null)
                .GroupBy(b => b.Appointment!.Category!.Id)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => g.First().Appointment!.Category)
                .ToList();

            var appointmentsQuery = _context.Appointments
                .AsNoTracking()
                .Include(a => a.Category)
                .Include(a => a.Bookings)
                .Where(a => a.IsAvailable && a.Status == "Active")
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                appointmentsQuery = appointmentsQuery.Where(a => a.Title.Contains(search));

            if (categoryId.HasValue)
                appointmentsQuery = appointmentsQuery.Where(a => a.CategoryId == categoryId.Value);

            var dbAppointments = await appointmentsQuery.ToListAsync();

            ViewBag.AvailableAppointments = dbAppointments
                .Where(a => a.Date.ToDateTime(a.Time) >= now)
                .OrderBy(a => a.Date)
                .Take(6)
                .ToList();

            var myWaitlist = await _context.Waitlists
                .AsNoTracking()
                .Include(w => w.Appointment).ThenInclude(a => a!.Category)
                .Where(w => w.UserId == userId && w.Status == "Waiting")
                .ToListAsync();

            var waitlistData = new List<dynamic>();
            foreach (var item in myWaitlist)
            {
                var position = await _waitlistService.GetWaitlistPositionAsync(item.UserId, item.AppointmentId);
                waitlistData.Add(new { Entry = item, Position = position });
            }
            ViewBag.WaitlistData = waitlistData;
            ViewBag.WaitlistCount = myWaitlist.Count;
            ViewBag.UserWaitlistAppointmentIds = myWaitlist.Select(w => w.AppointmentId).ToHashSet();
            ViewBag.WaitlistIdMap = myWaitlist.GroupBy(w => w.AppointmentId).ToDictionary(g => g.Key, g => g.First().Id);

            ViewBag.TotalBookings = allBookings.Count;
            ViewBag.UpcomingCount = (ViewBag.UpcomingBookings as List<Booking>)?.Count ?? 0;

            ViewBag.Categories = await _context.Categories.AsNoTracking().Where(c => c.IsActive).ToListAsync();

            return View(user);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "A technical error occurred while loading your dashboard. Support has been notified.";
            return RedirectToAction("Error", "Home", new { message = ex.Message });
        }
    }
}
