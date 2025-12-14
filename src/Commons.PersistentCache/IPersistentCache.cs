namespace Commons.PersistentCache;

public interface IPersistentCache
{
    Task<bool> SetConfigurationAsync(PersistentCacheConfiguration configuration,
        CancellationToken cancellationToken = default);

    Task<bool> CleanupAsync(CancellationToken cancellationToken = default);

    Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> SaveAsync(string key, byte[] data, CancellationToken cancellationToken = default);

    Task<bool> SaveAsync(string key, byte[] data, EntryConfiguration configuration, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);
}