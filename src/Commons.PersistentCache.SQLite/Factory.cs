using System.Collections.Concurrent;

namespace Commons.PersistentCache.SQLite;

/// <summary>
/// Custom <see cref="IPersistentCacheFactory"/> that will build <see cref="Cache"/> instances.
/// </summary>
public class Factory : IPersistentCacheFactory
{
    private static ConcurrentDictionary<string, Cache> Cache { get; } = new();
    
    #region IPersistentCacheFactory

    /// <inheritdoc />
    public string? DefaultPath { get; set; }

    /// <inheritdoc />
    public IPersistentCache Create(string path, PersistentCacheConfiguration? configuration = null)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(path, out var cache))
            {
                if (!cache.IsDisposed) return cache;
                Cache.TryRemove(path, out cache);
            }

            if (DefaultPath is { } defaultPath)
            {
                path = Path.Combine(defaultPath, path);
            }

            cache = new Cache(path, configuration);

            Cache.TryAdd(path, cache);
            return cache;
        }
    }

    #endregion
}