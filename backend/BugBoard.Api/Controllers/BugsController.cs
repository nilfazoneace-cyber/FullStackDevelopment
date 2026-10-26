using BugBoard.Api.Dtos;
using BugBoard.Api.Infrastructure;
using BugBoard.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BugBoard.Api.Controllers;

// No [Authorize] here on purpose: the global FALLBACK policy already requires a signed-in user
// for every endpoint that is not explicitly marked [AllowAnonymous].
[Route("api/bugs")]
public class BugsController(IBugService bugs) : ApiControllerBase
{
    /// <summary>Paged, filterable list of bugs.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<BugDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List([FromQuery] BugListQuery query, CancellationToken ct) =>
        Ok(await bugs.ListAsync(query, ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType<BugDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await bugs.GetAsync(id, ct);
        return result.Succeeded ? Ok(result.Value) : Failure(result);
    }

    [HttpPost]
    [ProducesResponseType<BugDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(CreateBugRequest request, CancellationToken ct)
    {
        var result = await bugs.CreateAsync(request, ct);
        if (!result.Succeeded)
        {
            return Failure(result);
        }
        // 201 Created + a Location header pointing at the new resource.
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<BugDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, UpdateBugRequest request, CancellationToken ct)
    {
        var result = await bugs.UpdateAsync(id, request, ct);
        return result.Succeeded ? Ok(result.Value) : Failure(result);
    }

    /// <summary>Lead only. A Tester gets 403 from the authorization policy before this code even runs.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.LeadOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await bugs.DeleteAsync(id, ct);
        return result.Succeeded ? NoContent() : Failure(result);
    }
}
