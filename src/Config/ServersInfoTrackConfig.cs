using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace ServersList.Config;

public class ServersInfoTrackConfig : BasePluginConfig
{
    [JsonPropertyName("Database")]
    public DatabaseConfig Database { get; set; } = new();

    [JsonPropertyName("ServerId")]
    public int ServerId { get; set; } = 1;

    [JsonPropertyName("ServerName")]
    public string ServerName { get; set; } = "My Server";

    [JsonPropertyName("ServerIp")]
    public string ServerIp { get; set; } = "127.0.0.1";
}

public class DatabaseConfig
{
    [JsonPropertyName("Host")]
    public string Host { get; set; } = "";

    [JsonPropertyName("Port")]
    public int Port { get; set; } = 3306;

    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("User")]
    public string User { get; set; } = "";

    [JsonPropertyName("Password")]
    public string Password { get; set; } = "";

    [JsonPropertyName("TableName")]
    public string TableName { get; set; } = "servers_info";
}
