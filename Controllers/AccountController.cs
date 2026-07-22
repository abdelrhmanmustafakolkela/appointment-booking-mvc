using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using project.Data;
using project.Services;

namespace project.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _context;
    private readonly FileService _fileService;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext context,
        FileService fileService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _fileService = fileService;
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("Admin")) return RedirectToAction("Index", "AdminPortal");
            return RedirectToAction("Dashboard", "UserDashboard");
        }
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string fullName, string email, string password, string phoneNumber)
    {
        try
        {
            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "All fields are required.");
                return View();
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                PhoneNumber = phoneNumber
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "User");
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Dashboard", "UserDashboard");
            }

            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        }
        catch { ModelState.AddModelError("", "A database error occurred."); }
        return View();
    }

    [Route("Admin/Register")]
    [HttpGet]
    public IActionResult AdminRegister() => View();

    [Route("Admin/Register")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminRegister(string fullName, string email, string password, int age, string jobProfession, string universityName, string phoneNumber, string adminSecret)
    {
        if (adminSecret != "BooklyAdmin2026")
        {
            ModelState.AddModelError("", "Invalid Admin Secret Key.");
            return View();
        }

        try
        {
            var adminUser = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                Age = age,
                JobProfession = jobProfession,
                UniversityName = universityName,
                PhoneNumber = phoneNumber
            };

            var result = await _userManager.CreateAsync(adminUser, password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(adminUser, "Admin");
                await _signInManager.SignInAsync(adminUser, isPersistent: false);
                return RedirectToAction("Index", "AdminPortal");
            }
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        }
        catch { ModelState.AddModelError("", "Registration failed."); }
        return View();
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("Admin")) return RedirectToAction("Index", "AdminPortal");
            return RedirectToAction("Dashboard", "UserDashboard");
        }
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, bool rememberMe)
    {
        try
        {
            var result = await _signInManager.PasswordSignInAsync(email, password, rememberMe, false);
            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user != null && await _userManager.IsInRoleAsync(user, "Admin"))
                    return RedirectToAction("Index", "AdminPortal");
                return RedirectToAction("Dashboard", "UserDashboard");
            }
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        }
        catch { ModelState.AddModelError("", "Connection error."); }
        return View();
    }

    [Route("Admin/Login")]
    [HttpGet]
    public IActionResult AdminLogin() => View();

    [Route("Admin/Login")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminLogin(string email, string password, bool rememberMe)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || !await _userManager.IsInRoleAsync(user, "Admin"))
        {
            ModelState.AddModelError(string.Empty, "Invalid admin credentials.");
            return View();
        }

        var result = await _signInManager.PasswordSignInAsync(email, password, rememberMe, false);
        if (result.Succeeded) return RedirectToAction("Index", "AdminPortal");

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        var user = await _context.Users
            .Include(u => u.Notifications)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return Challenge();

        var bookings = await _context.Bookings
            .Include(b => b.Appointment).ThenInclude(a => a!.Category)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.BookingDate)
            .ToListAsync();

        ViewBag.TotalBookings = bookings.Count;
        ViewBag.UpcomingBookings = bookings.Where(b => b.Status != "Cancelled" && b.Status != "Rejected" && b.Appointment?.Date.ToDateTime(b.Appointment.Time) >= DateTime.Now).ToList();
        ViewBag.CompletedBookings = bookings.Where(b => b.Appointment?.Date.ToDateTime(b.Appointment.Time) < DateTime.Now).ToList();
        ViewBag.CancelledBookings = bookings.Where(b => b.Status == "Cancelled" || b.Status == "Rejected").ToList();

        ViewBag.FavCategory = bookings
            .Where(b => b.Appointment?.Category != null)
            .GroupBy(b => b.Appointment!.Category!.Name)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? "None yet";

        var roles = await _userManager.GetRolesAsync(user);
        ViewData["Layout"] = roles.Contains("Admin") ? "_AdminLayout" : "_UserLayout";

        return View(user);
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(string fullName, string phoneNumber, string? jobProfession, string? universityName)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        user.FullName = fullName;
        user.PhoneNumber = phoneNumber;
        user.JobProfession = jobProfession;
        user.UniversityName = universityName;

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
            TempData["SuccessMessage"] = "Profile updated successfully!";
        else
            TempData["ErrorMessage"] = "Failed to update profile.";

        return RedirectToAction(nameof(Profile));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (result.Succeeded)
            TempData["SuccessMessage"] = "Password changed successfully!";
        else
            TempData["ErrorMessage"] = string.Join(" ", result.Errors.Select(e => e.Description));

        return RedirectToAction(nameof(Profile));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadProfileImage(IFormFile profileImage)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (profileImage != null && profileImage.Length > 0)
        {
            if (!string.IsNullOrEmpty(user.ProfileImage))
                _fileService.DeleteFile(user.ProfileImage);

            user.ProfileImage = await _fileService.SaveFileAsync(profileImage, "profiles");
            await _userManager.UpdateAsync(user);
            TempData["SuccessMessage"] = "Profile image updated!";
        }

        return RedirectToAction(nameof(Profile));
    }
}
