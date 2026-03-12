using Commons.PersistentCache;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Commons.PersistentCache.IndexedDB;

/// <summary>
/// Custom <see cref="IPersistentCacheFactory"/> that will build <see cref="Cache"/> instances.
/// </summary>
public class Factory : IPersistentCacheFactory
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILoggerFactory? _loggerFactory;

    /// <summary>
    /// Default constructor with optional logger factory.
    /// </summary>
    /// <param name="jsRuntime">IJSRuntime instance needed for JS Interop.</param>
    /// <param name="loggerFactory">Factory used to create logger instances for its caches.</param>
    public Factory(IJSRuntime jsRuntime, ILoggerFactory? loggerFactory = null)
    {
        _jsRuntime = jsRuntime;
        _loggerFactory = loggerFactory;
    }

    private static readonly Dictionary<string, IPersistentCache> Caches = new();
    
    /// <inheritdoc />
    public IPersistentCache Create(string path, PersistentCacheConfiguration? configuration = null)
    {
        lock (Caches)
        {
            if (Caches.TryGetValue(path, out var cache))
            {
                if (configuration != null)
                {
                    _ = cache.SetConfigurationAsync(configuration);
                }

                return cache;
            }

            var newCache = new Cache(_jsRuntime, path, configuration, _loggerFactory?.CreateLogger<Cache>());
            Caches[path] = newCache;
            return newCache;
        }
    }

    /// <inheritdoc />
    public string? DefaultPath { get; }
}