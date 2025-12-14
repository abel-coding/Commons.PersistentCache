namespace Commons.PersistentCache.SQLite.Tests;

using Commons.PersistentCache.SQLite;

public class FactoryTest : TestBase
{
    [Fact]
    public void Test_SingleCreateWithName()
    {
        var factory = new Factory();
        var cache = factory.Create("SingleCreateWithName", new PersistentCacheConfiguration()) as Cache;
        Assert.NotNull(cache);
        RegisterTestCache(cache);
    }

    [Fact]
    public void Test_MultipleCreateWithEqualName()
    {
        var cache0 = new Factory().Create("MultipleCreateWithEqualName", new PersistentCacheConfiguration()) as Cache;
        Assert.NotNull(cache0);
        RegisterTestCache(cache0);
        var cache1 = new Factory().Create("MultipleCreateWithEqualName", new PersistentCacheConfiguration()) as Cache;
        Assert.NotNull(cache1);
        RegisterTestCache(cache1);
        Assert.Equal(cache0, cache1);
    }

    [Fact]
    public void Test_MultipleCreateWithEqualNameButDifferentPath()
    {
        var factory = new Factory();
        factory.DefaultPath = "Temp";
        var cache0 = factory.Create("MultipleCreateWithEqualName", new PersistentCacheConfiguration()) as Cache;
        Assert.NotNull(cache0);
        RegisterTestCache(cache0);
        var cache1 = new Factory().Create("MultipleCreateWithEqualName", new PersistentCacheConfiguration()) as Cache;
        Assert.NotNull(cache1);
        RegisterTestCache(cache1);
        Assert.NotEqual(cache0, cache1);
    }
}