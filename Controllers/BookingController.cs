using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using project.Data;
using project.Models;
using project.Services;

namespace project.Controllers;

[Authorize(Roles = "User")]
public class BookingController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly WaitlistService _waitlistService;

    public BookingController(ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        WaitlistService waitlistService)
    {
        _context = context;
        _userManager = userManager;
        _waitlistService = waitlistService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BookAppointment(int appointmentId)
    {
        try
        {
            var appointment = await _context.Appointments
                .Include(a => a.Bookings)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null || !appointment.IsAvailable)
            {
                TempData["ErrorMessage"] = "Appointment is no longer available.";
                return RedirectToAction("Dashboard", "UserDashboard");
            }

            if (appointment.Date.ToDateTime(appointment.Time) < DateTime.Now)
            {
                TempData["ErrorMessage"] = "Cannot book past appointments.";
                return RedirectToAction("Dashboard", "UserDashboard");
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId)) return Challenge();

            var alreadyBooked = await _context.Bookings
                .AnyAsync(b => b.AppointmentId == appointmentId && b.UserId == userId
                            && b.Status != "Cancelled" && b.Status != "Rejected");
            if (alreadyBooked)
            {
                TempData["ErrorMessage"] = "You have already booked this appointment.";
                return RedirectToAction("Dashboard", "UserDashboard");
            }

            var currentCount = appointment.Bookings
                .Count(b => b.Status != "Cancelled" && b.Status != "Rejected");

            if (currentCount >= appointment.MaxBookings)
            {
                TempData["ErrorMessage"] = "This appointment is fully booked. You can join the waitlist.";
                return RedirectToAction("Dashboard", "UserDashboard");
            }

            _context.Bookings.Add(new Booking
            {
                UserId = userId,
                AppointmentId = appointmentId,
                BookingDate = DateTime.UtcNow,
                Status = "Pending",
                AdminId = appointment.AdminId
            });

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Booking request submitted successfully!";
            return RedirectToAction("Dashboard", "UserDashboard");
        }
        catch
        {
            TempData["ErrorMessage"] = "An error occurred while booking. Please try again.";
            return RedirectToAction("Dashboard", "UserDashboard");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> JoinWaitlist(int appointmentId)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var (success, message) = await _waitlistService.JoinWaitlistAsync(userId, appointmentId);
        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("Dashboard", "UserDashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LeaveWaitlist(int waitlistId)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var (success, message) = await _waitlistService.LeaveWaitlistAsync(userId, waitlistId);
        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction("Dashboard", "UserDashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelBooking(int bookingId)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var booking = await _context.Bookings
            .Include(b => b.Appointment)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.UserId == userId);

        if (booking == null)
        {
            TempData["ErrorMessage"] = "Booking not found.";
            return RedirectToAction("Dashboard", "UserDashboard");
        }

        if (booking.Status == "Cancelled")
        {
            TempData["ErrorMessage"] = "Booking is already cancelled.";
            return RedirectToAction("Dashboard", "UserDashboard");
        }

        booking.Status = "Cancelled";
        await _context.SaveChangesAsync();

        if (booking.AppointmentId > 0)
        {
            await _waitlistService.PromoteNextInWaitlistAsync(booking.AppointmentId);
        }

        TempData["SuccessMessage"] = "Booking cancelled successfully.";
        return RedirectToAction("Dashboard", "UserDashboard");
    }

    [HttpGet]
    public IActionResult MyBookings() => RedirectToAction("Dashboard", "UserDashboard");
}
