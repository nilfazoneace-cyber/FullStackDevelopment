using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace BugBoard.Api.Infrastructure;

/// <summary>
/// Adds a "Bearer" security scheme to the OpenAPI document. Scalar then shows an
/// "Authentication" box where you paste the token returned by /api/auth/login.
/// </summary>
public class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Paste the accessToken from POST /api/auth/login (no 'Bearer ' prefix)."
            }
        };

        document.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            }
        ];

        return Task.CompletedTask;
    }
}
