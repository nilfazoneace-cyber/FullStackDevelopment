namespace BugBoard.Api.Configuration;

/// <summary>
/// Strongly-typed view of the "Jwt" configuration section.
/// Issuer/Audience/ExpiryMinutes live in appsettings.json (not secret).
/// Key is a SECRET: it comes from "dotnet user-secrets" or the Jwt__Key environment variable.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}
