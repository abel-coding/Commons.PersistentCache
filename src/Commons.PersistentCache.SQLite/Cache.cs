using Microsoft.Data.Sqlite;

namespace Commons.PersistentCache.SQLite;

/// <summary>
/// Fast <see cref="IPersistentCache"/> implementation on top of SQLite.
/// </summary>
public partial class Cache : IPersistentCache, IAsyncDisposable
{
    /// <summary>
    /// Default constructor that requires a path for the SQLite database store.
    /// </summary>
    /// <param name="path">Base path to be used for the SQLite database.</param>
    /// <param name="configuration">Cache configuration if needed.</param>
    public Cache(string path, PersistentCacheConfiguration? configuration = null)
    {
        _configuration = configuration;

        _connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = $"{path}.cache.db",
            Pooling = true
        };

        _connectionPool = new SqliteConnectionPool(_connectionStringBuilder.ConnectionString);

        _initializationTask =
            new(CreateInitializeTask, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    #region Fields

    private readonly SqliteConnectionStringBuilder _connectionStringBuilder;
    private readonly SqliteConnectionPool _connectionPool;
    private string? _dataSource;
    private readonly Lazy<ValueTask<bool>> _initializationTask;
    private ValueTask<bool> InitializationTask => _initializationTask.Value;
    private PersistentCacheConfiguration? _configuration;
    private long _totalSizeInBytes;
    private bool _disposed;
    private bool _internalCleanUpInProgress;
    private readonly object _internalCleanuplockObject = new();

    #endregion

    #region IPersistentCache

    /// <inheritdoc />
    public async Task<bool> SetConfigurationAsync(PersistentCacheConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        await InitializationTask.ConfigureAwait(false);

        return await WithConnection(async connection =>
        {
            if (connection is null) return false;
            return await UpsertMetadataConfiguration(connection, configuration, cancellationToken);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> CleanupAsync(CancellationToken cancellationToken = default)
    {
        await InitializationTask.ConfigureAwait(false);
        return await WithConnection(async connection =>
        {
            if (connection is null) return false;
            await CleanUpInvalidEntriesAsync(connection, cancellationToken);
            return true;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        await InitializationTask.ConfigureAwait(false);
        return await WithConnection(async connection =>
        {
            if (connection is null) return null;
            var stream = await RetrieveEntryAsync(connection, key, cancellationToken);
            if (stream is null) return null;
            byte[] data = new byte[stream.Length];
            await stream.ReadExactlyAsync(data, 0, data.Length, cancellationToken);
            return data;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> SaveAsync(string key, byte[] data, CancellationToken cancellationToken = default)
    {
        await InitializationTask.ConfigureAwait(false);
        return await WithConnection(async connection =>
        {
            if (connection is null) return false;
            return await UpsertEntryAsync(connection, key, data, null, cancellationToken);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> SaveAsync(string key, byte[] data, EntryConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        await InitializationTask.ConfigureAwait(false);
        return await WithConnection(async connection =>
        {
            if (connection is null) return false;
            return await UpsertEntryAsync(connection, key, data, configuration, cancellationToken);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await InitializationTask.ConfigureAwait(false);
        return await WithConnection(async connection =>
        {
            if (connection is null) return false;
            return await RemoveEntryAsync(connection, key, cancellationToken);
        }, cancellationToken);
    }
    
    #endregion

    #region Private

    private async ValueTask<bool> CreateInitializeTask()
    {
        return await WithConnection(async connection =>
        {
            if (connection is null) return false;
            try
            {
                _dataSource = connection.DataSource;
                _totalSizeInBytes = 0;

                // Request Existing Version
                var (savedConfiguration, currentVersion, totalSizeInBytes) =
                    await ReadCacheMetadata(connection);

                if (currentVersion is not null && currentVersion == Version)
                {
                    _totalSizeInBytes = totalSizeInBytes;

                    if (_configuration is null)
                    {
                        _configuration = savedConfiguration;
                    }
                    else
                    {
                        await UpsertMetadataConfiguration(connection, _configuration);
                    }

                    return true;
                }

                // Initialize Database Structure
                await InitializeDatabaseStructure(connection, currentVersion);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
            finally
            {
                connection.Close();
            }

            return true;
        });
    }

    private async Task<T> WithConnection<T>(
        Func<SqliteConnection?, Task<T>> function,
        CancellationToken cancellationToken = default)
    {
        SqliteConnection? connection = null;
        try
        {
            connection = await _connectionPool.RentAsync(cancellationToken)
                .ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        T? result;
        try
        {
            result = await function(connection).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        finally
        {
            if (connection is not null)
            {
                _connectionPool.Return(connection);
            }
        }

        return result;
    }
    
    private async Task<bool> ConfigurePragmaAsync(SqliteConnection connection,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                                   PRAGMA journal_mode=WAL;
                                   """;

            var result = await command.ExecuteScalarAsync(cancellationToken);

            return result is not null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<bool> UpsertMetadataConfiguration(SqliteConnection connection,
        PersistentCacheConfiguration configuration, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                                   INSERT INTO Metadata (Key, Version, MaximumCapacityInBytes, TimeToLiveInSeconds, SlidingTimeToLiveInSeconds)
                                   VALUES (1, $Version, $MaximumCapacityInBytes, $TimeToLiveInSeconds, $SlidingTimeToLiveInSeconds)
                                   ON CONFLICT(Key) DO UPDATE SET
                                       Version = excluded.Version,
                                       MaximumCapacityInBytes = excluded.MaximumCapacityInBytes,
                                       TimeToLiveInSeconds = excluded.TimeToLiveInSeconds,
                                       SlidingTimeToLiveInSeconds = excluded.SlidingTimeToLiveInSeconds;
                                   """;
            command.Parameters.AddWithValue("$Version", Version);
            command.Parameters.AddWithValue("$MaximumCapacityInBytes",
                configuration.MaximumCapacityInBytes ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$TimeToLiveInSeconds",
                configuration.TimeToLiveInSeconds ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$SlidingTimeToLiveInSeconds",
                configuration.SlidingTimeToLiveInSeconds ?? (object)DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
            _configuration = configuration;
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    private async Task<(PersistentCacheConfiguration?, int?, long)> ReadCacheMetadata(SqliteConnection connection,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                                   SELECT MaximumCapacityInBytes, TimeToLiveInSeconds, SlidingTimeToLiveInSeconds, Version
                                   FROM Metadata
                                   WHERE Key = 1;
                                   SELECT SUM(SizeInBytes) FROM Entries;
                                   """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            PersistentCacheConfiguration? configuration = null;
            int? version = null;

            if (await reader.ReadAsync(cancellationToken))
            {
                int? maximumCapacityInBytes = reader.IsDBNull(0) ? null : reader.GetInt32(0);
                int? timeToLiveInSeconds = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                int? slidingTimeToLiveInSeconds = reader.IsDBNull(2) ? null : reader.GetInt32(2);
                version = reader.IsDBNull(3) ? null : reader.GetInt32(3);
                configuration = new PersistentCacheConfiguration(
                    MaximumCapacityInBytes: maximumCapacityInBytes,
                    TimeToLiveInSeconds: timeToLiveInSeconds,
                    SlidingTimeToLiveInSeconds: slidingTimeToLiveInSeconds
                );
            }

            long totalSizeInBytes = 0;
            if (await reader.NextResultAsync(cancellationToken) && await reader.ReadAsync(cancellationToken))
            {
                totalSizeInBytes = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
            }

            return (configuration, version, totalSizeInBytes);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return (null, null, 0);
        }
    }

    private async Task<bool> UpsertEntryAsync(
        SqliteConnection connection,
        string key,
        byte[] value,
        EntryConfiguration? configuration,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var utcNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            await using var command = connection.CreateCommand();
            
            bool hasConfiguration = configuration != null;

            if (hasConfiguration)
            {
                command.CommandText = """
                                      SELECT SizeInBytes FROM Entries WHERE Key = $Key;
                                      INSERT INTO Entries (Key, Value, SizeInBytes, AccessUtc, CreatedUtc, TimeToLiveInSeconds, SlidingTimeToLiveInSeconds)
                                      VALUES ($Key, $Value, $SizeInBytes, $AccessUtc, $CreatedUtc, $TimeToLiveInSeconds, $SlidingTimeToLiveInSeconds)
                                      ON CONFLICT(Key) DO UPDATE SET
                                          Value = excluded.Value,
                                          SizeInBytes = excluded.SizeInBytes,
                                          AccessUtc = excluded.AccessUtc,
                                          TimeToLiveInSeconds = excluded.TimeToLiveInSeconds,
                                          SlidingTimeToLiveInSeconds = excluded.SlidingTimeToLiveInSeconds;
                                      """;
            }
            else
            {
                command.CommandText = """
                                      SELECT SizeInBytes FROM Entries WHERE Key = $Key;
                                      INSERT INTO Entries (Key, Value, SizeInBytes, AccessUtc, CreatedUtc)
                                      VALUES ($Key, $Value, $SizeInBytes, $AccessUtc, $CreatedUtc)
                                      ON CONFLICT(Key) DO UPDATE SET
                                          Value = excluded.Value,
                                          SizeInBytes = excluded.SizeInBytes,
                                          AccessUtc = excluded.AccessUtc;
                                      """;
            }

            command.Parameters.AddWithValue("$Key", key);
            command.Parameters.AddWithValue("$Value", value);
            command.Parameters.AddWithValue("$SizeInBytes", value.Length);
            command.Parameters.AddWithValue("$AccessUtc", utcNow);
            command.Parameters.AddWithValue("$CreatedUtc", utcNow);

            if (hasConfiguration)
            {
                command.Parameters.AddWithValue("$TimeToLiveInSeconds", configuration?.TimeToLiveInSeconds ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("SlidingTimeToLiveInSeconds", configuration?.SlidingTimeToLiveInSeconds ?? (object)DBNull.Value);
            }

            var reader = await command.ExecuteReaderAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            long previousSize = 0;
            if (await reader.ReadAsync(cancellationToken))
            {
                previousSize = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
            }

            Interlocked.Add(ref _totalSizeInBytes, value.Length - previousSize);
            _ = TriggerInternalCleanupIfNeeded();
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqliteException e)
        {
            Console.WriteLine(e);
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    private async Task<Stream?> RetrieveEntryAsync(
        SqliteConnection connection,
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var utcNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                                  SELECT Value, AccessUtc, CreatedUtc, TimeToLiveInSeconds, SlidingTimeToLiveInSeconds FROM Entries WHERE Key = $Key;
                                  UPDATE Entries SET AccessUtc = $AccessUtc WHERE Key = $Key;
                                  """;

            command.Parameters.AddWithValue("$Key", key);
            command.Parameters.AddWithValue("$AccessUtc", utcNow);

            var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;

            var accessUtc = reader.GetInt64(1);
            var createdUtc = reader.GetInt64(2);
            long? timeToLiveInSeconds = reader.IsDBNull(3) ? null : reader.GetInt64(3);
            long? slidingTimeToLiveInSeconds = reader.IsDBNull(4) ? null : reader.GetInt64(4);
            if (!IsValid(accessUtc, createdUtc, timeToLiveInSeconds, slidingTimeToLiveInSeconds))
            {
                await RemoveEntryAsync(connection, key, cancellationToken);
                return null;
            }

            var result = reader.GetStream(0);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }

    private async Task<bool> RemoveEntryAsync(
        SqliteConnection connection,
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                                  SELECT SizeInBytes FROM Entries WHERE Key = $Key;
                                  DELETE FROM Entries WHERE Key = $Key;
                                  """;

            command.Parameters.AddWithValue("$Key", key);

            var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            if (reader.HasRows)
            {
                var sizeInBytes = reader.GetInt64(0);
                Interlocked.Add(ref _totalSizeInBytes, -sizeInBytes);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    private async Task<bool> CleanUpInvalidEntriesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        long deltaSizeInBytes = 0;
        try
        {
            // Remove expired entries
            long remainingEntries = 0;
            {
                var utcNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                                      SELECT SUM(SizeInBytes) FROM Entries WHERE 
                                        ($CheckSliding = 1 AND ($UtcNow - AccessUtc) > $SlidingTimeToLiveInSeconds) OR
                                        ($CheckCreated = 1 AND ($UtcNow - CreatedUtc) > $TimeToLiveInSeconds) OR
                                        (SlidingTimeToLiveInSeconds IS NOT NULL AND ($UtcNow - AccessUtc) > SlidingTimeToLiveInSeconds) OR 
                                        (TimeToLiveInSeconds IS NOT NULL AND ($UtcNow - CreatedUtc) > TimeToLiveInSeconds);
                                      DELETE FROM Entries WHERE 
                                        ($CheckSliding = 1 AND ($UtcNow - AccessUtc) > $SlidingTimeToLiveInSeconds) OR
                                        ($CheckCreated = 1 AND ($UtcNow - CreatedUtc) > $TimeToLiveInSeconds) OR 
                                        (SlidingTimeToLiveInSeconds IS NOT NULL AND ($UtcNow - AccessUtc) > SlidingTimeToLiveInSeconds) OR 
                                        (TimeToLiveInSeconds IS NOT NULL AND ($UtcNow - CreatedUtc) > TimeToLiveInSeconds);
                                      SELECT COUNT(*) FROM Entries;
                                      """;

                var slidingTimeToLiveInSeconds = _configuration?.SlidingTimeToLiveInSeconds ?? int.MaxValue;
                var timeToLiveInSeconds = _configuration?.TimeToLiveInSeconds ?? int.MaxValue;
                var checkSliding = _configuration?.SlidingTimeToLiveInSeconds is not null;
                var checkCreated = _configuration?.TimeToLiveInSeconds is not null;

                command.Parameters.AddWithValue("$UtcNow", utcNow);
                command.Parameters.AddWithValue("$CheckSliding", checkSliding);
                command.Parameters.AddWithValue("$CheckCreated", checkCreated);
                command.Parameters.AddWithValue("$SlidingTimeToLiveInSeconds", slidingTimeToLiveInSeconds);
                command.Parameters.AddWithValue("$TimeToLiveInSeconds", timeToLiveInSeconds);

                var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    var sizeInBytes = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                    deltaSizeInBytes += sizeInBytes;
                }

                if (await reader.NextResultAsync(cancellationToken) && await reader.ReadAsync(cancellationToken))
                {
                    remainingEntries = reader.GetInt64(0);
                }
            }

            // If not enough nor possible, remove the least used entries 
            if (_configuration?.MaximumCapacityInBytes is { } maximumCapacityInBytes &&
                _totalSizeInBytes > maximumCapacityInBytes)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                                      SELECT SUM(SizeInBytes)
                                      FROM (
                                          SELECT SizeInBytes
                                          FROM Entries
                                          ORDER BY AccessUtc ASC
                                          LIMIT $Limit
                                      );
                                      WITH ToDelete AS (
                                          SELECT ROWID, SizeInBytes
                                          FROM Entries
                                          ORDER BY AccessUtc ASC
                                          LIMIT $Limit
                                      )
                                      DELETE FROM Entries WHERE ROWID IN (SELECT ROWID FROM ToDelete);
                                      """;
                command.Parameters.AddWithValue("$Limit", remainingEntries / 2);
                var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    var sizeInBytes = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                    deltaSizeInBytes += sizeInBytes;
                }
            }

            await transaction.CommitAsync(cancellationToken);
            Interlocked.Add(ref _totalSizeInBytes, -deltaSizeInBytes);

            return true;
        }
        catch (SqliteException e)
        {
            Console.WriteLine(e);
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    private async Task<bool> TriggerInternalCleanupIfNeeded()
    {
        lock (_internalCleanuplockObject)
        {
            if (_internalCleanUpInProgress) return false;
            _internalCleanUpInProgress = true;
        }

        try
        {
            var shouldProceed = _configuration?.MaximumCapacityInBytes is { } maximumCapacityInBytes &&
                                _totalSizeInBytes >= maximumCapacityInBytes;
            if (!shouldProceed) return false;
            await CleanupAsync(CancellationToken.None);
            return true;
        }
        finally
        {
            lock (_internalCleanuplockObject)
            {
                _internalCleanUpInProgress = false;
            }
        }
    }

    private bool IsValid(
        long accessUtc,
        long createdUtc,
        long? entryTimeToLiveInSeconds,
        long? entrySlidingTimeToLiveInSeconds)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var createdAge = now - createdUtc;
        var accessAge = now - accessUtc;

        if (entryTimeToLiveInSeconds is { } currentTimeToLiveInSeconds)
        {
            if (createdAge > currentTimeToLiveInSeconds) return false;
        }
        else if (_configuration?.TimeToLiveInSeconds is { } timeToLiveInSeconds)
        {
            if (createdAge > timeToLiveInSeconds) return false;
        }

        if (entrySlidingTimeToLiveInSeconds is { } currentSlidingTimeToLiveInSeconds)
        {
            if (accessAge > currentSlidingTimeToLiveInSeconds) return false;
        }
        else if (_configuration?.SlidingTimeToLiveInSeconds is { } slidingTimeToLiveInSeconds)
        {
            if (accessAge > slidingTimeToLiveInSeconds) return false;
        }

        return true;
    }

    #endregion

    #region IAsyncDisposable

    internal bool IsDisposed => _disposed;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _connectionPool.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Internal

    internal CacheStateData GetCacheStateData()
    {
        return new CacheStateData(TotalSizeInBytes: _totalSizeInBytes, DatabasePath: _dataSource);
    }

    #endregion
}