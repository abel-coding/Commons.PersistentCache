using System.Diagnostics;
using System.Text;

namespace Commons.PersistentCache.SQLite.Tests;

public class CacheTest : TestBase
{
    [Fact]
    public async Task Test_AddFiniteMultipleEntries()
    {
        var factory = new Factory();
        var cache = factory.Create("AddFiniteMultipleEntries", new PersistentCacheConfiguration()) as Cache;
        Assert.NotNull(cache);
        RegisterTestCache(cache);
        var result = await Task.WhenAll([
            cache.SaveAsync("key0", "123456"u8.ToArray()),
            cache.SaveAsync("key1", "789012"u8.ToArray()),
            cache.SaveAsync("key2", "345678"u8.ToArray()),
        ]);
        Assert.True(result.Length == 3);
        Assert.True(result.All(x => true));

        var cacheData = cache.GetCacheStateData();
        Assert.Equal(18, cacheData.TotalSizeInBytes);
    }

    [Fact]
    public async Task Test_AddManyMultipleEntries()
    {
        var stopwatch = Stopwatch.StartNew();

        var factory = new Factory();
        Cache? cache = factory.Create("AddManyMultipleEntries", new PersistentCacheConfiguration()) as Cache;
        Assert.NotNull(cache);
        RegisterTestCache(cache);
        List<Task<bool>> operations = [];
        var totalSize = 0;
        for (int i = 0; i < 1000; i++)
        {
            var value = Encoding.UTF8.GetBytes($"value{i}");
            totalSize += value.Length;
            operations.Add(cache.SaveAsync($"key{i}", value));
        }

        var result = await Task.WhenAll(operations.ToArray());
        Assert.True(result.Length == 1000);
        Assert.True(result.All(x => true));

        var cacheData = cache.GetCacheStateData();
        Assert.Equal(totalSize, cacheData.TotalSizeInBytes);

        stopwatch.Stop();
        long elapsedMs = stopwatch.ElapsedMilliseconds;
    }

    [Fact]
    public async Task Test_SimpleSaveGetRemoveEntry()
    {
        var factory = new Factory();
        Cache? cache = factory.Create("SimpleSaveGetRemoveEntry", new PersistentCacheConfiguration()) as Cache;
        Assert.NotNull(cache);
        RegisterTestCache(cache);

        // Save
        Assert.True(await cache.SaveAsync("key0", "value0"u8.ToArray()));
        Assert.Equal(6, cache.GetCacheStateData().TotalSizeInBytes);

        // Get
        var r0 = await cache.GetAsync("key0");
        Assert.NotNull(r0);
        Assert.Equal("value0", Encoding.UTF8.GetString(r0));
        Assert.Equal(6, cache.GetCacheStateData().TotalSizeInBytes);

        // Remove
        Assert.True(await cache.RemoveAsync("key0"));
        Assert.Equal(0, cache.GetCacheStateData().TotalSizeInBytes);

        // Final Get
        var r1 = await cache.GetAsync("key0");
        Assert.Null(r1);
    }

    [Fact]
    public async Task Test_GetInvalidEntries()
    {
        var factory = new Factory();
        Cache? cache = factory.Create("GetInvalidEntries", new PersistentCacheConfiguration()) as Cache;
        Assert.NotNull(cache);
        RegisterTestCache(cache);

        // Get
        for (var i = 0; i < 10; i++)
        {
            Assert.Null(await cache.GetAsync($"key{i}"));
        }
    }

