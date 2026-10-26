using BugBoard.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BugBoard.Api.Data;

/// <summary>
/// Inserts demo data (2 Testers, 1 Lead and a few bugs) the first time the database is empty.
/// Only called in the Development environment. The passwords below are DEV-ONLY and are
/// documented in the README; they are hashed before being stored.
/// </summary>
public static class DbSeeder
{
    public const string Tester1Email = "tester1@bugboard.local";
    public const string Tester2Email = "tester2@bugboard.local";
    public const string LeadEmail = "lead@bugboard.local";

    public const string Tester1Password = "Tester1Pass!";
    public const string Tester2Password = "Tester2Pass!";
    public const string LeadPassword = "LeadPass123!";

    public static async Task SeedAsync(BugBoardDbContext db, TimeProvider clock)
    {
        if (await db.Users.AnyAsync())
        {
            return; // already seeded: running twice must not duplicate data
        }

        var hasher = new PasswordHasher<User>();
        User Make(string email, string name, Role role, string password)
        {
            var user = new User { Email = email, DisplayName = name, Role = role };
            user.PasswordHash = hasher.HashPassword(user, password);
            return user;
        }

        var tester1 = Make(Tester1Email, "Tina Tester", Role.Tester, Tester1Password);
        var tester2 = Make(Tester2Email, "Tom Tester", Role.Tester, Tester2Password);
        var lead = Make(LeadEmail, "Lena Lead", Role.Lead, LeadPassword);
        db.Users.AddRange(tester1, tester2, lead);
        await db.SaveChangesAsync(); // saves users so they get their Ids

        var now = clock.GetUtcNow().UtcDateTime;
        Bug NewBug(string title, string? description, Severity severity, BugStatus status,
            User reporter, User? assignee, int daysAgo) => new()
        {
            Title = title,
            Description = description,
            Severity = severity,
            Status = status,
            ReportedById = reporter.Id,
            AssignedToId = assignee?.Id,
            CreatedAt = now.AddDays(-daysAgo),
            UpdatedAt = now.AddDays(-daysAgo).AddHours(2)
        };

        db.Bugs.AddRange(
            NewBug("Login button unresponsive on Safari", "Clicking Sign in does nothing on Safari 17.", Severity.High, BugStatus.Open, tester1, lead, 12),
            NewBug("Typo on the pricing page", "'Recieve' should be 'Receive'.", Severity.Low, BugStatus.Open, tester2, null, 11),
            NewBug("Checkout total ignores discount code", "Discount SAVE10 is accepted but the total is unchanged.", Severity.Critical, BugStatus.InProgress, tester1, tester2, 10),
            NewBug("Search returns duplicate results", null, Severity.Medium, BugStatus.Resolved, tester2, tester1, 9),
            NewBug("Profile picture upload fails above 2 MB", "Upload spinner never stops for large images.", Severity.Medium, BugStatus.Open, tester1, null, 8),
            NewBug("Dashboard chart overlaps legend", "Visible at 1280px width.", Severity.Low, BugStatus.Closed, tester2, lead, 7),
            NewBug("Export to CSV drops last row", "Off-by-one when exporting a filtered list.", Severity.High, BugStatus.InProgress, tester1, tester1, 6),
            NewBug("Password reset email never arrives", "Checked spam folder as well.", Severity.Critical, BugStatus.Open, tester2, lead, 5),
            NewBug("Tooltip text cut off on mobile", null, Severity.Low, BugStatus.Resolved, tester1, null, 4),
            NewBug("Session expires without warning", "User is logged out mid-form and loses data.", Severity.High, BugStatus.Open, tester2, tester2, 3),
            NewBug("Date picker shows wrong month", "December shows as January in Firefox.", Severity.Medium, BugStatus.Open, tester1, null, 2),
            NewBug("Notification badge count is stale", "Count only refreshes after a hard reload.", Severity.Medium, BugStatus.InProgress, tester2, tester1, 1));

        await db.SaveChangesAsync();
    }
}
