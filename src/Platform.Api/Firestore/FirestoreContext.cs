using Google.Cloud.Firestore;
using Platform.Shared.Entities.Common;

namespace Platform.Api.Firestore;

/// <summary>
/// The single entry point to the Firestore database. Holds the one
/// <see cref="FirestoreDb"/> client for the process and hands out collections by
/// entity type. Application code uses <c>IRepository&lt;T&gt;</c>; only the data
/// layer, sign-in and seeding touch this directly.
/// </summary>
public interface IFirestoreContext
{
    /// <summary>The underlying client, for batches and transactions.</summary>
    FirestoreDb Database { get; }

    /// <summary>
    /// Returns the collection that stores <typeparamref name="TEntity"/>.
    /// </summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <returns>The collection reference.</returns>
    CollectionReference Collection<TEntity>() where TEntity : BaseEntity;
}

/// <summary>
/// Default <see cref="IFirestoreContext"/>. Registered as a singleton: the
/// Firestore client is thread-safe and meant to be reused.
/// </summary>
public sealed class FirestoreContext : IFirestoreContext
{
    /// <summary>
    /// Creates the context around an already-built client.
    /// </summary>
    /// <param name="database">Firestore client.</param>
    public FirestoreContext(FirestoreDb database)
    {
        Database = database;
    }

    /// <inheritdoc />
    public FirestoreDb Database { get; }

    /// <inheritdoc />
    public CollectionReference Collection<TEntity>() where TEntity : BaseEntity =>
        Database.Collection(FirestoreNaming.Collection<TEntity>());
}
