using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Google.Cloud.Firestore;
using Platform.Shared.Entities.Common;

namespace Platform.Api.Firestore;

/// <summary>
/// Who made a change and from where, for the audit log (P10).
/// </summary>
/// <param name="UserId">Acting user, or null for system work.</param>
/// <param name="OrgId">Acting organisation, if known.</param>
/// <param name="Ip">Caller's IP address.</param>
/// <param name="Device">Caller's user agent, or "system".</param>
public sealed record AuditActor(Guid? UserId, Guid? OrgId, string? Ip, string? Device);

/// <summary>
/// Builds the audit entry for one write by comparing the stored fields before
/// and after. Used only by <c>UnitOfWork</c>, so every save is audited without
/// any service having to remember it.
/// </summary>
public static class AuditBuilder
{
    /// <summary>Longest user agent kept.</summary>
    private const int MaxDeviceLength = 200;

    /// <summary>Fields never listed as changes: stamped on every write, or pure noise.</summary>
    private static readonly HashSet<string> IgnoredFields = new(StringComparer.Ordinal)
    {
        FirestoreNaming.Field(nameof(BaseEntity.CreatedAt)),
        FirestoreNaming.Field(nameof(BaseEntity.CreatedBy)),
        FirestoreNaming.Field(nameof(BaseEntity.UpdatedAt)),
        FirestoreNaming.Field(nameof(BaseEntity.UpdatedBy)),
        FirestoreNaming.Field(nameof(IOrgScoped.OrgId)),
        "search_terms",
    };

    /// <summary>Properties tried, in order, for the entry's label.</summary>
    private static readonly string[] LabelProperties = { "ReferenceNo", "Code", "Name", "Email" };

    /// <summary>Stored name of <see cref="ISoftDeletable.IsActive"/>.</summary>
    private static readonly string IsActiveField = FirestoreNaming.Field(nameof(ISoftDeletable.IsActive));

    /// <summary>
    /// Builds the audit entry for a write, or null when nothing needs recording
    /// (an entity that is not audited, no organisation, or an update that changed nothing).
    /// </summary>
    /// <param name="entity">Entity being written.</param>
    /// <param name="isNew">True for a create.</param>
    /// <param name="before">Stored fields when the entity was loaded; null if it was not loaded.</param>
    /// <param name="after">Stored fields being written.</param>
    /// <param name="actor">Who and from where.</param>
    /// <returns>The entry to save alongside the write, or null.</returns>
    public static AuditEntry? Build(
        BaseEntity entity,
        bool isNew,
        IReadOnlyDictionary<string, object?>? before,
        IReadOnlyDictionary<string, object?> after,
        AuditActor actor)
    {
        if (entity is INotAudited)
        {
            return null;
        }

        Guid? orgId = (entity as IOrgScoped)?.OrgId is { } own && own != Guid.Empty ? own : actor.OrgId;
        if (orgId is null)
        {
            return null;
        }

        List<AuditChange> changes = Diff(isNew ? null : before, after);
        if (!isNew && changes.Count == 0)
        {
            return null;
        }

        return new AuditEntry
        {
            OrgId = orgId.Value,
            Entity = entity.GetType().Name,
            EntityId = entity.Id,
            Action = ActionOf(isNew, before, after),
            Label = LabelOf(entity),
            Changes = changes,
            Ip = actor.Ip,
            Device = actor.Device is { Length: > MaxDeviceLength } device ? device[..MaxDeviceLength] : actor.Device,
            CreatedBy = actor.UserId,
        };
    }

    /// <summary>
    /// Lists the fields whose stored value differs.
    /// </summary>
    /// <param name="before">Old fields; null on create (every field is new).</param>
    /// <param name="after">New fields.</param>
    /// <returns>One change per differing field, in field order.</returns>
    private static List<AuditChange> Diff(IReadOnlyDictionary<string, object?>? before, IReadOnlyDictionary<string, object?> after)
    {
        var changes = new List<AuditChange>();
        foreach (var (field, value) in after.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            if (IgnoredFields.Contains(field))
            {
                continue;
            }

            string? newJson = ToJson(value);
            string? oldJson = before is not null && before.TryGetValue(field, out object? old) ? ToJson(old) : null;
            if (before is null || oldJson != newJson)
            {
                if (before is null && newJson is null)
                {
                    continue;
                }

                changes.Add(new AuditChange { Field = field, Before = before is null ? null : oldJson, After = newJson });
            }
        }

        return changes;
    }

    /// <summary>
    /// Works out what kind of change it was; a change of <c>is_active</c> is a deactivation or restore.
    /// </summary>
    /// <param name="isNew">True for a create.</param>
    /// <param name="before">Old fields.</param>
    /// <param name="after">New fields.</param>
    /// <returns>The action.</returns>
    private static AuditAction ActionOf(bool isNew, IReadOnlyDictionary<string, object?>? before, IReadOnlyDictionary<string, object?> after)
    {
        if (isNew)
        {
            return AuditAction.Created;
        }

        if (before?.GetValueOrDefault(IsActiveField) is bool wasActive && after.GetValueOrDefault(IsActiveField) is bool isActive && wasActive != isActive)
        {
            return isActive ? AuditAction.Restored : AuditAction.Deactivated;
        }

        return AuditAction.Updated;
    }

    /// <summary>
    /// A readable label for the record: its reference, code, name or email.
    /// </summary>
    /// <param name="entity">Entity.</param>
    /// <returns>The first non-empty of those, or an empty string.</returns>
    private static string LabelOf(BaseEntity entity)
    {
        Type type = entity.GetType();
        foreach (string name in LabelProperties)
        {
            if (type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(entity) is string { Length: > 0 } label)
            {
                return label;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Turns a stored Firestore value into compact JSON text.
    /// </summary>
    /// <param name="value">Stored value (primitive, timestamp, list or map).</param>
    /// <returns>JSON, or null for a null value.</returns>
    private static string? ToJson(object? value) => value is null ? null : ToNode(value)?.ToJsonString();

    /// <summary>
    /// Converts a stored value to a JSON node, turning timestamps into ISO 8601 text.
    /// </summary>
    /// <param name="value">Stored value.</param>
    /// <returns>The node.</returns>
    private static JsonNode? ToNode(object? value) => value switch
    {
        null => null,
        Timestamp timestamp => JsonValue.Create(timestamp.ToDateTime().ToString("O")),
        string text => JsonValue.Create(text),
        bool flag => JsonValue.Create(flag),
        IDictionary<string, object?> map => new JsonObject(map.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => KeyValuePair.Create(p.Key, ToNode(p.Value)))),
        System.Collections.IEnumerable items => new JsonArray(items.Cast<object?>().Select(ToNode).ToArray()),
        _ => JsonSerializer.SerializeToNode(value),
    };
}
