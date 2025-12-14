using System.Collections.Concurrent;

namespace Commons.PersistentCache.SQLite.Tests;

public class Factory : IPersistentCacheFactory
{
    private static ConcurrentDictionary<string, Cache> Cache { get; } = new();
    
    #region IPersistentCacheFactory

    public string? DefaultPath { get; set; }

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