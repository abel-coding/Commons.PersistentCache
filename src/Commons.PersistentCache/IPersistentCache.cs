namespace Commons.PersistentCache;

/// <summary>
/// Defines a contract for persistent cache operations, providing methods to store, retrieve, and manage cached data
/// with configuration and lifecycle management capabilities.
/// </summary>
public interface IPersistentCache
{
    /// <summary>
    /// Updates the existing configuration for the PersistentCache
    /// </summary>
    /// <param name="configuration">Global or per cache configuration to use.</param>
    /// <param name="cancellationToken">Asynchronous CancellationToken.</param>
    /// <returns>Whether this operation ended successfully.</returns>
    Task<bool> SetConfigurationAsync(PersistentCacheConfiguration configuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a cleanup process for the cache. Expiring elements should be removed and the size should be enforced. 
    /// </summary>
    /// <param name="cancellationToken">Asynchronous CancellationToken.</param>
    /// <returns>Whether this operation ended successfully.</returns>
    Task<bool> CleanupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve a cached content with a given key.
    /// </summary>
    /// <param name="key">The associated key that will be used for the search.</param>
    /// <param name="cancellationToken">Asynchronous CancellationToken.</param>
    /// <returns>Raw data representing the content in the form byte[] if available</returns>
    Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a given content with an associated cache key. This entry will be managed by the global configuration.
    /// </summary>
    /// <param name="key">Associated cache key to be used.</param>
    /// <param name="data">Raw data representation for the content to be stored.</param>
    /// <param name="cancellationToken">Asynchronous CancellationToken.</param>
    /// <returns>Whether this operation ended successfully.</returns>
    Task<bool> SaveAsync(string key, byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a given content with an associated cache key. It will allow to provide a per entry configuration that will
    /// help to have finer grain over how this entry is managed. It will take precedence over the global configuration.
    /// </summary>
    /// <param name="key">Associated cache key to be used.</param>
    /// <param name="data">Raw data representation for the content to be stored.</param>
    /// <param name="configuration">Per entry configuration to be used.</param>
    /// <param name="cancellationToken">Asynchronous CancellationToken.</param>
    /// <returns>Whether this operation ended successfully.</returns>
    Task<bool> SaveAsync(string key, byte[] data, EntryConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a given entry by providing the associated key.
    /// </summary>
    /// <param name="key">Associated cache key to be used.</param>
    /// <param name="cancellationToken">Asynchronous CancellationToken.</param>
    /// <returns>Whether this operation ended successfully.</returns>
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);
}