namespace Commons.PersistentCache;

public record PersistentCacheConfiguration(
    int? MaximumCapacityInBytes = null,
    int? TimeToLiveInSeconds = null,
    int? SlidingTimeToLiveInSeconds = null);
