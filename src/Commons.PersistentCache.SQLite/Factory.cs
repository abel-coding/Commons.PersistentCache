using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Commons.PersistentCache.SQLite;

/// <summary>
/// Custom <see cref="IPersistentCacheFactory"/> that will build <see cref="Cache"/> instances.
/// </summary>
public class Factory : IPersistentCacheFactory
{
    private readonly ILoggerFactory? _loggerFactory;
    private static ConcurrentDictionary<string, Cache> Cache { get; } = new();

    /// <summary>
    /// Default constructor with optional logger factory.
    /// </summary>
    /// <param name="loggerFactory">Factory used to create logger instances for its caches</param>
    public Factory(ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory;
    }

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

            cache = new Cache(path, configuration, _loggerFactory?.CreateLogger<Cache>());

            Cache.TryAdd(path, cache);
            return cache;
        }
    }

    #endregion
}