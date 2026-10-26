using BugBoard.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BugBoard.Api.Controllers;

/// <summary>Shared by all controllers: turns a failed <see cref="ServiceResult"/> into the right HTTP response.</summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult Failure(ServiceResult result) => result.Error switch
    {
        ServiceError.NotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: result.Message),
        ServiceError.Forbidden => Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: result.Message),
        ServiceError.Conflict => Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: result.Message),
        // 400 with a per-field "errors" map: same shape as automatic model validation errors.
        ServiceError.Validation => ValidationProblem(new ValidationProblemDetails(
            result.FieldErrors!.ToDictionary(kv => kv.Key, kv => kv.Value))),
        _ => throw new InvalidOperationException("Failure() called with a successful result.")
    };
}
