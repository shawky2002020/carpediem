using System.Data;
using Carpediem.Infrastructure.Audit;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Identity.Domain;
using Carpediem.Web.Errors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Web.Endpoints;

internal static class AccountQueries
{
    public static async Task<IResult> ListAsync(
        int? page, string? search, string[]? roles, string? status, string? view, string? sort,
        [FromHeader(Name = "X-Account-Search")] string? encodedSearch,
        HttpContext context, CarpediemDbContext db, ILookupNormalizer normalizer, CancellationToken cancellationToken)
    {
        const int pageSize = 20;
        var pageNumber = page ?? 1;
        if (pageNumber is < 1 or > 10000)
            return ApiProblemDetailsService.Problem(context, 400, "invalid_page", "Page must be between 1 and 10000.");

        if (encodedSearch?.Length > 3072)
            return ApiProblemDetailsService.Problem(context, 400, "invalid_account_query", "Search must be at most 256 characters.");
        search = (encodedSearch is null ? search : Uri.UnescapeDataString(encodedSearch))?.Trim();
        roles ??= [];
        var review = view == "review";
        sort ??= review ? "oldest" : "name";
        if (search?.Length > 256 || roles.Length > CarpediemRoles.All.Length ||
            roles.Any(role => !CarpediemRoles.All.Contains(role)) || view is not (null or "all" or "review") ||
            status is not (null or "active" or "inactive" or "pending") || (review && status is not null) ||
            sort is not ("name" or "name-desc" or "oldest") || (!review && sort == "oldest"))
        {
            return ApiProblemDetailsService.Problem(context, 400, "invalid_account_query", "Check the account search, filters, and sort order.");
        }

        context.Response.Headers.CacheControl = "no-store";
        return await ReadConsistentlyAsync(db, () => ReadPageAsync(db, normalizer, search, roles, status, review,
            sort, pageNumber, pageSize, cancellationToken), cancellationToken);
    }

