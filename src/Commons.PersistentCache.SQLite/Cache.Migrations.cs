using Microsoft.Data.Sqlite;

namespace Commons.PersistentCache.SQLite;

public partial class Cache
{
    private const int Version = 2;

    private async Task InitializeDatabaseStructure(SqliteConnection connection, int? existingVersion)
    {
        try
        {
            var pragmas = await ConfigurePragmaAsync(connection);
        }
        catch
        {
            // Ignore
        }
        
        if (existingVersion is not { } currentVersion)
        {
            await CreateDatabaseStructure(connection);
            return;
        }

        if (currentVersion < 2)
        {
            await MigrateDatabaseToVersion2(connection);
        }
    }

    private async Task CreateDatabaseStructure(SqliteConnection connection)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                                   DROP TABLE IF EXISTS Metadata;
                                   DROP TABLE IF EXISTS Entries;
                                   CREATE TABLE IF NOT EXISTS Entries (
                                       Key TEXT PRIMARY KEY,
                                       Value BLOB NOT NULL,
                                       SizeInBytes INTEGER NOT NULL,
                                       AccessUtc INTEGER NOT NULL,
                                       CreatedUtc INTEGER NOT NULL,
                                       TimeToLiveInSeconds INTEGER,
                                       SlidingTimeToLiveInSeconds INTEGER
                                   );
                                   CREATE TABLE IF NOT EXISTS Metadata (
                                       Key INTEGER PRIMARY KEY,
                                       Version INTEGER NOT NULL,
                                       MaximumCapacityInBytes INTEGER,
                                       TimeToLiveInSeconds INTEGER,
                                       SlidingTimeToLiveInSeconds INTEGER
                                           CHECK (Key = 1)
                                   );
                                   INSERT INTO Metadata (Key, Version)
                                   VALUES (1, $Version)
                                   ON CONFLICT(Key) DO UPDATE SET
                                       Version = excluded.Version;
                                   """;
            command.Parameters.AddWithValue("$Version", Version);
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (SqliteException e)
        {
            Console.WriteLine(e);
            throw;
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            Console.WriteLine(e);
            throw;
        }
    }

    private async Task MigrateDatabaseToVersion2(SqliteConnection connection)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                                   ALTER TABLE Entries ADD COLUMN TimeToLiveInSeconds INTEGER;
                                   ALTER TABLE Entries ADD COLUMN SlidingTimeToLiveInSeconds INTEGER;
                                   UPDATE Metadata SET Version = 2 WHERE Key = 1;
                                   """;
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (SqliteException e)
        {
            Console.WriteLine(e);
            throw;
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            Console.WriteLine(e);
            throw;
        }
    }
}