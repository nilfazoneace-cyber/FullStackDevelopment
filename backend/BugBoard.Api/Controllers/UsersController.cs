using BugBoard.Api.Dtos;
using BugBoard.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BugBoard.Api.Controllers;

[Route("api/users")]
public class UsersController(IAuthService users) : ApiControllerBase
{
    /// <summary>All users, for the "assignee" dropdown.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await users.ListUsersAsync(ct));
}
