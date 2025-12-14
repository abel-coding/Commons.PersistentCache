namespace Commons.PersistentCache.SQLite;

internal record CacheStateData(long TotalSizeInBytes, string? DatabasePath = null);