using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Kinevor.Modules.IdentityAccess.DemoApi;

/// <summary>
/// Documents both successful login payloads. ASP.NET Core keeps only one schema
/// when two <c>ProducesResponseType</c> attributes share the 200 status code, so
/// the filter replaces it with an explicit <c>oneOf</c>: a signed-in session or
/// the <c>mfa_required</c> challenge.
/// </summary>
public sealed class LoginResponseOperationFilter : IOperationFilter
{
    /// <summary>Adds the login union schema to the OpenAPI document.</summary>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var isLogin = string.Equals(
                context.ApiDescription.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
            && context.ApiDescription.RelativePath?.EndsWith(
                "auth/login", StringComparison.OrdinalIgnoreCase) == true;

        if (!isLogin || !operation.Responses.TryGetValue("200", out var response))
            return;

        response.Description = "A signed-in session, or an MFA challenge.";
        response.Content["application/json"] = new OpenApiMediaType
        {
            Schema = new OpenApiSchema
            {
                OneOf =
                [
                    SchemaReference("AuthSessionDtoApiResponse"),
                    SchemaReference("MfaChallengeDtoApiResponse"),
                ],
            },
        };
    }

    private static OpenApiSchema SchemaReference(string id) => new()
    {
        Reference = new OpenApiReference
        {
            Type = ReferenceType.Schema,
            Id = id,
        },
    };
}
