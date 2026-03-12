using Commons.PersistentCache;
using Microsoft.JSInterop;

namespace Commons.PersistentCache.IndexedDB;

class JsModuleInterop
{
    private readonly string _cacheName;
    private IJSObjectReference? _jsModule;
    private readonly Task _loadTask;

    public JsModuleInterop(IJSRuntime jsRuntime, string cacheName)
    {
        _cacheName = cacheName;
        _loadTask = Load(jsRuntime);
    }
    
    private async Task Load(IJSRuntime jsRuntime, CancellationToken cancellationToken = default)
    {
        // Import Module
        _jsModule = await jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", cancellationToken, "./_content/Commons.PersistentCache.IndexedDB/indexeddb-cache.js");
    }

    public async Task<int?> InitializeDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await _loadTask;
        if (_jsModule == null) return null;

        return await _jsModule.InvokeAsync<int>("initializeDatabase", cancellationToken, _cacheName);
    }

    public async Task<bool> SaveConfigurationAsync(PersistentCacheConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await _loadTask;
        if (_jsModule == null) return false;
        
        var configurationData = new Dictionary<string, object?>
        {
            { "maximumCapacityInBytes", configuration.MaximumCapacityInBytes },
            { "timeToLiveInSeconds", configuration.TimeToLiveInSeconds },
            { "slidingTimeToLiveInSeconds", configuration.SlidingTimeToLiveInSeconds }
        };

        var result = await _jsModule.InvokeAsync<bool>(
            "saveConfiguration",
            cancellationToken,
            _cacheName,
            configurationData);
        
        return result;
    }

    public async Task<PersistentCacheConfiguration?> GetConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        await _loadTask;
        if (_jsModule == null) return null;


        var configurationData = await _jsModule.InvokeAsync<Dictionary<string, object?>?>(
            "getConfiguration",
            cancellationToken,
            _cacheName);

        if (configurationData == null) return null;

        PersistentCacheConfiguration result = new PersistentCacheConfiguration()
        {
            MaximumCapacityInBytes = configurationData["maximumCapacityInBytes"] as int? ?? 0,
            TimeToLiveInSeconds = configurationData["timeToLiveInSeconds"] as int? ?? 0,
            SlidingTimeToLiveInSeconds = configurationData["slidingTimeToLiveInSeconds"] as int? ?? 0
        };
        return result;
    }
    
    public async Task<int?> CleanupDatabaseAsync(PersistentCacheConfiguration? configuration, CancellationToken cancellationToken = default)
    {
        await _loadTask;
        if (_jsModule == null) return null;

        return await _jsModule.InvokeAsync<int>("cleanupDatabase", cancellationToken, _cacheName,
            configuration?.MaximumCapacityInBytes,
            configuration?.TimeToLiveInSeconds,
            configuration?.SlidingTimeToLiveInSeconds);
    }

    public async Task<byte[]?> GetEntry(string key, CancellationToken cancellationToken = default)
    {
        await _loadTask;
        if (_jsModule == null) return null;

        return await _jsModule.InvokeAsync<byte[]?>("getEntry", cancellationToken, _cacheName, key);
    }

    public async Task<int?> SaveEntry(string key, byte[] data, EntryConfiguration? configuration,
        CancellationToken cancellationToken = default)
    {
        await _loadTask;
        if (_jsModule == null) return null;

        Dictionary<string, object?>? configurationData = null;
        if (configuration != null)
        {
            configurationData = new Dictionary<string, object?>
            {
                { "timeToLiveInSeconds", configuration.TimeToLiveInSeconds },
                { "slidingTimeToLiveInSeconds", configuration.SlidingTimeToLiveInSeconds }
            };
        }

        return await _jsModule.InvokeAsync<int?>("saveEntry", cancellationToken, _cacheName, key, data, data.Length,
            configurationData);
    }

    public async Task<int?> RemoveEntry(string key, CancellationToken cancellationToken = default)
    {
        await _loadTask;
        if (_jsModule == null) return null;

        return await _jsModule.InvokeAsync<int?>("removeEntry", cancellationToken, _cacheName, key);
    }
}