using System.Text;
using System.Text.Json.Serialization;
using BugBoard.Api.Data;
using BugBoard.Api.Domain;
using BugBoard.Api.Infrastructure;
using BugBoard.Api.Configuration;
using BugBoard.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- Configuration (typed options)
// "Jwt" section -> JwtOptions. The secret Key is NOT in appsettings.json: it comes from
// `dotnet user-secrets` (local) or the Jwt__Key environment variable (servers/CI).
// ValidateOnStart = the app refuses to boot with a missing/weak key instead of failing at the first login.
builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .Validate(o => Encoding.UTF8.GetByteCount(o.Key) >= 32,
        "Jwt:Key is missing or shorter than 32 bytes. Set it with: dotnet user-secrets set \"Jwt:Key\" \"<random 32+ chars>\"")
    .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
        "Jwt:Issuer and Jwt:Audience must be set.")
    .ValidateOnStart();

// ---------------------------------------------------------------- Database (EF Core + SQLite)
builder.Services.AddDbContext<BugBoardDbContext>((sp, options) =>
    options.UseSqlite(sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")));

// ---------------------------------------------------------------- Authentication (JWT bearer)
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// Configured through IOptions so the key is read lazily (works with user-secrets AND with tests).
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        bearer.MapInboundClaims = false; // keep claim names exactly as issued: "sub", "role", ...
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30), // default is 5 min, which makes expiry feel "late"
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });

// ---------------------------------------------------------------- Authorization
builder.Services.AddAuthorizationBuilder()
    // FALLBACK policy: any endpoint WITHOUT its own [Authorize]/[AllowAnonymous] requires a signed-in user.
    // This is "secure by default": forgetting an attribute can never expose an endpoint.
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(Policies.LeadOnly, policy => policy.RequireRole(nameof(Role.Lead)));

// ---------------------------------------------------------------- CORS (only the Vite dev origin)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins(allowedOrigins)   // NOT AllowAnyOrigin
    .WithHeaders("Authorization", "Content-Type")
    .WithMethods("GET", "POST", "PUT", "DELETE")));

// ---------------------------------------------------------------- MVC, errors, docs
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ValidationResponseFactory.Create)
    .AddJsonOptions(o =>
        // Enums travel as text ("High"), and numbers like 7 are rejected.
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false)));

builder.Services.AddProblemDetails();                       // standard RFC 9457 error bodies
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

// ---------------------------------------------------------------- Dependency injection
builder.Services.AddSingleton(TimeProvider.System);          // injectable clock = testable time
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBugService, BugService>();

var app = builder.Build();

// ---------------------------------------------------------------- Database setup at startup
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BugBoardDbContext>();
    await db.Database.MigrateAsync(); // applies pending EF migrations (creates the SQLite file the first time)

    if (app.Environment.IsDevelopment())
    {
        await DbSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<TimeProvider>());
    }
}

// ---------------------------------------------------------------- HTTP pipeline (ORDER MATTERS)
app.UseExceptionHandler();   // 1st: catches anything thrown below -> 500 ProblemDetails
app.UseStatusCodePages();    // empty 401/403/404 responses become ProblemDetails too

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");     // before auth, so pre-flight (OPTIONS) requests succeed
app.UseAuthentication();     // "who are you?"  reads + validates the token  -> 401
app.UseAuthorization();      // "may you?"      checks policies/roles        -> 403

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    // The docs must be reachable without a token, otherwise you could never get one.
    app.MapOpenApi().AllowAnonymous();                  // /openapi/v1.json
    app.MapScalarApiReference().AllowAnonymous();       // /scalar/v1
}

app.Run();

// Makes Program visible to the test project (WebApplicationFactory<Program>).
public partial class Program;