    [Fact]
    public async Task Test_ReplaceEntry()
    {
        var factory = new Factory();
        Cache? cache = factory.Create("ReplaceEntry", new PersistentCacheConfiguration()) as Cache;
        Assert.NotNull(cache);
        RegisterTestCache(cache);

        // Save
        Assert.True(await cache.SaveAsync("key0", "value0"u8.ToArray()));
        Assert.Equal(6, cache.GetCacheStateData().TotalSizeInBytes);

        // Get
        var r0 = await cache.GetAsync("key0");
        Assert.NotNull(r0);
        Assert.Equal("value0", Encoding.UTF8.GetString(r0));
        Assert.Equal(6, cache.GetCacheStateData().TotalSizeInBytes);

        // Replace
        Assert.True(await cache.SaveAsync("key0", "val"u8.ToArray()));
        Assert.Equal(3, cache.GetCacheStateData().TotalSizeInBytes);
    }

    [Fact]
    public async Task Test_GetOldEntry()
    {
        var factory = new Factory();
        Cache? cache =
            factory.Create("GetOldEntry", new PersistentCacheConfiguration(SlidingTimeToLiveInSeconds: 1)) as Cache;
        Assert.NotNull(cache);
        RegisterTestCache(cache);

        // Save
        var r0 = await cache.SaveAsync("key0", "value0"u8.ToArray());
        Assert.True(r0);
        Assert.Equal(6, cache.GetCacheStateData().TotalSizeInBytes);

        // Get
        var r1 = await cache.GetAsync("key0");
        Assert.NotNull(r1);
        Assert.Equal("value0", Encoding.UTF8.GetString(r1));
        Assert.Equal(6, cache.GetCacheStateData().TotalSizeInBytes);

        // Delay
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Final Get
        var r3 = await cache.GetAsync("key0");
        Assert.Null(r3);
    }

    [Fact]
    public async Task Test_GetRecentlyAccessedEntry()
    {
        var factory = new Factory();
        Cache? cache = factory.Create("GetRecentlyAccessedEntry",
            new PersistentCacheConfiguration(SlidingTimeToLiveInSeconds: 2)) as Cache;
        Assert.NotNull(cache);
        RegisterTestCache(cache);

        // Save
        Assert.True(await cache.SaveAsync("key0", "value0"u8.ToArray()));
        Assert.Equal(6, cache.GetCacheStateData().TotalSizeInBytes);

        // Get
        var r = await cache.GetAsync("key0");
        Assert.NotNull(r);
        Assert.Equal("value0", Encoding.UTF8.GetString(r));
        Assert.Equal(6, cache.GetCacheStateData().TotalSizeInBytes);

        // Delay
        await Task.Delay(TimeSpan.FromSeconds(0.5));

        // Get
        Assert.NotNull(await cache.GetAsync("key0"));

        // Delay
        await Task.Delay(TimeSpan.FromSeconds(0.5));

        // Get
        Assert.NotNull(await cache.GetAsync("key0"));

        // Delay
        await Task.Delay(TimeSpan.FromSeconds(0.5));

        // Get
        Assert.NotNull(await cache.GetAsync("key0"));
    }

    [Fact]
    public async Task Test_GlobalSlidingTimeToLiveInSecondsExpiration()
    {
        var factory = new Factory();
        Cache? cache = factory.Create("GlobalSlidingTimeToLiveInSecondsExpiration",
            new PersistentCacheConfiguration(SlidingTimeToLiveInSeconds: 1)) as Cache;
        Assert.NotNull(cache);
        RegisterTestCache(cache);

        // Save
        Assert.True(await cache.SaveAsync("key0", "value0"u8.ToArray()));
        Assert.Equal(6, cache.GetCacheStateData().TotalSizeInBytes);

        // Get
        var r = await cache.GetAsync("key0");
        Assert.NotNull(r);
        Assert.Equal("value0", Encoding.UTF8.GetString(r));

        // Delay
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Get
        Assert.Null(await cache.GetAsync("key0"));
    }

