using Microsoft.AspNetCore.Mvc;

namespace Carpediem.Web.Errors;

public static class ApiProblemDetailsService
{
    public static IResult Problem(
        HttpContext context,
        int status,
        string code,
        string title,
        string? detail = null,
        IReadOnlyDictionary<string, string[]>? errors = null,
        IReadOnlyDictionary<string, object?>? additionalExtensions = null)
    {
        var extensions = new Dictionary<string, object?>
        {
            ["code"] = code,
            ["traceId"] = context.TraceIdentifier,
        };

        if (errors is not null)
        {
            extensions["errors"] = errors;
        }

        if (additionalExtensions is not null)
        {
            foreach (var pair in additionalExtensions)
            {
                extensions[pair.Key] = pair.Value;
            }
        }

        return Results.Problem(
            statusCode: status,
            title: title,
            detail: detail,
            type: $"https://httpstatuses.com/{status}",
            extensions: extensions);
    }
}
