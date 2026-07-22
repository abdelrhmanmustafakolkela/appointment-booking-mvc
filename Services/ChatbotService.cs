using Microsoft.EntityFrameworkCore;
using project.Data;
using project.Models;
using System.Text.Json;
using System.Net.Http;

namespace project.Services;

public class ChatbotService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public ChatbotService(ApplicationDbContext context, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<ChatbotResponse> GetResponseAsync(string message, string? userId, bool isAdmin)
    {
        string apiKey = _configuration["GeminiApiKey"];
        if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_GEMINI_API_KEY_HERE")
        {
            return new ChatbotResponse
            {
                Text = "I am currently disconnected. The Gemini API key is not configured in appsettings.json. Please add it to enable my smart features."
            };
        }

        var categories = await _context.Categories
            .Where(c => c.IsActive)
            .Select(c => new { c.Id, c.Name, c.Description })
            .ToListAsync();

        var appointments = await _context.Appointments
            .Include(a => a.Bookings)
            .Select(a => new {
                a.Id, a.Title, a.CategoryId, a.Price, a.Date, a.Time, a.MaxBookings, a.IsAvailable, a.Status, a.Description,
                currentBookingsCount = a.Bookings.Count
            })
            .ToListAsync();

        string userDataJson = "";
        if (!string.IsNullOrEmpty(userId))
        {
            var myBookings = await _context.Bookings
                .Include(b => b.Appointment)
                .Where(b => b.UserId == userId)
                .Select(b => new { AppointmentTitle = b.Appointment.Title, b.BookingDate, b.Status })
                .ToListAsync();

            var myWaitlists = await _context.Waitlists
                .Include(w => w.Appointment)
                .Where(w => w.UserId == userId && w.Status == "Waiting")
                .Select(w => new { AppointmentTitle = w.Appointment.Title, w.JoinedAt, w.Status })
                .ToListAsync();

            userDataJson = "\nCurrent User Data (Logged In):\n" +
                "User Bookings: " + JsonSerializer.Serialize(myBookings) + "\n" +
                "User Waitlists: " + JsonSerializer.Serialize(myWaitlists) + "\n" +
                "If the user asks about 'my bookings' or 'my waitlist', use this data to answer them.";
        }
        else
        {
            userDataJson = "\nUser is NOT logged in. If they ask for 'my bookings' or 'my waitlist', tell them they need to log in first.";
        }

        var systemPrompt = "You are a smart assistant for an appointment booking platform. " +
            "You understand any question in any language (Arabic or English) and respond naturally. " +
            "You are given real-time data from the system in JSON format, including Categories, Appointments, and User Data. " +
            "Always base your answers on the data provided. Never invent appointments, prices, or categories. " +
            "If an appointment is IsAvailable = false, mention it's fully booked and offer the waitlist option. " +
            "If Status = 'Completed', tell the user this session already happened. " +
            "Always respond in the same language the user writes in (Arabic or English). Keep responses short and friendly.\n\n" +
            "System Data (JSON):\nCategories: " + JsonSerializer.Serialize(categories) +
            "\nAppointments: " + JsonSerializer.Serialize(appointments) + userDataJson;

        var requestPayload = new
        {
            system_instruction = new { parts = new { text = systemPrompt } },
            contents = new[] { new { role = "user", parts = new[] { new { text = message } } } }
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(requestPayload), System.Text.Encoding.UTF8, "application/json");
            var responseMsg = await _httpClient.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}", content);

            if (responseMsg.IsSuccessStatusCode)
            {
                var jsonResponse = await responseMsg.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(jsonResponse);

                var botText = document.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text").GetString();

                var response = new ChatbotResponse { Text = botText };

                if (botText != null && (botText.Contains("الأقرب") || botText.Contains("الأرخص") || botText.Contains("الأكتر")))
                {
                    response.Suggestions.AddRange(new[] { "الأقرب", "الأكتر طلباً", "الأرخص سعر" });
                }

                return response;
            }
            else
            {
                return new ChatbotResponse { Text = "Sorry, I'm having trouble connecting to my AI core right now. Error: " + responseMsg.StatusCode };
            }
        }
        catch (Exception ex)
        {
            return new ChatbotResponse { Text = "An error occurred while connecting to the AI: " + ex.Message };
        }
    }
}

public class ChatbotResponse
{
    public string Text { get; set; } = string.Empty;
    public List<string> Suggestions { get; set; } = new List<string>();
}