    [Fact]
    public async Task Test_GlobalTimeToLiveInSecondsExpiration()
    {
        var factory = new Factory();
        Cache? cache = factory.Create("GlobalTimeToLiveInSecondsExpiration",
            new PersistentCacheConfiguration(TimeToLiveInSeconds: 1)) as Cache;
        Assert.NotNull(cache);
        RegisterTestCache(cache);

        // Save
        Assert.True(await cache.SaveAsync("key0", "value0"u8.ToArray()));
        Assert.Equal(6, cache.GetCacheStateData().TotalSizeInBytes);

        // Get
        var r = await cache.GetAsync("key0");
        Assert.NotNull(r);
        Assert.Equal("value0", Encoding.UTF8.GetString(r));

        // Delay
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Get
        Assert.Null(await cache.GetAsync("key0"));
    }

    [Fact]
    public async Task Test_EntrySlidingTimeToLiveInSecondsExpiration()
    {
        var factory = new Factory();
        Cache? cache = factory.Create("EntrySlidingTimeToLiveInSecondsExpiration",
            new PersistentCacheConfiguration(SlidingTimeToLiveInSeconds: 120)) as Cache;
        Assert.NotNull(cache);
        RegisterTestCache(cache);

        // Save
        Assert.True(await cache.SaveAsync("key0", "value0"u8.ToArray(),
            EntryConfiguration.WithSlidingTimeToLiveInSeconds(1)));
        Assert.Equal(6, cache.GetCacheStateData().TotalSizeInBytes);

        // Get
        var r = await cache.GetAsync("key0");
        Assert.NotNull(r);
        Assert.Equal("value0", Encoding.UTF8.GetString(r));

        // Delay
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Get
        Assert.Null(await cache.GetAsync("key0"));
    }

    [Fact]
    public async Task Test_EntryTimeToLiveInSecondsExpiration()
    {
        var factory = new Factory();
        Cache? cache = factory.Create("EntryTimeToLiveInSecondsExpiration",
            new PersistentCacheConfiguration(TimeToLiveInSeconds: 120)) as Cache;
        Assert.NotNull(cache);
        RegisterTestCache(cache);

        // Save
        Assert.True(await cache.SaveAsync("key0", "value0"u8.ToArray(),
            EntryConfiguration.WithTimeToLiveInSeconds(1)));
        Assert.Equal(6, cache.GetCacheStateData().TotalSizeInBytes);

        // Get
        var r = await cache.GetAsync("key0");
        Assert.NotNull(r);
        Assert.Equal("value0", Encoding.UTF8.GetString(r));

        // Delay
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Get
        Assert.Null(await cache.GetAsync("key0"));
    }

    [Fact]
    public async Task Test_CleanUpForCapacity()
    {
        var factory = new Factory();
        Cache? cache =
            factory.Create("CleanUpForCapacity", new PersistentCacheConfiguration(MaximumCapacityInBytes: 10)) as Cache;
        Assert.NotNull(cache);
        RegisterTestCache(cache);

        // Insert elements not overpassing Capacity
        for (var i = 0; i < 9; i++)
        {
            Assert.True(await cache.SaveAsync($"key{i}", Encoding.UTF8.GetBytes($"{i}")));
        }

        Assert.Equal(9, cache.GetCacheStateData().TotalSizeInBytes);

        // Clean up nothing
        Assert.True(await cache.CleanupAsync());
        Assert.Equal(9, cache.GetCacheStateData().TotalSizeInBytes);

        // Insert element passing capacity
        Assert.True(await cache.SaveAsync("ups", "ups"u8.ToArray()));

        Assert.True(await cache.CleanupAsync());
        Assert.True(10 > cache.GetCacheStateData().TotalSizeInBytes,
            $"{cache.GetCacheStateData().TotalSizeInBytes} not valid");
    }

    [Fact]
    public async Task Test_Migration()
    {
        var factory = new Factory();
        factory.DefaultPath = "Resources/";
        Cache? cache = factory.Create("Version_1") as Cache;
        Assert.NotNull(cache);

        var value = await cache.GetAsync("key0");
        Assert.NotNull(value);
        Assert.Equal("value0", Encoding.UTF8.GetString(value));
    }
}