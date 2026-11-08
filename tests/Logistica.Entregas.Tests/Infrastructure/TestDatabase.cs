using Logistica.Entregas.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace Logistica.Entregas.Tests.Infrastructure;

/// <summary>Base de datos SQLite temporal (un archivo por prueba) con las migraciones aplicadas.</summary>
public sealed class TestDatabase : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"entregas-test-{Guid.NewGuid():N}.db");

    public string ConnectionString => $"Data Source={_path};Pooling=False";
    public SqliteConnectionFactory Connections => new(ConnectionString);

    public async Task InitializeAsync() => await new SqliteMigrator(Connections).MigrateAsync();

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(_path); } catch (IOException) { /* el archivo temporal se limpia luego */ }
        return Task.CompletedTask;
    }

    public async Task<long> ScalarAsync(string sql)
    {
        await using var connection = await Connections.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = await Connections.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}
