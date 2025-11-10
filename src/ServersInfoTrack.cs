using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Config;
using Microsoft.Extensions.Logging;
using ServersList.Config;
using ServersList.Database;

namespace ServersList;

public partial class ServersInfoTrack : BasePlugin, IPluginConfig<ServersInfoTrackConfig>
{
    public override string ModuleName => "ServersInfoTrack";
    public override string ModuleAuthor => "0x32";
    public override string ModuleVersion => "1.1";

    public ServersInfoTrackConfig Config { get; set; } = new();
    private DatabaseService? _db;
    private DateTime _lastUpdate = DateTime.MinValue;
    private const int UpdateIntervalMs = 3000;

    public override void Load(bool hotReload)
    {
        Logger.LogInformation("Плагин загружен. ID: {Id}", Config.ServerId);

        _db = new DatabaseService(Config, Logger);

        _ = _db.EnsureTableExistsAsync();
        _ = _db.SetStatusAsync(1);
        ScheduleUpdate();

        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnect);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventServerShutdown>(OnShutdown);
    }

    public override void Unload(bool hotReload)
    {
        Logger.LogWarning("Выгрузка — статус OFFLINE...");
        try
        {
            _db?.SetStatusAsync(0).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logger.LogCritical("Ошибка при выгрузке: {Error}", ex.Message);
        }
        base.Unload(hotReload);
    }

    private HookResult OnShutdown(EventServerShutdown e, GameEventInfo i)
    {
        Logger.LogInformation("Остановка: {Reason}", e.Reason ?? "неизвестно");
        try
        {
            _db?.SetStatusAsync(0).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logger.LogError("Ошибка shutdown: {Error}", ex.Message);
        }
        return HookResult.Continue;
    }

    private void ScheduleUpdate()
    {
        if (DateTime.UtcNow - _lastUpdate < TimeSpan.FromMilliseconds(UpdateIntervalMs))
            return;
        _lastUpdate = DateTime.UtcNow;
        _ = UpdateAsync();
    }

    private async Task UpdateAsync()
    {
        var map = Server.MapName ?? "unknown";
        var players = Utilities.GetPlayers().Count(p => p.IsValid && !p.IsBot && !p.IsHLTV);
        var max = Server.MaxPlayers;

        await (_db?.UpdateServerAsync(players, max, map) ?? Task.CompletedTask);
    }

    public void OnConfigParsed(ServersInfoTrackConfig config)
    {
        Config = config;
        if (string.IsNullOrWhiteSpace(Config.ServerName))
            Config.ServerName = "Безымянный сервер";
        if (string.IsNullOrWhiteSpace(Config.ServerIp))
            Config.ServerIp = "127.0.0.1";
    }

    private HookResult OnPlayerConnect(EventPlayerConnectFull ev, GameEventInfo info)
    {
        ScheduleUpdate();
        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect ev, GameEventInfo info)
    {
        ScheduleUpdate();
        return HookResult.Continue;
    }
}
