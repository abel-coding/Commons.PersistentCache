namespace Commons.PersistentCache;

/// <summary>
/// Specifies configuration settings for a persistent cache instance, including capacity limits and expiration policies.
/// </summary>
/// <param name="MaximumCapacityInBytes">Gets the maximum storage capacity of the cache in bytes. When exceeded,
/// the cache may evict entries based on the implementation's eviction policy.</param>
/// <param name="TimeToLiveInSeconds">Maximum TTL for the entry since its creation in seconds.</param>
/// <param name="SlidingTimeToLiveInSeconds">Maximum TTL for the entry since last accessed in seconds.</param>
public record PersistentCacheConfiguration(
    int? MaximumCapacityInBytes = null,
    int? TimeToLiveInSeconds = null,
    int? SlidingTimeToLiveInSeconds = null);
