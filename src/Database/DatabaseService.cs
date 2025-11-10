// Database/DatabaseService.cs
using Microsoft.Extensions.Logging;
using MySqlConnector;
using ServersList.Config;

namespace ServersList.Database;

public class DatabaseService
{
    private readonly ServersInfoTrackConfig _config;
    private readonly ILogger _logger;
    private readonly string _connectionString;

    public DatabaseService(ServersInfoTrackConfig config, ILogger logger)
    {
        _config = config;
        _logger = logger;
        _connectionString =
            $"Server={config.Database.Host};Port={config.Database.Port};"
            + $"Database={config.Database.Name};User ID={config.Database.User};"
            + $"Password={config.Database.Password};SslMode=Preferred;";
    }

    public async Task EnsureTableExistsAsync()
    {
        try
        {
            _logger.LogInformation(
                "Проверка подключения к MySQL: {Host}:{Port}",
                _config.Database.Host,
                _config.Database.Port
            );
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = string.Format(Queries.CreateTable, _config.Database.TableName);
            using var cmd = new MySqlCommand(query, connection);
            await cmd.ExecuteNonQueryAsync();

            _logger.LogInformation("Таблица `{Table}` готова.", _config.Database.TableName);
        }
        catch (Exception ex)
        {
            _logger.LogError("Ошибка подключения к БД: {Error}", ex.Message);
        }
    }

    public async Task UpdateServerAsync(int activePlayers, int maxPlayers, string mapName)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = string.Format(Queries.UpdateServer, _config.Database.TableName);
            using var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@id", _config.ServerId);
            cmd.Parameters.AddWithValue("@ip", _config.ServerIp);
            cmd.Parameters.AddWithValue("@name", _config.ServerName);
            cmd.Parameters.AddWithValue("@players", activePlayers);
            cmd.Parameters.AddWithValue("@max", maxPlayers);
            cmd.Parameters.AddWithValue("@map", mapName);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogDebug(
                "БД обновлена: ID {Id}, игроков {Players}/{Max}",
                _config.ServerId,
                activePlayers,
                maxPlayers
            );
        }
        catch (Exception ex)
        {
            _logger.LogError("Ошибка обновления БД: {Error}", ex.Message);
        }
    }

    public async Task SetStatusAsync(int status)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = string.Format(Queries.SetStatus, _config.Database.TableName);
            using var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@id", _config.ServerId);
            cmd.Parameters.AddWithValue("@status", status);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Статус: {Status}", status == 1 ? "ОНЛАЙН" : "ОФФЛАЙН");
        }
        catch (Exception ex)
        {
            _logger.LogError("Ошибка установки статуса: {Error}", ex.Message);
        }
    }
}
