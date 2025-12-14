using System.Collections.Concurrent;
using System.Data;
using Microsoft.Data.Sqlite;

namespace Commons.PersistentCache.SQLite;

public class SqliteConnectionPool : IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly ConcurrentBag<SqliteConnection> _pool = new();
    private readonly SemaphoreSlim _poolLimiter;
    private bool _disposed = false;

    public SqliteConnectionPool(string connectionString, int maxPoolSize = 3)
    {
        _connectionString = connectionString;
        _poolLimiter = new SemaphoreSlim(maxPoolSize, maxPoolSize);
    }

    public async Task<SqliteConnection> RentAsync(CancellationToken cancellationToken = default)
    {
        await _poolLimiter.WaitAsync(cancellationToken);

        if (_pool.TryTake(out var connection))
        {
            if (connection.State == ConnectionState.Closed)
                await connection.OpenAsync(cancellationToken);

            return connection;
        }

        var newConnection = new SqliteConnection(_connectionString);
        await newConnection.OpenAsync(cancellationToken);
        return newConnection;
    }

    public void Return(SqliteConnection connection)
    {
        if (connection.State == ConnectionState.Open && !_disposed)
        {
            _pool.Add(connection);
        }
        else
        {
            connection.Dispose();
        }

        _poolLimiter.Release();
    }

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        while (_pool.TryTake(out var connection))
        {
            await connection.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    #endregion

}