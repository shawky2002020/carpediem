using Carpediem.Modules.Identity.Domain;
using Carpediem.Web.Errors;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace Carpediem.Web.Endpoints;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/session").WithTags("Session");

        group.MapGet("/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            context.Response.Headers.CacheControl = "no-store";
            return TypedResults.Ok(new AntiforgeryResponse(tokens.RequestToken
                ?? throw new InvalidOperationException("Antiforgery request token was not created.")));
        });

        group.MapPost("/login", LoginAsync)
            .WithMetadata(new RequireAntiforgeryTokenAttribute());
        group.MapGet("/me", GetCurrentSessionAsync).RequireAuthorization();
        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .WithMetadata(new RequireAntiforgeryTokenAttribute());

        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        SignInManager<CarpediemUser> signInManager,
        UserManager<CarpediemUser> userManager)
    {
        if (!await HasValidAntiforgeryAsync(context, antiforgery))
        {
            return ApiProblemDetailsService.Problem(context, 400, "antiforgery_failed", "The request could not be verified.");
        }

        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrEmpty(request.Password))
        {
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "Username and password are required.");
        }

        var user = await userManager.FindByNameAsync(request.UserName);
        if (user is null || !user.IsActive)
        {
            return ApiProblemDetailsService.Problem(context, 401, "invalid_credentials", "The supplied credentials are invalid.");
        }

        var result = await signInManager.PasswordSignInAsync(user, request.Password, false, true);
        if (!result.Succeeded)
        {
            return ApiProblemDetailsService.Problem(context, 401, "invalid_credentials", "The supplied credentials are invalid.");
        }

        return TypedResults.Ok(await BuildSessionAsync(user, userManager));
    }

    private static async Task<IResult> GetCurrentSessionAsync(
        HttpContext context,
        UserManager<CarpediemUser> userManager)
    {
        var user = await userManager.GetUserAsync(context.User);
        return user is null || !user.IsActive
            ? ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.")
            : TypedResults.Ok(await BuildSessionAsync(user, userManager));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        SignInManager<CarpediemUser> signInManager)
    {
        if (!await HasValidAntiforgeryAsync(context, antiforgery))
        {
            return ApiProblemDetailsService.Problem(context, 400, "antiforgery_failed", "The request could not be verified.");
        }

        await signInManager.SignOutAsync();
        return TypedResults.NoContent();
    }

    private static async Task<bool> HasValidAntiforgeryAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (context.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: false })
        {
            return false;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static async Task<SessionResponse> BuildSessionAsync(
        CarpediemUser user,
        UserManager<CarpediemUser> userManager) =>
        new(user.Id, user.UserName ?? string.Empty, [.. await userManager.GetRolesAsync(user)]);

    private sealed record AntiforgeryResponse(string RequestToken);
    private sealed record LoginRequest(string UserName, string Password);
    private sealed record SessionResponse(Guid UserId, string UserName, string[] Roles);
}
