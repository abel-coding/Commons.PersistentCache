using System.Runtime.InteropServices.JavaScript;
using Commons.PersistentCache;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Commons.PersistentCache.IndexedDB;

/// <summary>
/// Persistent cache implementation that uses IndexedDB as the underlying storage.
/// </summary>
public class Cache : IPersistentCache
{
    private readonly string _cacheName;
    private readonly JsModuleInterop _interop;
    private PersistentCacheConfiguration? _globalConfiguration;
    private int _totalSizeInBytes = 0;
    private readonly Task _initializationTask;
    private int _isCleanUpRunning;
    private ILogger<Cache>? _logger;

    private bool IsCleanUpRunning
    {
        get => Interlocked.CompareExchange(ref _isCleanUpRunning, 0, 0) == 1;
        set => Interlocked.Exchange(ref _isCleanUpRunning, value ? 1 : 0);
    }

    
    /// <summary>
    /// Default constructor for the IndexedDB cache instance.
    /// </summary>
    /// <param name="jsRuntime">IJSRuntime runtime instance to use for the JS interop.</param>
    /// <param name="name">Cache name key</param>
    /// <param name="configuration">Cache configuration if needed.</param>
    /// <param name="logger">Logger used to log cache operations</param>
    /// <exception cref="ArgumentNullException">Name parameter should be non-null.</exception>
    public Cache(IJSRuntime jsRuntime, string name, PersistentCacheConfiguration? configuration = null, ILogger<Cache>? logger = null)
    {
        _logger = logger;
        
        _cacheName = name ?? throw new ArgumentNullException(nameof(name));

        _interop = new JsModuleInterop(jsRuntime, _cacheName);

        _globalConfiguration = configuration;
        _initializationTask = InitializeAsync(configuration);
    }

    private async Task InitializeAsync(PersistentCacheConfiguration? configuration,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _interop.InitializeDatabaseAsync(cancellationToken);
            if (result is null)
            {
                throw new InvalidOperationException($"Failed to initialize IndexedDB cache '{_cacheName}'");
            }

            _totalSizeInBytes = result.Value;

            if (configuration is { })
            {
                if (!await _interop.SaveConfigurationAsync(configuration, cancellationToken))
                {
                    throw new InvalidOperationException(
                        $"Failed to save configuration to IndexedDB cache '{_cacheName}'");
                }

                _globalConfiguration = configuration;
            }
            else
            {
                _globalConfiguration = await _interop.GetConfigurationAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to initialize IndexedDB cache '{_cacheName}'", ex);
        }
    }

    #region IPersistentCache

    /// <inheritdoc />
    public async Task<bool> SetConfigurationAsync(PersistentCacheConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _initializationTask;

            if (await _interop.SaveConfigurationAsync(configuration, cancellationToken))
            {
                _globalConfiguration = configuration;
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to set configuration error '{_cacheName}'", ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> CleanupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _initializationTask;

            var removedAmount = await _interop.CleanupDatabaseAsync(_globalConfiguration, cancellationToken);
            if (removedAmount is null) return false;

            _totalSizeInBytes -= removedAmount.Value;

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Cleanup error");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _initializationTask;

            return await _interop.GetEntry(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, $"Get error for key '{key}'");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveAsync(string key, byte[] data, CancellationToken cancellationToken = default)
    {
        return await SaveAsync(key, data, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> SaveAsync(
        string key,
        byte[] data,
        EntryConfiguration? configuration,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _initializationTask;

            var delta = await _interop.SaveEntry(key, data, configuration, cancellationToken);
            if (delta is null) return false;

            _totalSizeInBytes += delta.Value;

            _ = TriggerCleanUpIfNeeded();
            
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, $"Save error for key '{key}'");
            return false;
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _initializationTask;

            var delta = await _interop.RemoveEntry(key, cancellationToken);
            if (delta is null) return false;

            _totalSizeInBytes += delta.Value;

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, $"Remove error for key '{key}'");
            return false;
        }
    }

    #endregion
    
    #region Private

    private async Task TriggerCleanUpIfNeeded()
    {
        if (IsCleanUpRunning) return;
        if (_globalConfiguration?.MaximumCapacityInBytes is { } maximumCapacityInBytes &&
            _totalSizeInBytes > maximumCapacityInBytes)
        {
            if (IsCleanUpRunning) return;
            IsCleanUpRunning = true;
            try
            {
                await CleanupAsync();
            }
            finally
            {
                IsCleanUpRunning = false;
            }
        }
    }

    #endregion
}