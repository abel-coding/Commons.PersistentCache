namespace Commons.PersistentCache;

public interface IPersistentCacheFactory
{
    string? DefaultPath { get; }
    
    IPersistentCache Create(string path, PersistentCacheConfiguration? configuration = null);
}