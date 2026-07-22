using Microsoft.EntityFrameworkCore;
using project.Data;
using project.Models;

namespace project.Services;

public class WaitlistService
{
    private readonly ApplicationDbContext _context;

    public WaitlistService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<(bool Success, string Message)> JoinWaitlistAsync(string userId, int appointmentId)
    {
        var appointment = await _context.Appointments
            .Include(a => a.Bookings)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (appointment == null)
            return (false, "Appointment not found.");

        if (appointment.Date.ToDateTime(appointment.Time) < DateTime.Now)
            return (false, "Cannot join the waitlist for a past appointment.");

        var activeBookings = appointment.Bookings.Count(b => b.Status != "Cancelled" && b.Status != "Rejected");
        if (activeBookings < appointment.MaxBookings)
            return (false, "This appointment still has available slots. Please book directly.");

        var alreadyBooked = await _context.Bookings
            .AnyAsync(b => b.AppointmentId == appointmentId && b.UserId == userId
                        && b.Status != "Cancelled" && b.Status != "Rejected");
        if (alreadyBooked)
            return (false, "You already have an active booking for this appointment.");

        var existing = await _context.Waitlists
            .FirstOrDefaultAsync(w => w.AppointmentId == appointmentId && w.UserId == userId);

        if (existing != null)
        {
            if (existing.Status == "Waiting")
                return (false, "You are already on the waitlist for this appointment.");
            if (existing.Status == "Promoted")
                return (false, "You have already been promoted from the waitlist.");

            existing.Status = "Waiting";
            existing.JoinedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return (true, "You have rejoined the waitlist successfully.");
        }

        _context.Waitlists.Add(new Waitlist
        {
            UserId = userId,
            AppointmentId = appointmentId,
            JoinedAt = DateTime.UtcNow,
            Status = "Waiting",
            AdminId = appointment.AdminId
        });
        await _context.SaveChangesAsync();
        return (true, "You have joined the waitlist. We will notify you when a slot opens.");
    }

    public async Task<(bool Success, string Message)> LeaveWaitlistAsync(string userId, int waitlistId)
    {
        var entry = await _context.Waitlists
            .FirstOrDefaultAsync(w => w.Id == waitlistId && w.UserId == userId);

        if (entry == null)
            return (false, "Waitlist entry not found.");

        if (entry.Status != "Waiting")
            return (false, "You are not actively waiting on this waitlist.");

        entry.Status = "Left";
        await _context.SaveChangesAsync();
        return (true, "You have been removed from the waitlist.");
    }

    public async Task PromoteNextInWaitlistAsync(int appointmentId)
    {
        var appointment = await _context.Appointments
            .Include(a => a.Bookings)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (appointment == null) return;

        var activeBookings = appointment.Bookings.Count(b => b.Status != "Cancelled" && b.Status != "Rejected");
        if (activeBookings >= appointment.MaxBookings) return;

        var nextEntry = await _context.Waitlists
            .Where(w => w.AppointmentId == appointmentId && w.Status == "Waiting")
            .OrderBy(w => w.JoinedAt)
            .FirstOrDefaultAsync();

        if (nextEntry == null) return;

        nextEntry.Status = "Promoted";

        _context.Bookings.Add(new Booking
        {
            UserId = nextEntry.UserId,
            AppointmentId = appointmentId,
            BookingDate = DateTime.UtcNow,
            Status = "Confirmed",
            AdminId = appointment.AdminId
        });

        _context.Notifications.Add(new Notification
        {
            UserId = nextEntry.UserId,
            Message = $"Great news! A slot opened up for \"{appointment.Title}\" on {appointment.Date.ToShortDateString()} at {appointment.Time:hh\\:mm tt}. You have been automatically booked!",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
    }

    public async Task<(bool Success, string Message)> PromoteUserAsync(int waitlistId, string adminId)
    {
        var entry = await _context.Waitlists
            .Include(w => w.Appointment)
            .FirstOrDefaultAsync(w => w.Id == waitlistId && w.AdminId == adminId);

        if (entry == null)
            return (false, "Waitlist entry not found or access denied.");

        if (entry.Status != "Waiting")
            return (false, "This user is no longer on the waiting list.");

        var appointment = entry.Appointment;
        if (appointment == null)
            return (false, "Associated appointment not found.");

        entry.Status = "Promoted";

        _context.Bookings.Add(new Booking
        {
            UserId = entry.UserId,
            AppointmentId = entry.AppointmentId,
            BookingDate = DateTime.UtcNow,
            Status = "Confirmed",
            AdminId = adminId
        });

        _context.Notifications.Add(new Notification
        {
            UserId = entry.UserId,
            Message = $"Congratulations! You have been promoted from the waitlist for \"{appointment.Title}\" and your booking is now confirmed.",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return (true, "User promoted to booking successfully.");
    }

    public async Task<int?> GetWaitlistPositionAsync(string userId, int appointmentId)
    {
        var waitingEntries = await _context.Waitlists
            .Where(w => w.AppointmentId == appointmentId && w.Status == "Waiting")
            .OrderBy(w => w.JoinedAt)
            .ToListAsync();

        var idx = waitingEntries.FindIndex(w => w.UserId == userId);
        return idx >= 0 ? idx + 1 : null;
    }

    public async Task<Waitlist?> GetActiveEntryAsync(string userId, int appointmentId)
    {
        return await _context.Waitlists
            .FirstOrDefaultAsync(w => w.UserId == userId
                                   && w.AppointmentId == appointmentId
                                   && w.Status == "Waiting");
    }
}
