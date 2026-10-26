using BugBoard.Api.Domain;

namespace BugBoard.Api.Services;

/// <summary>Who is making the current request? Read from the validated JWT, never from the request body.</summary>
public interface ICurrentUser
{
    int Id { get; }
    Role Role { get; }
    bool IsLead { get; }
}

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal Principal =>
        accessor.HttpContext?.User ?? throw new InvalidOperationException("No active HTTP request.");

    // "sub" (subject) is the standard JWT claim that holds the user id.
    public int Id => int.Parse(
        Principal.FindFirst("sub")?.Value ?? throw new InvalidOperationException("Token has no 'sub' claim."));

    public Role Role => Enum.Parse<Role>(
        Principal.FindFirst("role")?.Value ?? throw new InvalidOperationException("Token has no 'role' claim."));

    public bool IsLead => Role == Role.Lead;
}
