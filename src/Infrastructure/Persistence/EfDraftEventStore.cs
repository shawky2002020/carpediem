using System.Security.Cryptography;
using Carpediem.Infrastructure.Audit;
using Carpediem.Modules.Events.Application.Drafts;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Events.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Infrastructure.Persistence;

public sealed class EfDraftEventStore(CarpediemDbContext db) : IDraftEventStore
{
    public async Task<IReadOnlyList<Event>> ListAsync(int page, int pageSize,
        Guid actorUserId, CancellationToken cancellationToken)
    {
        await EnsureActiveActorAsync(actorUserId, cancellationToken);
        return await db.Events.AsNoTracking()
            .Where(eventItem => eventItem.Status == EventStatus.Draft)
            .OrderBy(eventItem => eventItem.StartDateTime)
            .ThenBy(eventItem => eventItem.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<Event> GetAsync(Guid eventId, Guid actorUserId, CancellationToken cancellationToken)
    {
        await EnsureActiveActorAsync(actorUserId, cancellationToken);
        var eventItem = await db.Events.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == eventId, cancellationToken)
            ?? throw new DraftNotFoundException();
        if (eventItem.Status != EventStatus.Draft) throw new NotDraftException();
        return eventItem;
    }

    public async Task<DraftCreateStoreResult> CreateAsync(
        Event draft,
        Guid actorUserId,
        Guid requestToken,
        byte[] fingerprint,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await EnsureActiveActorAsync(actorUserId, cancellationToken);
                var existing = await db.DraftCreateRequests.AsNoTracking()
                    .SingleOrDefaultAsync(intent => intent.ActorUserId == actorUserId &&
                        intent.RequestToken == requestToken, cancellationToken);
                if (existing is not null)
                {
                    return await ReplayAsync(existing, fingerprint, cancellationToken);
                }

                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                db.Events.Add(draft);
                db.AuditLogs.Add(EventAuditWriter.Created(draft, actorUserId, correlationId, draft.CreatedAt));
                db.DraftCreateRequests.Add(DraftCreateRequest.Create(actorUserId, requestToken,
                    fingerprint, draft.Id, draft.CreatedAt));
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new DraftCreateStoreResult(draft, false);
            });
        }
        catch (DbUpdateException error) when (IsDuplicateKey(error))
        {
            db.ChangeTracker.Clear();
            var existing = await db.DraftCreateRequests.AsNoTracking()
                .SingleOrDefaultAsync(intent => intent.ActorUserId == actorUserId &&
                    intent.RequestToken == requestToken, cancellationToken);
            if (existing is null) throw;
            return await ReplayAsync(existing, fingerprint, cancellationToken);
        }
    }

    public async Task<Event> UpdateAsync(
        Guid eventId,
        DraftEditValues values,
        byte[] expectedVersion,
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await EnsureActiveActorAsync(actorUserId, cancellationToken);
                var draft = await db.Events.Include(eventItem => eventItem.StaffingCycles)
                    .SingleOrDefaultAsync(eventItem => eventItem.Id == eventId, cancellationToken)
                    ?? throw new DraftNotFoundException();
                if (draft.Status != EventStatus.Draft) throw new NotDraftException();
                if (!draft.RowVersion.AsSpan().SequenceEqual(expectedVersion)) throw new StaleDraftException();

                var editedAt = DateTimeOffset.UtcNow;
                var changes = draft.EditDraft(values.Title, values.StartDateTime, values.EndDateTime,
                    values.Location, values.Notes, values.EffectiveEventDate, editedAt);
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                db.Entry(draft).Property(item => item.RowVersion).OriginalValue = expectedVersion;
                db.AuditLogs.Add(EventAuditWriter.Updated(draft, changes, actorUserId, correlationId, editedAt));
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return draft;
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new StaleDraftException();
        }
    }

    private async Task EnsureActiveActorAsync(Guid actorUserId, CancellationToken cancellationToken)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(user => user.Id == actorUserId && user.IsActive,
            cancellationToken))
        {
            throw new UnauthorizedAccessException("The actor is not active.");
        }
    }

    private async Task<DraftCreateStoreResult> ReplayAsync(
        DraftCreateRequest existing,
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        if (!CryptographicOperations.FixedTimeEquals(existing.PayloadFingerprint, fingerprint))
        {
            throw new DraftCreateConflictException();
        }

        var draft = await db.Events.AsNoTracking()
            .SingleAsync(eventItem => eventItem.Id == existing.EventId, cancellationToken);
        return new DraftCreateStoreResult(draft, true);
    }

    private static bool IsDuplicateKey(DbUpdateException error) =>
        error.InnerException is SqlException sql && sql.Number is 2601 or 2627;
}