    private static async Task<IResult> ReadPageAsync(CarpediemDbContext db, ILookupNormalizer normalizer,
        string? search, string[] roles, string? status, bool review, string sort, int pageNumber, int pageSize,
        CancellationToken cancellationToken)
    {
        // RequireAdmin scopes this directory to current active administrators.
        var directory = Directory(db);
        var totalAccounts = await db.Users.CountAsync(cancellationToken);
        var reviewCount = await directory.CountAsync(row => row.PendingSince != null, cancellationToken);
        var matches = status switch
        {
            "active" => directory.Where(row => row.User.IsActive),
            "inactive" => directory.Where(row => !row.User.IsActive && row.PendingSince == null),
            "pending" => directory.Where(row => row.PendingSince != null),
            _ when review => directory.Where(row => row.PendingSince != null),
            _ => directory,
        };
        if (!string.IsNullOrEmpty(search))
        {
            var name = normalizer.NormalizeName(search)!;
            var email = normalizer.NormalizeEmail(search)!;
            matches = matches.Where(row => (row.User.NormalizedUserName != null && row.User.NormalizedUserName.Contains(name)) ||
                (row.User.NormalizedEmail != null && row.User.NormalizedEmail.Contains(email)));
        }
        if (roles.Length > 0)
        {
            var selected = from membership in db.UserRoles
                           join role in db.Roles on membership.RoleId equals role.Id
                           where roles.Contains(role.Name!)
                           select membership.UserId;
            matches = matches.Where(row => selected.Contains(row.User.Id));
        }

        var matchingAccounts = await matches.CountAsync(cancellationToken);
        pageNumber = Math.Min(pageNumber, Math.Max(1, (matchingAccounts + pageSize - 1) / pageSize));
        var ordered = sort switch
        {
            "oldest" => matches.OrderBy(row => row.PendingSince),
            "name-desc" => matches.OrderByDescending(row => row.User.NormalizedUserName ?? row.User.NormalizedEmail),
            _ => matches.OrderBy(row => row.User.NormalizedUserName ?? row.User.NormalizedEmail),
        };
        var rows = await ordered.ThenBy(row => row.User.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(row => new { row.User.Id, row.User.UserName, row.User.Email, row.User.IsActive, row.PendingSince })
            .ToListAsync(cancellationToken);
        var assigned = await ReadRolesAsync(db, rows.Select(row => row.Id).ToArray(), cancellationToken);
        var items = rows.Select(row => new AccountSummary(row.Id, row.UserName ?? "", row.Email ?? "", row.IsActive,
            assigned.GetValueOrDefault(row.Id) ?? [], Status(row.IsActive, row.PendingSince), row.PendingSince)).ToArray();
        return Results.Ok(new AccountPage(items, pageNumber, pageSize, pageNumber * pageSize < matchingAccounts,
            totalAccounts, reviewCount, matchingAccounts));
    }

    public static async Task<IResult> DetailAsync(
        Guid userId, HttpContext context, CarpediemDbContext db, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        return await ReadConsistentlyAsync(db, () => ReadDetailAsync(userId, context, db, cancellationToken), cancellationToken);
    }

    private static async Task<IResult> ReadDetailAsync(
        Guid userId, HttpContext context, CarpediemDbContext db, CancellationToken cancellationToken)
    {
        var row = await Directory(db).SingleOrDefaultAsync(item => item.User.Id == userId, cancellationToken);
        if (row is null)
            return ApiProblemDetailsService.Problem(context, 404, "account_not_found", "Account not found.");

        var roles = await ReadRolesAsync(db, [userId], cancellationToken);
        var history = await db.AuditLogs.AsNoTracking().Where(log => log.ResourceType == "User" && log.ResourceId == userId &&
                (log.Action == AuditLog.RegistrationRequested || log.Action == AuditLog.AccountApproved ||
                 log.Action == AuditLog.AccountDeactivated || log.Action == AuditLog.AccountRolesChanged))
            .OrderByDescending(log => log.OccurredAt).ThenBy(log => log.Id).Take(10)
            .Select(log => new AccountHistoryEntry(log.Action, log.OccurredAt)).ToArrayAsync(cancellationToken);
        var user = row.User;
        return Results.Ok(new AccountDetail(userId, user.UserName ?? "", user.Email ?? "", user.IsActive,
            roles.GetValueOrDefault(userId) ?? [], Status(user.IsActive, row.PendingSince), row.PendingSince,
            user.ConcurrencyStamp, history));
    }

    private static Task<IResult> ReadConsistentlyAsync(CarpediemDbContext db, Func<Task<IResult>> read,
        CancellationToken cancellationToken) => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        // Keep the counts, page, and assigned roles on the same account state across statements.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var result = await read();
        await transaction.CommitAsync(cancellationToken);
        return result;
    });

    private static IQueryable<DirectoryRow> Directory(CarpediemDbContext db)
    {
        var accountAudit = db.AuditLogs.AsNoTracking().Where(log => log.ResourceType == "User");
        return db.Users.AsNoTracking().Select(user => new DirectoryRow
        {
            User = user,
            PendingSince = !user.IsActive && !accountAudit.Any(log => log.ResourceId == user.Id &&
                (log.Action == AuditLog.AccountApproved || log.Action == AuditLog.AccountDeactivated))
                ? accountAudit.Where(log => log.ResourceId == user.Id && log.Action == AuditLog.RegistrationRequested)
                    .OrderBy(log => log.OccurredAt).Select(log => (DateTimeOffset?)log.OccurredAt).FirstOrDefault()
                : null,
        });
    }

    private static async Task<Dictionary<Guid, string[]>> ReadRolesAsync(
        CarpediemDbContext db, Guid[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0) return [];
        var assigned = await (from membership in db.UserRoles.AsNoTracking()
                              join role in db.Roles.AsNoTracking() on membership.RoleId equals role.Id
                              where ids.Contains(membership.UserId) && role.Name != null
                              orderby role.Name
                              select new { membership.UserId, role.Name }).ToListAsync(cancellationToken);
        return assigned.GroupBy(row => row.UserId)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Name!).ToArray());
    }

    private static string Status(bool isActive, DateTimeOffset? pendingSince) =>
        isActive ? "active" : pendingSince.HasValue ? "pending" : "inactive";

    private sealed class DirectoryRow
    {
        public required CarpediemUser User { get; init; }
        public DateTimeOffset? PendingSince { get; init; }
    }
    internal sealed record AccountSummary(Guid UserId, string UserName, string Email, bool IsActive, string[] Roles,
        string Status, DateTimeOffset? PendingSince);
    internal sealed record AccountPage(AccountSummary[] Items, int Page, int PageSize, bool HasMore,
        int TotalAccounts, int ReviewCount, int MatchingAccounts);
    internal sealed record AccountDetail(Guid UserId, string UserName, string Email, bool IsActive, string[] Roles,
        string Status, DateTimeOffset? PendingSince, string? Version, AccountHistoryEntry[] History);
    internal sealed record AccountHistoryEntry(string Action, DateTimeOffset OccurredAt);
}
