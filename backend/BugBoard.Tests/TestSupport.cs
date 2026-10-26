using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BugBoard.Api.Data;
using BugBoard.Api.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace BugBoard.Tests;

/// <summary>
/// Starts the REAL API in memory (same Program.cs, same pipeline, same JWT validation)
/// but with its own throw-away SQLite file and a test signing key.
/// This is an integration test: HTTP request in, HTTP response out.
/// </summary>
public sealed class BugBoardFactory : WebApplicationFactory<Program>
{
    public const string JwtKey = "unit-test-signing-key-that-is-at-least-32-bytes-long!";
    public const string Issuer = "BugBoard.Api";
    public const string Audience = "BugBoard.Web";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"bugboard-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development"); // Development = the database gets seeded
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = JwtKey,
            ["ConnectionStrings:Default"] = $"Data Source={_dbPath}"
        }));
    }

    /// <summary>A client that is already logged in as the given seeded user.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(Json.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    public Task<HttpClient> Tester1() => CreateAuthenticatedClientAsync(DbSeeder.Tester1Email, DbSeeder.Tester1Password);
    public Task<HttpClient> Tester2() => CreateAuthenticatedClientAsync(DbSeeder.Tester2Email, DbSeeder.Tester2Password);
    public Task<HttpClient> Lead() => CreateAuthenticatedClientAsync(DbSeeder.LeadEmail, DbSeeder.LeadPassword);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools(); // release the file lock so we can delete it
        try { File.Delete(_dbPath); } catch (IOException) { /* temp file, OS will clean up */ }
    }
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Options))!;
}

public static class BugHelpers
{
    public static async Task<BugDto> CreateBugAsync(this HttpClient client, string? title = null, string severity = "Medium")
    {
        var response = await client.PostAsJsonAsync("/api/bugs", new
        {
            title = title ?? $"Test bug {Guid.NewGuid():N}",
            description = "created by a test",
            severity
        });
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<BugDto>();
    }

    public static Task<HttpResponseMessage> UpdateBugAsync(this HttpClient client, BugDto bug, string? status = null, string? title = null) =>
        client.PutAsJsonAsync($"/api/bugs/{bug.Id}", new
        {
            title = title ?? bug.Title,
            description = bug.Description,
            severity = bug.Severity.ToString(),
            status = status ?? bug.Status.ToString(),
            assignedToId = bug.AssignedToId
        });
}
