using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Google.Cloud.Firestore;
using Platform.Shared.Entities.Common;

namespace Platform.Api.Firestore;

/// <summary>
/// Converts any entity to a Firestore document and back, by convention. Entities
/// in <c>Platform.Shared</c> therefore stay plain classes with no Firestore
/// attributes, and a new entity needs no mapping code to be stored.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>The document id is <see cref="BaseEntity.Id"/>; it is not repeated as a field.</item>
/// <item>Field names come from <see cref="FirestoreNaming.Field"/> (snake_case).</item>
/// <item><see cref="Guid"/> is stored as a string.</item>
/// <item><see cref="DateTime"/> is stored as a UTC Timestamp.</item>
/// <item><see cref="decimal"/> (money, quantity) is stored as an invariant string so no
/// precision is lost; Firestore has no decimal type and <c>double</c> is banned for money.</item>
/// <item><see cref="DateOnly"/> is stored as <c>yyyy-MM-dd</c>; enums by name.</item>
/// </list>
/// </remarks>
public static class DocumentConverter
{
    private const string DateOnlyFormat = "yyyy-MM-dd";
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    /// <summary>
    /// Builds the field map to write for an entity.
    /// </summary>
    /// <param name="entity">Entity to store.</param>
    /// <returns>Field name to Firestore value, excluding the id.</returns>
    public static Dictionary<string, object?> ToDocument(BaseEntity entity)
    {
        var fields = new Dictionary<string, object?>();
        foreach (PropertyInfo property in GetStoredProperties(entity.GetType()))
        {
            fields[FirestoreNaming.Field(property.Name)] = ToFirestoreValue(property.GetValue(entity));
        }

        return fields;
    }

    /// <summary>
    /// Rebuilds an entity from a document snapshot.
    /// </summary>
    /// <typeparam name="TEntity">Entity type to create.</typeparam>
    /// <param name="snapshot">An existing document.</param>
    /// <returns>The populated entity.</returns>
    public static TEntity FromDocument<TEntity>(DocumentSnapshot snapshot) where TEntity : BaseEntity, new()
    {
        var entity = new TEntity { Id = Guid.Parse(snapshot.Id) };
        Dictionary<string, object> fields = snapshot.ToDictionary();

        foreach (PropertyInfo property in GetStoredProperties(typeof(TEntity)))
        {
            if (fields.TryGetValue(FirestoreNaming.Field(property.Name), out object? value))
            {
                property.SetValue(entity, FromFirestoreValue(value, property.PropertyType));
            }
        }

        return entity;
    }

    /// <summary>
    /// Converts one CLR value to the form Firestore stores. Use this for query
    /// arguments too, so a filter compares like with like.
    /// </summary>
    /// <param name="value">CLR value.</param>
    /// <returns>The Firestore-compatible value.</returns>
    /// <exception cref="NotSupportedException">The type has no storage rule.</exception>
    public static object? ToFirestoreValue(object? value) => value switch
    {
        null => null,
        string or bool or int or long or double => value,
        Guid guid => guid.ToString(),
        DateTime dateTime => Timestamp.FromDateTime(dateTime.Kind == DateTimeKind.Utc
            ? dateTime
            : DateTime.SpecifyKind(dateTime.ToUniversalTime(), DateTimeKind.Utc)),
        DateTimeOffset offset => Timestamp.FromDateTimeOffset(offset),
        DateOnly date => date.ToString(DateOnlyFormat, CultureInfo.InvariantCulture),
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        Enum enumValue => enumValue.ToString(),
        _ => throw new NotSupportedException($"No Firestore storage rule for type {value.GetType().Name}."),
    };

    /// <summary>
    /// Converts a stored Firestore value back to a CLR type.
    /// </summary>
    /// <param name="value">Value read from the document.</param>
    /// <param name="targetType">Property type to produce.</param>
    /// <returns>The converted value.</returns>
    private static object? FromFirestoreValue(object? value, Type targetType)
    {
        Type type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (value is null)
        {
            return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null
                ? Activator.CreateInstance(targetType)
                : null;
        }

        if (type == typeof(Guid)) return Guid.Parse((string)value);
        if (type == typeof(DateTime)) return ((Timestamp)value).ToDateTime();
        if (type == typeof(DateTimeOffset)) return ((Timestamp)value).ToDateTimeOffset();
        if (type == typeof(DateOnly)) return DateOnly.ParseExact((string)value, DateOnlyFormat, CultureInfo.InvariantCulture);
        if (type == typeof(decimal)) return decimal.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture);
        if (type.IsEnum) return Enum.Parse(type, (string)value);
        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Returns the public read/write properties stored for a type, excluding the id.
    /// Cached per type.
    /// </summary>
    /// <param name="type">Entity type.</param>
    /// <returns>The stored properties.</returns>
    private static PropertyInfo[] GetStoredProperties(Type type) =>
        PropertyCache.GetOrAdd(type, t => t
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.Name != nameof(BaseEntity.Id))
            .ToArray());
}
