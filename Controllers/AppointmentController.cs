using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using project.Data;

namespace project.Controllers;

public class AppointmentController : Controller
{
    private readonly ApplicationDbContext _context;

    public AppointmentController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var appointments = await _context.Appointments
            .OrderBy(a => a.Date)
            .ThenBy(a => a.Time)
            .ToListAsync();

        return View(appointments);
    }
}
