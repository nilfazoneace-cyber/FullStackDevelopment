using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BugBoard.Api.Infrastructure;

/// <summary>
/// Builds the automatic 400 response for [ApiController] model-validation failures.
/// By default keys look like "Title" (property name) or "$.severity" (JSON path) and messages for
/// bad JSON values expose .NET type names. This makes keys consistent camelCase ("title", "severity")
/// to match the JSON the client sends, and the messages user-friendly.
/// </summary>
public static class ValidationResponseFactory
{
    public static IActionResult Create(ActionContext context)
    {
        var cleaned = new ModelStateDictionary();

        foreach (var (key, entry) in context.ModelState)
        {
            if (entry.Errors.Count == 0)
            {
                continue;
            }

            var field = ToCamelCase(key.StartsWith("$.") ? key[2..] : key);
            foreach (var error in entry.Errors)
            {
                var message = error.ErrorMessage.StartsWith("The JSON value could not be converted")
                    ? "The value is not valid."
                    : error.ErrorMessage;
                cleaned.AddModelError(field, message);
            }
        }

        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateValidationProblemDetails(context.HttpContext, cleaned, StatusCodes.Status400BadRequest);

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    // "AssignedToId" -> "assignedToId"; "Items[0].Name" -> "items[0].name"
    private static string ToCamelCase(string path) =>
        string.Join('.', path.Split('.').Select(part => part.Length == 0 ? part : char.ToLowerInvariant(part[0]) + part[1..]));
}
