using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Carpediem.Web.Errors;

public sealed class AuthorizationProblemResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler fallback = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context,
        AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged)
        {
            await ApiProblemDetailsService.Problem(context, 401, "unauthenticated",
                "Authentication is required.").ExecuteAsync(context);
            return;
        }

        if (authorizeResult.Forbidden)
        {
            await ApiProblemDetailsService.Problem(context, 403, "forbidden",
                "Access is denied.").ExecuteAsync(context);
            return;
        }

        await fallback.HandleAsync(next, context, policy, authorizeResult);
    }
}
