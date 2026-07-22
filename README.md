# Appointment Booking MVC

A full **ASP.NET Core MVC (.NET 8)** multi-tenant appointment booking platform. Multiple admins ("tenants") each manage their own categories, appointments, bookings, and waitlists, while users browse and book sessions across all admins. Includes an AI chatbot assistant powered by the Gemini API.

## Features

- **Authentication & Roles** — ASP.NET Core Identity with `Admin` and `User` roles, separate login/register flows for each (`/Admin/Login`, `/Admin/Register`).
- **Multi-tenant isolation** — Every `Category`, `Appointment`, `Booking`, and `Waitlist` entry is scoped to the `Admin` who owns it, so each admin only manages their own data.
- **Categories** — Admins create service categories (icon, color, image, description) that group their appointments.
- **Appointments** — Admins create bookable time slots with price, duration, capacity, and status (Active / Completed).
- **Bookings** — Users book available appointments; admins confirm, reject, or cancel bookings from their dashboard.
- **Waitlist system** — When an appointment is full, users can join a waitlist. When a slot frees up (via cancellation), the next person in line is automatically promoted and booked, with a notification sent.
- **Admin Portal** — Dashboard with booking/revenue analytics (Chart.js), category & appointment management, booking management, and waitlist management.
- **User Dashboard** — Upcoming/completed/cancelled bookings, waitlist status and position, favorite categories, and appointment search/filtering.
- **AI Chatbot** — A Gemini-powered assistant that answers questions about categories, appointments, and the logged-in user's own bookings/waitlist, in either Arabic or English.
- **Notifications** — In-app notifications for booking confirmations and waitlist promotions.
- **Global exception handling** — Centralized middleware redirects unhandled errors to a friendly error page.

## Tech Stack

- ASP.NET Core MVC (.NET 8)
- Entity Framework Core (SQL Server / LocalDB)
- ASP.NET Core Identity
- Bootstrap, Chart.js
- Google Gemini API (chatbot)

## Project Structure

```
appointment-booking-mvc/
├── Controllers/       # Account, Admin Portal, Booking, Category, Appointment, Chatbot, Home, UserDashboard
├── Models/            # ApplicationUser, Category, Appointment, Booking, Waitlist, Notification, ContactMessage
├── Data/              # ApplicationDbContext, DbInitializer (seed data)
├── Services/          # FileService, WaitlistService, ChatbotService
├── Infrastructure/    # GlobalExceptionMiddleware
├── Program.cs
└── appsettings.json
```

> **Note:** This repository contains the core C# source (controllers, models, data layer, services, infrastructure). The Razor Views (`.cshtml`), client-side assets (`wwwroot`), and EF Core migrations from the original project were left out of this upload to keep the repo lean — happy to add them if you'd like the full project pushed.

## Getting Started

1. Update the connection string in `appsettings.json` if needed (defaults to LocalDB).
2. Add your Gemini API key to `appsettings.json` under `GeminiApiKey` to enable the chatbot (optional — the app runs fine without it, the chatbot just won't respond).
3. Run EF Core migrations to create the database:
   ```bash
   dotnet ef database update
   ```
4. Run the app:
   ```bash
   dotnet run
   ```
5. On first run, the database is seeded with demo admins, users, categories, appointments, and bookings (see `Data/DbInitializer.cs` for credentials).

> ⚠️ The seeded demo accounts and `AdminSecret` key in `AccountController` are for local development only — change them before deploying to production.

## License

MIT
