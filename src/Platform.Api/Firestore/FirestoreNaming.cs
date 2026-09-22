using System.Collections.Concurrent;
using System.Text;

namespace Platform.Api.Firestore;

/// <summary>
/// Derives every Firestore collection and field name from the C# name, so a
/// name is chosen once (in C#) and the stored name can never drift from it.
/// <c>UpdatedAt</c> is always stored as <c>updated_at</c>; <c>Brand</c> always
/// lives in <c>brands</c>. See <c>.claude/rules/field-names.md</c>.
/// </summary>
public static class FirestoreNaming
{
    private static readonly ConcurrentDictionary<string, string> FieldCache = new();
    private static readonly ConcurrentDictionary<Type, string> CollectionCache = new();

    /// <summary>
    /// Converts a C# property name to its stored field name.
    /// Use with <c>nameof</c>: <c>FirestoreNaming.Field(nameof(Brand.Slug))</c>.
    /// </summary>
    /// <param name="propertyName">PascalCase property name.</param>
    /// <returns>The snake_case field name.</returns>
    public static string Field(string propertyName) => FieldCache.GetOrAdd(propertyName, ToSnakeCase);

    /// <summary>
    /// Returns the collection that stores <typeparamref name="TEntity"/>: the
    /// plural snake_case of the type name.
    /// </summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <returns>The collection name, e.g. <c>brands</c>.</returns>
    public static string Collection<TEntity>() => Collection(typeof(TEntity));

    /// <summary>
    /// Returns the collection that stores an entity type.
    /// </summary>
    /// <param name="entityType">Entity type.</param>
    /// <returns>The collection name.</returns>
    public static string Collection(Type entityType) =>
        CollectionCache.GetOrAdd(entityType, t => Pluralize(ToSnakeCase(t.Name)));

    /// <summary>
    /// Converts PascalCase to snake_case, treating digit runs and acronyms sensibly
    /// (<c>LogoUrl</c> → <c>logo_url</c>, <c>OrgId</c> → <c>org_id</c>).
    /// </summary>
    /// <param name="name">PascalCase name.</param>
    /// <returns>The snake_case name.</returns>
    private static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsUpper(c))
            {
                bool startsWord = i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])
                    || (i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(name[i - 1])));
                if (startsWord)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Pluralises the last word of a snake_case name with the regular English rules.
    /// </summary>
    /// <param name="name">Singular snake_case name.</param>
    /// <returns>The plural name.</returns>
    private static string Pluralize(string name)
    {
        if (name.EndsWith('y') && name.Length > 1 && !"aeiou".Contains(name[^2]))
        {
            return name[..^1] + "ies";
        }

        return name.EndsWith('s') || name.EndsWith('x') || name.EndsWith("ch") || name.EndsWith("sh")
            ? name + "es"
            : name + "s";
    }
}
