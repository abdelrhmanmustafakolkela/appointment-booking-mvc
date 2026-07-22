using Microsoft.AspNetCore.Identity;
using project.Models;
using Microsoft.EntityFrameworkCore;

namespace project.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IConfiguration config)
    {
        string[] roles = { "Admin", "User" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var admin1 = await EnsureUserAsync(userManager, "admin@test.com", "Password123!", "Dr. Sarah Johnson", "Admin", 34, "Senior Healthcare Consultant", "Oxford University");
        var admin2 = await EnsureUserAsync(userManager, "michael.admin@demo.com", "Password123!", "Michael Chen", "Admin", 42, "Business Strategy Director", "Stanford University");
        var admin3 = await EnsureUserAsync(userManager, "emma.wilson@demo.com", "Password123!", "Emma Wilson", "Admin", 29, "Lead Educational Advisor", "Cambridge University");

        var admins = new List<ApplicationUser> { admin1, admin2, admin3 };

        var user1 = await EnsureUserAsync(userManager, "user@test.com", "Password123!", "John Doe", "User");
        var users = new List<ApplicationUser> { user1 };

        string[] names = { "Alice Brown", "Bob Smith", "Charlie Davis", "Diana Prince", "Ethan Hunt", "Fiona Glenanne", "George Costanza", "Hannah Abbott", "Ian Wright", "Julia Roberts", "Kevin Hart", "Laura Palmer", "Mark Ruffalo", "Nina Simone" };
        for (int i = 0; i < names.Length; i++)
        {
            var email = $"{names[i].Replace(" ", ".").ToLower()}{i}@demo.com";
            users.Add(await EnsureUserAsync(userManager, email, "Password123!", names[i], "User"));
        }

        if (!context.Categories.Any())
        {
            context.Categories.AddRange(
                new Category { Name = "Medical", AdminId = admin1.Id, ColorHex = "#ef4444", IconClass = "fa-solid fa-stethoscope", IsActive = true, Description = "Professional health consultations and checkups." },
                new Category { Name = "Consultation", AdminId = admin2.Id, ColorHex = "#f59e0b", IconClass = "fa-solid fa-comments", IsActive = true, Description = "Expert advice for your projects and goals." },
                new Category { Name = "Business", AdminId = admin2.Id, ColorHex = "#4f46e5", IconClass = "fa-solid fa-briefcase", IsActive = true, Description = "Strategic corporate planning and networking." },
                new Category { Name = "Coaching", AdminId = admin3.Id, ColorHex = "#10b981", IconClass = "fa-solid fa-user-tie", IsActive = true, Description = "Personal and professional growth coaching." },
                new Category { Name = "Education", AdminId = admin3.Id, ColorHex = "#3b82f6", IconClass = "fa-solid fa-graduation-cap", IsActive = true, Description = "Tutoring, workshops, and academic guidance." },
                new Category { Name = "Fitness", AdminId = admin1.Id, ColorHex = "#8b5cf6", IconClass = "fa-solid fa-dumbbell", IsActive = true, Description = "Personal training and wellness sessions." },
                new Category { Name = "Online Meetings", AdminId = admin2.Id, ColorHex = "#ec4899", IconClass = "fa-solid fa-video", IsActive = true, Description = "Virtual sessions for remote collaboration." }
            );
            await context.SaveChangesAsync();
        }

        if (!context.Appointments.Any())
        {
            var cats = await context.Categories.ToListAsync();
            var random = new Random();
            var appointments = new List<Appointment>();

            for (int i = 1; i <= 10; i++)
            {
                var cat = cats[random.Next(cats.Count)];
                var adm = admins.FirstOrDefault(a => a.Id == cat.AdminId) ?? admin1;
                appointments.Add(new Appointment { Title = $"Session {i}: {cat.Name} Workshop", AdminId = adm.Id, CategoryId = cat.Id, Price = random.Next(20, 200), MaxBookings = random.Next(5, 20), Date = DateOnly.FromDateTime(DateTime.Now.AddDays(random.Next(5, 30))), Time = new TimeOnly(random.Next(9, 17), 0), IsAvailable = true, Status = "Active", Description = $"A professional {cat.Name} session focused on results." });
            }

            for (int i = 1; i <= 8; i++)
            {
                var cat = cats[random.Next(cats.Count)];
                var adm = admins.FirstOrDefault(a => a.Id == cat.AdminId) ?? admin1;
                appointments.Add(new Appointment { Title = $"Full {cat.Name} Masterclass {i}", AdminId = adm.Id, CategoryId = cat.Id, Price = 100, MaxBookings = 2, Date = DateOnly.FromDateTime(DateTime.Now.AddDays(random.Next(1, 4))), Time = new TimeOnly(11, 0), IsAvailable = false, Status = "Active", Description = "This session is currently at full capacity." });
            }

            for (int i = 1; i <= 7; i++)
            {
                var cat = cats[random.Next(cats.Count)];
                var adm = admins.FirstOrDefault(a => a.Id == cat.AdminId) ?? admin1;
                appointments.Add(new Appointment { Title = $"Past {cat.Name} Event {i}", AdminId = adm.Id, CategoryId = cat.Id, Price = 50, MaxBookings = 10, Date = DateOnly.FromDateTime(DateTime.Now.AddDays(-random.Next(5, 20))), Time = new TimeOnly(14, 30), IsAvailable = false, Status = "Completed", Description = "Archive: This event has already taken place." });
            }

            for (int i = 1; i <= 5; i++)
            {
                var cat = cats[random.Next(cats.Count)];
                var adm = admins.FirstOrDefault(a => a.Id == cat.AdminId) ?? admin1;
                appointments.Add(new Appointment { Title = $"Hurry! {cat.Name} Session {i}", AdminId = adm.Id, CategoryId = cat.Id, Price = 75, MaxBookings = 10, Date = DateOnly.FromDateTime(DateTime.Now.AddDays(2)), Time = new TimeOnly(10, 0), IsAvailable = true, Status = "Active", Description = "Only a few slots remaining for this popular session." });
            }

            context.Appointments.AddRange(appointments);
            await context.SaveChangesAsync();
        }

        if (!context.Bookings.Any())
        {
            var appts = await context.Appointments.ToListAsync();
            var random = new Random();
            var bookingsList = new List<Booking>();

            var fullAppts = appts.Where(a => a.MaxBookings == 2 && a.Status == "Active").ToList();
            foreach (var appt in fullAppts)
            {
                for (int i = 0; i < 2; i++)
                {
                    var u = users[random.Next(users.Count)];
                    bookingsList.Add(new Booking { UserId = u.Id, AppointmentId = appt.Id, AdminId = appt.AdminId, Status = "Confirmed", BookingDate = DateTime.UtcNow.AddDays(-1) });
                }
            }

            var almostFull = appts.Where(a => a.Title.Contains("Hurry!")).ToList();
            foreach (var appt in almostFull)
            {
                for (int i = 0; i < 8; i++)
                {
                    var u = users[random.Next(users.Count)];
                    bookingsList.Add(new Booking { UserId = u.Id, AppointmentId = appt.Id, AdminId = appt.AdminId, Status = "Confirmed", BookingDate = DateTime.UtcNow.AddDays(-2) });
                }
            }

            while (bookingsList.Count < 50)
            {
                var appt = appts[random.Next(appts.Count)];
                var u = users[random.Next(users.Count)];
                var status = random.Next(10) > 2 ? "Confirmed" : (random.Next(2) == 0 ? "Cancelled" : "Completed");
                bookingsList.Add(new Booking { UserId = u.Id, AppointmentId = appt.Id, AdminId = appt.AdminId, Status = status, BookingDate = DateTime.UtcNow.AddDays(-random.Next(1, 10)) });
            }

            context.Bookings.AddRange(bookingsList);
            await context.SaveChangesAsync();
        }

        if (!context.Waitlists.Any())
        {
            var fullAppts = await context.Appointments.Where(a => a.MaxBookings == 2 && a.Status == "Active").ToListAsync();
            var random = new Random();
            foreach (var appt in fullAppts)
            {
                var u = users[random.Next(users.Count)];
                context.Waitlists.Add(new Waitlist { UserId = u.Id, AppointmentId = appt.Id, AdminId = appt.AdminId, Status = "Waiting", JoinedAt = DateTime.UtcNow.AddHours(-2) });
            }
            await context.SaveChangesAsync();
        }

        if (!context.Notifications.Any())
        {
            foreach (var u in users.Take(5))
            {
                context.Notifications.Add(new Notification { UserId = u.Id, Message = "Your booking has been confirmed! Looking forward to seeing you.", CreatedAt = DateTime.UtcNow.AddDays(-1), IsRead = false });
                context.Notifications.Add(new Notification { UserId = u.Id, Message = "Reminder: Your appointment is scheduled for tomorrow.", CreatedAt = DateTime.UtcNow.AddHours(-12), IsRead = true });
            }
            await context.SaveChangesAsync();
        }
    }

    private static async Task<ApplicationUser> EnsureUserAsync(UserManager<ApplicationUser> userManager, string email, string password, string name, string role, int? age = null, string? job = null, string? uni = null)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = name,
                EmailConfirmed = true,
                Age = age,
                JobProfession = job,
                UniversityName = uni,
                ProfileImage = $"https://ui-avatars.com/api/?name={name.Replace(" ", "+")}&background=random&color=fff&size=128",
                PhoneNumber = "123-456-7890"
            };
            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
        else
        {
            if (!await userManager.IsInRoleAsync(user, role))
                await userManager.AddToRoleAsync(user, role);
        }
        return user;
    }
}
