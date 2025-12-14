namespace Commons.PersistentCache;

/// <summary>
/// Defines a factory contract for creating and configuring <see cref="IPersistentCache"/> instances.
/// </summary>
/// <remarks>
/// This factory abstraction enables flexible creation of persistent cache instances with custom configurations.
/// It provides a centralized way to instantiate cache objects while maintaining loose coupling between components.
/// </remarks>
public interface IPersistentCacheFactory
{
    /// <summary>
    /// Gets the default file system path where persistent cache data is stored.
    /// </summary>
    string? DefaultPath { get; }
    
    /// <summary>
    /// Creates a new <see cref="IPersistentCache"/> instance with the specified path and optional configuration.
    /// </summary>
    /// <param name="path">The file system path where cache data will be persisted.</param>
    /// <param name="configuration">Optional configuration settings for the cache instance.</param>
    /// <returns>A new <see cref="IPersistentCache"/> instance configured for the specified path.</returns>
    IPersistentCache Create(string path, PersistentCacheConfiguration? configuration = null);
}