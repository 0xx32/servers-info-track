using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Config;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ServersList;

#region CONFIG

public class ServersInfoTrackConfig : BasePluginConfig
{
    [JsonPropertyName("Database")] public DatabaseConfig Database { get; set; } = new();
    [JsonPropertyName("ServerId")] public int ServerId { get; set; } = 1;
    [JsonPropertyName("ServerName")] public string ServerName { get; set; } = "My Server";
    [JsonPropertyName("ServerIp")] public string ServerIp { get; set; } = "127.0.0.1";
}

public class DatabaseConfig
{
    [JsonPropertyName("Host")] public string Host { get; set; } = "";
    [JsonPropertyName("Port")] public int Port { get; set; } = 3306;
    [JsonPropertyName("Name")] public string Name { get; set; } = "";
    [JsonPropertyName("User")] public string User { get; set; } = "";
    [JsonPropertyName("Password")] public string Password { get; set; } = "";
    [JsonPropertyName("TableName")] public string TableName { get; set; } = "servers_info";
}

#endregion

public partial class ServersInfoTrack : BasePlugin, IPluginConfig<ServersInfoTrackConfig>
{
    public override string ModuleName => "ServersInfoTrack";
    public override string ModuleAuthor => "0x32";
    public override string ModuleVersion => "1.0";

    public ServersInfoTrackConfig Config { get; set; } = new();

    private string ConnectionString =>
        $"Server={Config.Database.Host};Port={Config.Database.Port};Database={Config.Database.Name};User ID={Config.Database.User};Password={Config.Database.Password};SslMode=Preferred;";

    public override void Load(bool hotReload)
    {
        Logger.LogInformation($"Loaded. ServerID={Config.ServerId}");

        _ = Task.Run(async () =>
        {
            await EnsureTableExistsAsync();
            await SetServerStatusAsync(1);
            await UpdateDatabaseAsync();
        });

        // Подписываемся на игровые события
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventServerShutdown>(OnServerShutdown);
    }

    private HookResult OnServerShutdown(EventServerShutdown @event, GameEventInfo info)
    {
        Logger.LogInformation("Server shutdown event detected.");
        _ = SetServerStatusAsync(0, true);
        return HookResult.Continue;
    }

    public override void Unload(bool hotReload)
    {
        _ = SetServerStatusAsync(0);
        base.Unload(hotReload);
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull ev, GameEventInfo info)
    {
        _ = Task.Run(UpdateDatabaseAsync);
        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect ev, GameEventInfo info)
    {
        _ = Task.Run(UpdateDatabaseAsync);
        return HookResult.Continue;
    }

    private async Task EnsureTableExistsAsync()
    {
        try
        {
            Logger.LogInformation($"Проверка подключения к MySQL ({Config.Database.Host}:{Config.Database.Port})...");

            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();
            Logger.LogInformation("Подключение к MySQL успешно!");

            string createTableQuery = $@"
                CREATE TABLE IF NOT EXISTS `{Config.Database.TableName}` (
                    `id` INT PRIMARY KEY,
                    `ip` VARCHAR(64) DEFAULT NULL,
                    `name` VARCHAR(64) DEFAULT NULL,
                    `map_name` VARCHAR(64) DEFAULT NULL,
                    `active_players` INT DEFAULT 0,
                    `max_players` INT DEFAULT 0,
                    `max_players_offset` INT DEFAULT 0,
                    `status` TINYINT DEFAULT 1,
                    `last_update` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                );";

            using var command = new MySqlCommand(createTableQuery, connection);
            await command.ExecuteNonQueryAsync();

            Logger.LogInformation($"Таблица `{Config.Database.TableName}` проверена/создана.");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Ошибка при подключении к БД: {ex.Message}");
        }
    }

    private async Task UpdateDatabaseAsync()
    {
        try
        {
            string mapName = "unknown";
            int activePlayers = 0;
            int maxPlayers = 0;

            await Server.NextFrameAsync(() =>
            {
                mapName = Server.MapName ?? "unknown";
                activePlayers = Utilities.GetPlayers().Count(p => p.IsValid && !p.IsBot && !p.IsHLTV);
                maxPlayers = Server.MaxPlayers;
            });

            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();

            string query = $@"
                INSERT INTO `{Config.Database.TableName}`
                (`id`, `ip`, `name`, `active_players`, `max_players`, `map_name`, `status`)
                VALUES (@id, @ip, @name, @players, @max, @map, 1)
                ON DUPLICATE KEY UPDATE
                    `ip` = VALUES(`ip`),
                    `name` = VALUES(`name`),
                    `active_players` = VALUES(`active_players`),
                    `max_players` = VALUES(`max_players`),
                    `map_name` = VALUES(`map_name`),
                    `status` = 1,
                    `last_update` = CURRENT_TIMESTAMP;";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@id", Config.ServerId);
            command.Parameters.AddWithValue("@ip", Config.ServerIp);
            command.Parameters.AddWithValue("@name", Config.ServerName);
            command.Parameters.AddWithValue("@players", activePlayers);
            command.Parameters.AddWithValue("@max", maxPlayers);
            command.Parameters.AddWithValue("@map", mapName);

            await command.ExecuteNonQueryAsync();
            Logger.LogDebug($"DB updated for server {Config.ServerId}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error updating DB: {ex.Message}");
        }
    }

    private async Task SetServerStatusAsync(int status, bool resetPlayers = false)
    {
        try
        {
            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();

            string query = $@"
                INSERT INTO `{Config.Database.TableName}` (`id`, `status`, `active_players`)
                VALUES (@id, @status, @players)
                ON DUPLICATE KEY UPDATE
                    `status` = VALUES(`status`),
                    `last_update` = CURRENT_TIMESTAMP,
                    `active_players` = {(resetPlayers ? "0" : "VALUES(`active_players`)")};";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@id", Config.ServerId);
            command.Parameters.AddWithValue("@status", status);
            command.Parameters.AddWithValue("@players", 0);

            await command.ExecuteNonQueryAsync();

            Logger.LogInformation($"Server status set to {(status == 1 ? "ONLINE" : "OFFLINE")}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error setting server status: {ex.Message}");
        }
    }


    public void OnConfigParsed(ServersInfoTrackConfig config)
    {
        Config = config;

        if (string.IsNullOrWhiteSpace(Config.ServerName))
            Config.ServerName = "Unnamed Server";

        if (string.IsNullOrWhiteSpace(Config.ServerIp))
            Config.ServerIp = "127.0.0.1";
    }
}
