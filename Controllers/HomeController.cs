using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using project.Data;
using project.Models;

namespace project.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index() => View();

        public async Task<IActionResult> Appointments(string? search, int? categoryId)
        {
            var appointmentsQuery = _context.Appointments
                .Include(a => a.Category)
                .Where(a => a.IsAvailable)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalized = search.Trim().ToLower();
                appointmentsQuery = appointmentsQuery.Where(a =>
                    a.Title.ToLower().Contains(normalized) ||
                    (a.Description != null && a.Description.ToLower().Contains(normalized)));
            }

            if (categoryId.HasValue)
            {
                appointmentsQuery = appointmentsQuery.Where(a => a.CategoryId == categoryId.Value);
            }

            ViewBag.Categories = await _context.Categories.ToListAsync();
            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;

            var dbResults = await appointmentsQuery
                .OrderBy(a => a.Date)
                .ThenBy(a => a.Time)
                .ToListAsync();

            var now = DateTime.Now;
            var model = dbResults
                .Where(a => a.Date.ToDateTime(a.Time) >= now)
                .ToList();

            return View(model);
        }

        public async Task<IActionResult> AppointmentDetails(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Category)
                .Include(a => a.Admin)
                .Include(a => a.Bookings)
                .Include(a => a.WaitlistEntries)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            if (!string.IsNullOrEmpty(userId))
            {
                var userBooking = appointment.Bookings
                    .FirstOrDefault(b => b.UserId == userId && b.Status != "Cancelled" && b.Status != "Rejected");

                var userWaitlist = appointment.WaitlistEntries
                    .FirstOrDefault(w => w.UserId == userId && w.Status == "Waiting");

                ViewBag.UserBooking = userBooking;
                ViewBag.UserWaitlist = userWaitlist;
            }

            ViewBag.SimilarAppointments = await _context.Appointments
                .Include(a => a.Category)
                .Where(a => a.CategoryId == appointment.CategoryId && a.Id != appointment.Id && a.IsAvailable)
                .OrderBy(a => a.Date)
                .Take(3)
                .ToListAsync();

            return View(appointment);
        }

        public IActionResult About() => View();
        public IActionResult Services() => View();
        public IActionResult Contact() => View();
        public IActionResult FAQ() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(ContactMessage message)
        {
            if (ModelState.IsValid)
            {
                _context.ContactMessages.Add(message);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Your message has been sent successfully. We will contact you soon.";
                return RedirectToAction(nameof(Contact));
            }
            return View(message);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(string? message)
        {
            ViewBag.ErrorMessage = message;
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
