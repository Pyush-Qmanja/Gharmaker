using Google.Cloud.Firestore;
using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Repositories;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Entities.Common;
using Platform.Shared.Entities.Identity;

namespace Platform.Api.Services;

/// <summary>
/// Reads the audit log (P10). Entries are written by <c>UnitOfWork</c>; nothing
/// here writes, and there is no way to change or delete an entry.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Pages through the log, newest first.
    /// </summary>
    /// <param name="request">Kind of record, one record, paging.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One page.</returns>
    Task<PagedResult<AuditEntryDto>> GetPagedAsync(AuditListRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one entry with every changed field.
    /// </summary>
    /// <param name="id">Entry id.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The entry.</returns>
    Task<AuditEntryDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore-backed <see cref="IAuditService"/>.
/// </summary>
public sealed class AuditService : IAuditService
{
    private static readonly string EntityField = FirestoreNaming.Field(nameof(AuditEntry.Entity));
    private static readonly string EntityIdField = FirestoreNaming.Field(nameof(AuditEntry.EntityId));
    private static readonly string CreatedAtField = FirestoreNaming.Field(nameof(AuditEntry.CreatedAt));

    private readonly IRepository<AuditEntry> _entries;
    private readonly IRepository<User> _users;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="entries">Audit log data access.</param>
    /// <param name="users">User data access, for names.</param>
    public AuditService(IRepository<AuditEntry> entries, IRepository<User> users)
    {
        _entries = entries;
        _users = users;
    }

    /// <inheritdoc />
    public async Task<PagedResult<AuditEntryDto>> GetPagedAsync(AuditListRequest request, CancellationToken cancellationToken = default)
    {
        Query query = _entries.Query();
        if (!string.IsNullOrWhiteSpace(request.Entity))
        {
            query = query.WhereEqualTo(EntityField, request.Entity.Trim());
        }

        if (request.EntityId is { } entityId)
        {
            query = query.WhereEqualTo(EntityIdField, entityId.ToString());
        }

        var page = await _entries.GetPagedAsync(query.OrderByDescending(CreatedAtField), request, cancellationToken);
        IReadOnlyDictionary<Guid, string> names = await NamesAsync(page.Items.Select(e => e.CreatedBy), cancellationToken);
        return new PagedResult<AuditEntryDto>
        {
            Items = page.Items.Select(e => ToDto(e, names)).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
        };
    }

    /// <inheritdoc />
    public async Task<AuditEntryDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        AuditEntry entry = await _entries.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Audit entry");
        return ToDto(entry, await NamesAsync(new[] { entry.CreatedBy }, cancellationToken));
    }

    /// <summary>
    /// Builds an entry's read model.
    /// </summary>
    /// <param name="entry">Entry.</param>
    /// <param name="names">User names by id.</param>
    /// <returns>The read model.</returns>
    private static AuditEntryDto ToDto(AuditEntry entry, IReadOnlyDictionary<Guid, string> names) => new()
    {
        Id = entry.Id,
        CreatedAt = entry.CreatedAt,
        CreatedBy = entry.CreatedBy,
        Entity = entry.Entity,
        EntityId = entry.EntityId,
        Action = entry.Action,
        Label = entry.Label,
        Changes = entry.Changes.Select(c => new AuditChangeDto { Field = c.Field, Before = c.Before, After = c.After }).ToList(),
        Ip = entry.Ip,
        Device = entry.Device,
        CreatedByName = entry.CreatedBy is { } by ? names.GetValueOrDefault(by) : "System",
    };

    /// <summary>
    /// Names of users, for "who".
    /// </summary>
    /// <param name="ids">User ids (nulls ignored).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Name by id.</returns>
    private async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        List<Guid> wanted = ids.OfType<Guid>().Distinct().ToList();
        return wanted.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _users.GetByIdsAsync(wanted, cancellationToken)).ToDictionary(u => u.Id, u => u.Name);
    }
}
