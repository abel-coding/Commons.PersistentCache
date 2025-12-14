namespace Commons.PersistentCache;

public record EntryConfiguration(
    int? TimeToLiveInSeconds = null,
    int? SlidingTimeToLiveInSeconds = null)
{
    public static EntryConfiguration WithSlidingTimeToLiveInSeconds(int slidingTimeToLiveInSeconds)
    {
        return new EntryConfiguration(SlidingTimeToLiveInSeconds: slidingTimeToLiveInSeconds);
    }

    public static EntryConfiguration WithTimeToLiveInSeconds(int timeToLiveInSeconds)
    {
        return new EntryConfiguration(TimeToLiveInSeconds: timeToLiveInSeconds);
    }
}
    