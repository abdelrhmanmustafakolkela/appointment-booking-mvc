using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using project.Models;
using project.Services;

namespace project.Controllers;

public class ChatbotController : Controller
{
    private readonly ChatbotService _chatbotService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ChatbotController(ChatbotService chatbotService, UserManager<ApplicationUser> userManager)
    {
        _chatbotService = chatbotService;
        _userManager = userManager;
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
    {
        if (string.IsNullOrEmpty(request.Message))
            return BadRequest();

        var userId = _userManager.GetUserId(User);
        var isAdmin = User.IsInRole("Admin");

        var response = await _chatbotService.GetResponseAsync(request.Message, userId, isAdmin);

        return Json(response);
    }

    [HttpGet]
    public IActionResult GetInitialSuggestions()
    {
        var suggestions = new List<string> { "View Appointments", "My Bookings", "How to book?", "Upcoming Events" };
        if (User.IsInRole("Admin"))
        {
            suggestions.Add("Admin Dashboard");
            suggestions.Add("View Analytics");
        }
        return Json(suggestions);
    }
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
}
