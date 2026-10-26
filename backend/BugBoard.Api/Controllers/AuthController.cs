using BugBoard.Api.Dtos;
using BugBoard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BugBoard.Api.Controllers;

[Route("api/auth")]
public class AuthController(IAuthService auth, ICurrentUser currentUser) : ApiControllerBase
{
    /// <summary>Exchange email + password for a JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous] // the ONLY open endpoint: everything else needs a token (fallback policy)
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var response = await auth.LoginAsync(request, ct);
        if (response is null)
        {
            // One generic message for both "no such email" and "wrong password".
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "Invalid email or password.");
        }
        return Ok(response);
    }

    /// <summary>The currently signed-in user (from the token).</summary>
    [HttpGet("me")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var user = await auth.GetUserAsync(currentUser.Id, ct);
        // Valid token but the user was deleted afterwards: treat as not signed in.
        return user is null ? Unauthorized() : Ok(user);
    }
}
