namespace Commons.PersistentCache;

/// <summary>
/// Configuration that will be applied per cache entry when specified. 
/// </summary>
/// <param name="TimeToLiveInSeconds">Maximum TTL for the entry since its creation in seconds.</param>
/// <param name="SlidingTimeToLiveInSeconds">Maximum TTL for the entry since last accessed in seconds.</param>
public record EntryConfiguration(
    int? TimeToLiveInSeconds = null,
    int? SlidingTimeToLiveInSeconds = null)
{
    /// <summary>
    /// Creates an entry configuration by providing only an SlidingTimeToLiveInSeconds.
    /// </summary>
    /// <param name="slidingTimeToLiveInSeconds">Maximum TTL for the entry since last accessed in seconds.</param>
    /// <returns>Specified entry configuration</returns>
    public static EntryConfiguration WithSlidingTimeToLiveInSeconds(int slidingTimeToLiveInSeconds)
    {
        return new EntryConfiguration(SlidingTimeToLiveInSeconds: slidingTimeToLiveInSeconds);
    }

    /// <summary>
    /// Creates an entry configuration by providing only an TimeToLiveInSeconds.
    /// </summary>
    /// <param name="timeToLiveInSeconds">Maximum TTL for the entry since its creation in seconds.</param>
    /// <returns>Specified entry configuration</returns>
    public static EntryConfiguration WithTimeToLiveInSeconds(int timeToLiveInSeconds)
    {
        return new EntryConfiguration(TimeToLiveInSeconds: timeToLiveInSeconds);
    }
}
    