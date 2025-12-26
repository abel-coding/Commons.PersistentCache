using System.Collections.Concurrent;

namespace Commons.PersistentCache.SQLite.Tests;

public class TestBase : IAsyncLifetime
{
    private readonly ConcurrentBag<Cache> _usedCacheObjects = new ();

    protected void RegisterTestCache(Cache cache)
    {
        _usedCacheObjects.Add(cache);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Console.WriteLine(@"Removing test caches");
        while (_usedCacheObjects.TryTake(out var cache))
        {
            if (cache.IsDisposed) continue;
            var data = cache.GetCacheStateData();
            await cache.DisposeAsync();
            if (data?.DatabasePath is not { } databasePath) continue;
            try
            {
                Console.WriteLine($@"Deleting {databasePath}");
                File.Delete(databasePath);
            }
            catch
            {
                // ignored
            }
        }
    }
}