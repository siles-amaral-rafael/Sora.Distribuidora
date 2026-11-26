using Microsoft.EntityFrameworkCore;
using Sora.Infrastructure.Persistence;
using System.Data.Common;
using Xunit;

namespace Sora.IntegrationTests;

[Collection("Isolated PostgreSQL")]
public sealed class PostgresIsolationTests
{
    [Fact]
    public async Task Test_database_is_the_named_isolated_database_and_supports_rollback()
    {
        var connectionString = Environment.GetEnvironmentVariable("SORA_IT_CONNECTION_STRING");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "Falta SORA_IT_CONNECTION_STRING. Inicia solo el entorno aislado con scripts/integration-env.sh up.");
        Assert.Contains("Database=sora_it", connectionString!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Username=sora_it", connectionString!, StringComparison.OrdinalIgnoreCase);
        var parsed = new DbConnectionStringBuilder { ConnectionString = connectionString! };
        Assert.True(parsed.TryGetValue("Host", out var host) &&
                    string.Equals(Convert.ToString(host), "127.0.0.1", StringComparison.OrdinalIgnoreCase));
        Assert.True(parsed.TryGetValue("Port", out var port) && Convert.ToString(port) == "55433");
        Assert.DoesNotContain("web_sora-postgres", connectionString!, StringComparison.OrdinalIgnoreCase);

        var options = new DbContextOptionsBuilder<SoraDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new SoraDbContext(options);
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var identity = connection.CreateCommand();
            identity.CommandText = "select current_database() || ':' || current_user";
            Assert.Equal("sora_it:sora_it", await identity.ExecuteScalarAsync());

            await using var migrations = connection.CreateCommand();
            migrations.CommandText = "select count(*) from \"__EFMigrationsHistory\"";
            Assert.True(Convert.ToInt32(await migrations.ExecuteScalarAsync()) > 0);

            await using var tx = await connection.BeginTransactionAsync();
            await using var probe = connection.CreateCommand();
            probe.Transaction = tx;
            probe.CommandText = "create temporary table sora_it_transaction_probe (id integer primary key)";
            await probe.ExecuteNonQueryAsync();
            probe.CommandText = "insert into sora_it_transaction_probe values (1)";
            await probe.ExecuteNonQueryAsync();
            probe.CommandText = "select count(*) from sora_it_transaction_probe";
            Assert.Equal(1, Convert.ToInt32(await probe.ExecuteScalarAsync()));
            await tx.RollbackAsync();
            await using var rolledBack = connection.CreateCommand();
            rolledBack.CommandText = "select to_regclass('pg_temp.sora_it_transaction_probe') is null";
            Assert.True(Convert.ToBoolean(await rolledBack.ExecuteScalarAsync()));
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}

[CollectionDefinition("Isolated PostgreSQL", DisableParallelization = true)]
public sealed class IsolatedPostgresCollection { }
