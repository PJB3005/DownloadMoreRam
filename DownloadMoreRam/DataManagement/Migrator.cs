using Dapper;
using Microsoft.Data.Sqlite;

namespace DownloadMoreRam.DataManagement;

/// <summary>
/// Utility class to do SQLite database migrations.
/// </summary>
public static class Migrator
{
    internal static bool Migrate(SqliteConnection connection, string prefix, ILogger logger)
    {
        logger.LogDebug("Migrating with prefix {Prefix}", prefix);

        using var transaction = connection.BeginTransaction();

        connection.Execute("""
            CREATE TABLE IF NOT EXISTS SchemaVersions(
                SchemaVersionID INTEGER PRIMARY KEY,
                ScriptName TEXT NOT NULL,
                Applied DATETIME NOT NULL
            );
            """,
            transaction: transaction);

        var appliedScripts = connection.Query<string>(
            "SELECT ScriptName FROM main.SchemaVersions",
            transaction: transaction);

        // ReSharper disable once InvokeAsExtensionMethod
        var scriptsToApply = MigrationFileScriptList(prefix).ExceptBy(appliedScripts, s => s.name).OrderBy(x => x.name);

        var success = true;
        foreach (var (name, script) in scriptsToApply)
        {
            logger.LogInformation("Applying migration {Transaction}!", name);
            transaction.Save(name);

            try
            {
                var code = script.Up(connection);

                connection.Execute(code, transaction: transaction);

                connection.Execute(
                    "INSERT INTO SchemaVersions(ScriptName, Applied) VALUES (@Script, datetime('now'))",
                    new { Script = name },
                    transaction);

                transaction.Release(name);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Exception during migration {Transaction}, rolling back...!", name);
                transaction.Rollback(name);
                success = false;
                break;
            }
        }

        logger.LogInformation("Committing migrations");
        transaction.Commit();
        return success;
    }

    private static IEnumerable<(string name, IMigrationScript)> MigrationFileScriptList(string prefix)
    {
        var assembly = typeof(Migrator).Assembly;
        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.EndsWith(".sql") || !resourceName.StartsWith(prefix))
                continue;

            var index = resourceName.LastIndexOf('.', resourceName.Length - 5, resourceName.Length - 4);
            index += 1;

            var name = resourceName[(index + "Script".Length)..^4];

            using var reader = new StreamReader(assembly.GetManifestResourceStream(resourceName)!);
            var scriptContents = reader.ReadToEnd();
            yield return (name, new FileMigrationScript(scriptContents));
        }
    }

    public interface IMigrationScript
    {
        string Up(SqliteConnection connection);
    }

    private sealed class FileMigrationScript : IMigrationScript
    {
        private readonly string _code;

        public FileMigrationScript(string code) => _code = code;

        public string Up(SqliteConnection connection) => _code;
    }
}
