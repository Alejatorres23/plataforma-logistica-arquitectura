using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace Logistica.Entregas.Infrastructure.Persistence;

/// <summary>Abre conexiones a la base de datos SQLite con las claves foráneas activas.</summary>
public sealed class SqliteConnectionFactory(string connectionString)
{
    public async Task<SqliteConnection> OpenAsync(CancellationToken ct = default)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString) { ForeignKeys = true };
        var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(ct);
        return connection;
    }
}

/// <summary>
/// Aplica las migraciones versionadas que aún no se han aplicado, en orden, igual que Flyway:
/// archivos V{n}__{descripcion}.sql, una tabla schema_version con lo aplicado y una suma de
/// verificación que detecta si alguien modificó una migración ya aplicada.
/// </summary>
public sealed partial class SqliteMigrator(SqliteConnectionFactory connections)
{
    [GeneratedRegex(@"V(?<version>\d+)__(?<name>[\w]+)\.sql$")]
    private static partial Regex MigrationName();

    public sealed record Migration(int Version, string Description, string Script, string Checksum);

    /// <summary>Migraciones incluidas en el ensamblado, ordenadas por versión.</summary>
    public static IReadOnlyList<Migration> Discover()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetManifestResourceNames()
            .Select(name => (name, match: MigrationName().Match(name)))
            .Where(x => x.match.Success)
            .Select(x =>
            {
                using var stream = assembly.GetManifestResourceStream(x.name)!;
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var script = reader.ReadToEnd().Replace("\r\n", "\n");
                var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
                return new Migration(int.Parse(x.match.Groups["version"].Value, CultureInfo.InvariantCulture),
                    x.match.Groups["name"].Value.Replace('_', ' '), script, checksum);
            })
            .OrderBy(m => m.Version)
            .ToList();
    }

    /// <summary>Aplica lo pendiente y devuelve cuántas migraciones se aplicaron.</summary>
    public async Task<int> MigrateAsync(CancellationToken ct = default)
    {
        await using var connection = await connections.OpenAsync(ct);
        await Execute(connection, """
            CREATE TABLE IF NOT EXISTS schema_version (
                version     INTEGER NOT NULL PRIMARY KEY,
                description TEXT    NOT NULL,
                checksum    TEXT    NOT NULL,
                applied_at  TEXT    NOT NULL
            );
            """, ct);

        var applied = new Dictionary<int, string>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT version, checksum FROM schema_version";
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                applied[reader.GetInt32(0)] = reader.GetString(1);
        }

        var count = 0;
        foreach (var migration in Discover())
        {
            if (applied.TryGetValue(migration.Version, out var checksum))
            {
                if (checksum != migration.Checksum)
                    throw new InvalidOperationException(
                        $"La migración V{migration.Version} ya aplicada fue modificada. Cree una migración nueva en lugar de editarla.");
                continue;
            }

            await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(ct);
            await Execute(connection, migration.Script, ct, tx);
            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = tx;
                insert.CommandText = "INSERT INTO schema_version (version, description, checksum, applied_at) VALUES ($v, $d, $c, $a)";
                insert.Parameters.AddWithValue("$v", migration.Version);
                insert.Parameters.AddWithValue("$d", migration.Description);
                insert.Parameters.AddWithValue("$c", migration.Checksum);
                insert.Parameters.AddWithValue("$a", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                await insert.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
            count++;
        }
        return count;
    }

    private static async Task Execute(SqliteConnection connection, string sql, CancellationToken ct, SqliteTransaction? tx = null)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

/// <summary>Conversión de fechas a texto UTC ISO-8601 y de vuelta.</summary>
internal static class SqliteDates
{
    public static string ToDb(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    public static DateTimeOffset FromDb(string value)
        => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
