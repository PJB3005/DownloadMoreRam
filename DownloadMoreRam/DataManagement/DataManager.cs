using Microsoft.Data.Sqlite;

namespace DownloadMoreRam.DataManagement;

/// <summary>
/// Manages the watchdog's SQLite database, used to store all persisted data of the watchdog itself.
/// </summary>
public sealed class DataManager(ILogger<DataManager> logger, IConfiguration configuration) : IHostedService
{
    public SqliteConnection OpenConnection()
    {
        var con = new SqliteConnection(GetConnectionString());
        con.Open();
        return con;
    }

    private string GetConnectionString()
    {
        return configuration.GetConnectionString("Default")!;
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        var con = OpenConnection();

        Migrator.Migrate(con, "DownloadMoreRam.DataManagement.Migrations", logger);
        return Task.CompletedTask;
    }

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        // No shutdown needed.
        return Task.CompletedTask;
    }
}