using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BugBoard.Api.Data;
using BugBoard.Api.Dtos;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BugBoard.Tests;

public class AuthTests(BugBoardFactory factory) : IClassFixture<BugBoardFactory>
{
    private static async Task<HttpResponseMessage> Login(HttpClient client, string email, string password) =>
        await client.PostAsJsonAsync("/api/auth/login", new { email, password });

    [Fact]
    public async Task Login_with_valid_credentials_returns_token_and_user_without_password_hash()
    {
        var client = factory.CreateClient();

        var response = await Login(client, DbSeeder.LeadEmail, DbSeeder.LeadPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);

        var login = JsonSerializer.Deserialize<LoginResponse>(raw, Json.Options)!;
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.Equal(DbSeeder.LeadEmail, login.User.Email);
        Assert.Equal(Api.Domain.Role.Lead, login.User.Role);
        var lifetime = login.ExpiresAt - DateTime.UtcNow;
        Assert.InRange(lifetime, TimeSpan.FromMinutes(59), TimeSpan.FromMinutes(61)); // 60-minute tokens
    }

    [Fact]
    public async Task Issued_token_is_HS256_and_carries_sub_email_name_and_role_claims()
    {
        var client = factory.CreateClient();
        var login = await (await Login(client, DbSeeder.Tester1Email, DbSeeder.Tester1Password)).ReadAsync<LoginResponse>();

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(login.AccessToken);

        Assert.Equal("HS256", jwt.Alg);
        Assert.Equal(login.User.Id.ToString(), jwt.Subject);
        Assert.Equal(DbSeeder.Tester1Email, jwt.GetClaim("email").Value);
        Assert.Equal("Tina Tester", jwt.GetClaim("name").Value);
        Assert.Equal("Tester", jwt.GetClaim("role").Value);
        Assert.Equal(BugBoardFactory.Issuer, jwt.Issuer);
        Assert.Contains(BugBoardFactory.Audience, jwt.Audiences);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_return_the_same_401_message()
    {
        var client = factory.CreateClient();

        var wrongPassword = await Login(client, DbSeeder.LeadEmail, "definitely-wrong");
        var unknownEmail = await Login(client, "nobody@bugboard.local", "definitely-wrong");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);

        var a = await wrongPassword.Content.ReadFromJsonAsync<JsonElement>();
        var b = await unknownEmail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(a.GetProperty("detail").GetString(), b.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Login_with_invalid_body_returns_400_with_per_field_errors()
    {
        var client = factory.CreateClient();

        var response = await Login(client, "not-an-email", "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("email", out _), "errors map should contain camelCase 'email'");
        Assert.True(errors.TryGetProperty("password", out _), "errors map should contain camelCase 'password'");
    }

    [Fact]
    public async Task Me_returns_the_signed_in_user()
    {
        var client = await factory.Tester2();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.ReadAsync<UserDto>();
        Assert.Equal(DbSeeder.Tester2Email, me.Email);
    }

    [Theory]
    [InlineData("GET", "/api/auth/me")]
    [InlineData("GET", "/api/users")]
    [InlineData("GET", "/api/bugs")]
    [InlineData("GET", "/api/bugs/1")]
    [InlineData("POST", "/api/bugs")]
    [InlineData("PUT", "/api/bugs/1")]
    [InlineData("DELETE", "/api/bugs/1")]
    public async Task Every_protected_endpoint_returns_401_without_a_token(string method, string url)
    {
        var client = factory.CreateClient();

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Each row breaks exactly ONE of the four things the API validates: lifetime, signing key, audience.
    // (issuer is covered by the same mechanism)
    public static TheoryData<string, string?, string?, bool> BadTokens => new()
    {
        // description,            key override,                              audience override, expired?
        { "expired",               null,                                      null,              true },
        { "signed with wrong key", "another-secret-key-that-is-32-bytes-long!!", null,           false },
        { "wrong audience",        null,                                      "someone-else",    false },
    };

    [Theory]
    [MemberData(nameof(BadTokens))]
    public async Task Invalid_tokens_are_rejected_with_401(string why, string? key, string? audience, bool expired)
    {
        _ = why; // only here so the test explorer shows which case ran
        var now = DateTime.UtcNow;
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = BugBoardFactory.Issuer,
            Audience = audience ?? BugBoardFactory.Audience,
            IssuedAt = expired ? now.AddHours(-3) : now,
            NotBefore = expired ? now.AddHours(-3) : now,
            Expires = expired ? now.AddHours(-2) : now.AddHours(1), // "expired" = ended 2 hours ago
            Subject = new ClaimsIdentity([new Claim("sub", "1"), new Claim("role", "Lead")]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key ?? BugBoardFactory.JwtKey)),
                SecurityAlgorithms.HmacSha256)
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/bugs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_tampered_token_is_rejected_with_401()
    {
        var client = await factory.Lead();
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        // Flip one character of the SIGNATURE (last part): the content is untouched but the signature no longer matches.
        var tampered = token[..^2] + (token[^2] == 'A' ? 'B' : 'A') + token[^1];
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        var response = await client.GetAsync("/api/bugs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Users_endpoint_never_exposes_password_hashes()
    {
        var client = await factory.Tester1();

        var raw = await client.GetStringAsync("/api/users");

        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(DbSeeder.LeadEmail, raw);
    }
}
