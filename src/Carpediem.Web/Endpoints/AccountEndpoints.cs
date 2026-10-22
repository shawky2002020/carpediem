using System.Security.Claims;
using Carpediem.Infrastructure.Audit;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Identity.Domain;
using Carpediem.Web.Errors;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Web.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/accounts/register", RegisterAsync)
            .WithTags("Accounts")
            .WithName("RegisterLeader")
            .RequireRateLimiting("public-registration")
            .WithMetadata(new RequireAntiforgeryTokenAttribute())
            .Produces(202);

        var admin = endpoints.MapGroup("/api/admin/accounts")
            .WithTags("Account Administration")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);
        admin.MapGet("/", ListAsync).WithName("ListAccounts")
            .Produces<AccountPage>(200);
        admin.MapPost("/{userId:guid}/approve", ApproveAsync)
            .WithName("ApproveAccount")
            .WithMetadata(new RequireAntiforgeryTokenAttribute());
        admin.MapPost("/{userId:guid}/deactivate", DeactivateAsync)
            .WithName("DeactivateAccount")
            .WithMetadata(new RequireAntiforgeryTokenAttribute());
        admin.MapPut("/{userId:guid}/roles", SetRolesAsync)
            .WithName("SetAccountRoles")
            .WithMetadata(new RequireAntiforgeryTokenAttribute());
        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request, HttpContext context, IAntiforgery antiforgery,
        UserManager<CarpediemUser> users, RoleManager<IdentityRole<Guid>> roles,
        CarpediemDbContext db, TimeProvider clock)
    {
        if (!await ValidAntiforgeryAsync(context, antiforgery)) return BadAntiforgery(context);
        if (request is null || string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return ApiProblemDetailsService.Problem(context, 400, "invalid_registration", "Username, email, and password are required.");
        var userName = request.UserName.Trim();
        var email = request.Email.Trim();
        if (userName.Length > 100 || email.Length > 256)
            return ApiProblemDetailsService.Problem(context, 400, "invalid_registration", "Registration details are too long.");
        if (userName.Any(character => !char.IsAsciiLetterOrDigit(character) &&
            character is not ('-' or '.' or '_' or '@' or '+')))
            return ApiProblemDetailsService.Problem(context, 400, "invalid_registration",
                "Registration details did not meet account requirements.");

        if (!await roles.RoleExistsAsync(CarpediemRoles.Leader))
        {
            var roleResult = await roles.CreateAsync(new IdentityRole<Guid>(CarpediemRoles.Leader));
            if (!roleResult.Succeeded && !await roles.RoleExistsAsync(CarpediemRoles.Leader))
                return ApiProblemDetailsService.Problem(context, 503, "registration_unavailable", "Registration is temporarily unavailable.");
        }

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var user = new CarpediemUser
            {
                Id = Guid.NewGuid(), UserName = userName, Email = email,
                IsActive = false, EmailConfirmed = false,
            };
            var created = await users.CreateAsync(user, request.Password);
            if (!created.Succeeded)
            {
                if (created.Errors.All(error => error.Code is "DuplicateUserName" or "DuplicateEmail"))
                    return RegistrationAccepted();
                return ApiProblemDetailsService.Problem(context, 400, "invalid_registration",
                    "Registration details did not meet account requirements.");
            }
            var assigned = await users.AddToRoleAsync(user, CarpediemRoles.Leader);
            if (!assigned.Succeeded)
                return ApiProblemDetailsService.Problem(context, 503, "registration_unavailable", "Registration is temporarily unavailable.");
            db.AuditLogs.Add(AuditLog.AccountRegistrationRequested(user.Id, clock.GetUtcNow()));
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return RegistrationAccepted();
        });
    }

    private static async Task<IResult> ListAsync(
        int? page, HttpContext context, CarpediemDbContext db,
        CancellationToken cancellationToken)
    {
        const int pageSize = 50;
        var pageNumber = page ?? 1;
        if (pageNumber is < 1 or > 10000)
            return ApiProblemDetailsService.Problem(context, 400, "invalid_page", "Page must be between 1 and 10000.");
        context.Response.Headers.CacheControl = "no-store";
        var accounts = await db.Users.AsNoTracking().OrderBy(user => user.IsActive)
            .ThenBy(user => user.UserName).ThenBy(user => user.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize + 1).ToListAsync(cancellationToken);
        var hasMore = accounts.Count > pageSize;
        if (hasMore) accounts.RemoveAt(pageSize);
        var ids = accounts.Select(user => user.Id).ToArray();
        var roleRows = await (from userRole in db.UserRoles.AsNoTracking()
                              join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                              where ids.Contains(userRole.UserId)
                              select new { userRole.UserId, role.Name }).ToListAsync(cancellationToken);
        var rolesByUser = roleRows.GroupBy(row => row.UserId)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Name ?? string.Empty).ToArray());
        var result = accounts.Select(account => new AccountSummary(account.Id,
            account.UserName ?? string.Empty, account.Email ?? string.Empty, account.IsActive,
            rolesByUser.GetValueOrDefault(account.Id) ?? [])).ToArray();
        return Results.Ok(new AccountPage(result, pageNumber, pageSize, hasMore));
    }

    private static Task<IResult> ApproveAsync(
        Guid userId, HttpContext context, IAntiforgery antiforgery,
        UserManager<CarpediemUser> users, CarpediemDbContext db, TimeProvider clock) =>
        ChangeActiveAsync(userId, true, context, antiforgery, users, db, clock);

    private static Task<IResult> DeactivateAsync(
        Guid userId, HttpContext context, IAntiforgery antiforgery,
        UserManager<CarpediemUser> users, CarpediemDbContext db, TimeProvider clock) =>
        ChangeActiveAsync(userId, false, context, antiforgery, users, db, clock);

    private static async Task<IResult> ChangeActiveAsync(
        Guid userId, bool active, HttpContext context, IAntiforgery antiforgery,
        UserManager<CarpediemUser> users, CarpediemDbContext db, TimeProvider clock)
    {
        if (!await ValidAntiforgeryAsync(context, antiforgery)) return BadAntiforgery(context);
        if (!TryGetActor(context, out var adminId)) return Unauthenticated(context);
        if (!active && userId == adminId)
            return ApiProblemDetailsService.Problem(context, 409, "self_deactivation_forbidden", "Admins cannot deactivate their own account.");
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound(context);
        if (user.IsActive == active) return Results.Ok(new AccountStateResponse(user.Id, active));

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            user.IsActive = active;
            var updated = await users.UpdateAsync(user);
            if (!updated.Succeeded) return ApiProblemDetailsService.Problem(context, 409, "account_update_failed", "Account could not be updated.");
            if (!(await users.UpdateSecurityStampAsync(user)).Succeeded)
                return ApiProblemDetailsService.Problem(context, 409, "account_update_failed", "Account session could not be updated.");
            db.AuditLogs.Add(AuditLog.AccountChanged(user.Id, adminId, Guid.NewGuid(), clock.GetUtcNow(),
                active ? "AccountApproved" : "AccountDeactivated", (!active).ToString(), active.ToString()));
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Results.Ok(new AccountStateResponse(user.Id, active));
        });
    }

    private static async Task<IResult> SetRolesAsync(
        Guid userId, SetRolesRequest request, HttpContext context, IAntiforgery antiforgery,
        UserManager<CarpediemUser> users, RoleManager<IdentityRole<Guid>> roles,
        CarpediemDbContext db, TimeProvider clock)
    {
        if (!await ValidAntiforgeryAsync(context, antiforgery)) return BadAntiforgery(context);
        if (!TryGetActor(context, out var adminId)) return Unauthenticated(context);
        var desired = request?.Roles?.Distinct(StringComparer.Ordinal).ToArray();
        if (desired is null || desired.Length == 0 || desired.Any(role => !CarpediemRoles.All.Contains(role)))
            return ApiProblemDetailsService.Problem(context, 400, "invalid_roles", "Choose at least one recognized role.");
        if (userId == adminId && !desired.Contains(CarpediemRoles.Admin))
            return ApiProblemDetailsService.Problem(context, 409, "self_demotion_forbidden", "Admins cannot remove their own Admin role.");
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound(context);
        var current = await users.GetRolesAsync(user);
        foreach (var role in desired)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                var result = await roles.CreateAsync(new IdentityRole<Guid>(role));
                if (!result.Succeeded && !await roles.RoleExistsAsync(role))
                    return ApiProblemDetailsService.Problem(context, 503, "account_update_failed", "Roles are temporarily unavailable.");
            }
        }
        var remove = current.Except(desired).ToArray();
        var add = desired.Except(current).ToArray();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            if (remove.Length > 0 && !(await users.RemoveFromRolesAsync(user, remove)).Succeeded)
                return ApiProblemDetailsService.Problem(context, 409, "account_update_failed", "Roles could not be updated.");
            if (add.Length > 0 && !(await users.AddToRolesAsync(user, add)).Succeeded)
                return ApiProblemDetailsService.Problem(context, 409, "account_update_failed", "Roles could not be updated.");
            if (remove.Length > 0 || add.Length > 0)
            {
                if (!(await users.UpdateSecurityStampAsync(user)).Succeeded)
                    return ApiProblemDetailsService.Problem(context, 409, "account_update_failed", "Account session could not be updated.");
                db.AuditLogs.Add(AuditLog.AccountChanged(user.Id, adminId, Guid.NewGuid(), clock.GetUtcNow(),
                    "AccountRolesChanged", string.Join(',', current.Order()), string.Join(',', desired.Order())));
                await db.SaveChangesAsync();
            }
            await transaction.CommitAsync();
            return Results.Ok(new AccountRolesResponse(user.Id, desired));
        });
    }

    private static async Task<bool> ValidAntiforgeryAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (context.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: false }) return false;
        try { await antiforgery.ValidateRequestAsync(context); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
    private static bool TryGetActor(HttpContext context, out Guid id) =>
        Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out id);
    private static IResult BadAntiforgery(HttpContext context) => ApiProblemDetailsService.Problem(context, 400, "antiforgery_failed", "The request could not be verified.");
    private static IResult Unauthenticated(HttpContext context) => ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");
    private static IResult NotFound(HttpContext context) => ApiProblemDetailsService.Problem(context, 404, "account_not_found", "Account not found.");
    private static IResult RegistrationAccepted() => Results.Accepted(value: new
    { message = "Registration received. An Admin must approve the account before sign-in." });

    private sealed record RegisterRequest(string UserName, string Email, string Password);
    private sealed record SetRolesRequest(string[] Roles);
    private sealed record AccountSummary(Guid UserId, string UserName, string Email, bool IsActive, string[] Roles);
    private sealed record AccountPage(AccountSummary[] Items, int Page, int PageSize, bool HasMore);
    private sealed record AccountStateResponse(Guid UserId, bool IsActive);
    private sealed record AccountRolesResponse(Guid UserId, string[] Roles);
}
