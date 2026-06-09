using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;
using System.Numerics;

namespace AFKManager;

public partial class AFKManager : BasePlugin, IPluginConfig<AFKManagerConfig>
{
    public override string ModuleAuthor => "NiGHT, K4ryuu, Marchand";
    public override string ModuleName => "AFK Manager";
    public override string ModuleVersion => "1.0.0";

    public required AFKManagerConfig Config { get; set; }
    private CCSGameRulesProxy? _gGameRulesProxy;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _afkTimer;
    private string[] _afkSkipFlags = [];
    private string[] _antiCampSkipFlags = [];
    private string[] _specSkipFlags = [];
    private readonly Dictionary<uint, PlayerInfo> _gPlayerInfo = new();

    public void OnConfigParsed(AFKManagerConfig config)
    {
        Config = config;

        if (Config.AfkPunishment is < 0 or > 2)
        {
            Config.AfkPunishment = 1;
            Logger.LogWarning($"{ModuleName}: AFKPunishment value is invalid, setting to default value (1).");
        }

        if (Config.Timer < 0.1f)
        {
            Config.Timer = 5.0f;
            Logger.LogWarning($"{ModuleName}: Timer value is invalid, setting to default value (5.0).");
        }

        if (Config.AfkPositionTolerance < 0.0f)
        {
            Config.AfkPositionTolerance = 1.0f;
            Logger.LogWarning($"{ModuleName}: AfkPositionTolerance value is invalid, setting to default value (1.0).");
        }

        if (Config.AfkAngleTolerance < 0.0f)
        {
            Config.AfkAngleTolerance = 0.5f;
            Logger.LogWarning($"{ModuleName}: AfkAngleTolerance value is invalid, setting to default value (0.5).");
        }

        if (Config.AfkWarnInterval < Config.Timer)
        {
            Config.AfkWarnInterval = Config.Timer;
            Logger.LogWarning($"{ModuleName}: The value of AfkWarnInterval is less than the value of Timer, AfkWarnInterval will be forced to {Config.Timer}");
        }

        if (Config.SpecWarnInterval < Config.Timer)
        {
            Config.SpecWarnInterval = Config.Timer;
            Logger.LogWarning($"{ModuleName}: The value of SpecWarnInterval is less than the value of Timer, SpecWarnInterval will be forced to {Config.Timer}");
        }

        if (Config.AntiCampWarnInterval < Config.Timer)
        {
            Config.AntiCampWarnInterval = Config.Timer;
            Logger.LogWarning($"{ModuleName}: The value of AntiCampWarnInterval is less than the value of Timer, AntiCampWarnInterval will be forced to {Config.Timer}");
        }

        if (Config.AntiCampPunishment is < 1 or > 2)
        {
            Config.AntiCampPunishment = 1;
            Logger.LogWarning($"{ModuleName}: AntiCampPunishment value is invalid, setting to default value (1).");
        }

        _afkSkipFlags = Config.AfkSkipFlag.ToArray();
        _antiCampSkipFlags = Config.AntiCampSkipFlag.ToArray();
        _specSkipFlags = Config.SpecSkipFlag.ToArray();

        if (_afkTimer != null)
        {
            _afkTimer.Kill();
            _afkTimer = AddTimer(Config.Timer, AfkTimer_Callback, TimerFlags.REPEAT);
        }
    }

    public override void Load(bool hotReload)
    {
        _afkTimer = AddTimer(Config.Timer, AfkTimer_Callback, TimerFlags.REPEAT);

        if (hotReload)
            Server.NextFrame(RefreshRuntimeState);

        RegisterListener<Listeners.OnMapStart>(_ =>
        {
            Server.NextFrame(() =>
            {
                RefreshRuntimeState();
            });
        });

        RegisterListener<Listeners.OnMapEnd>(() =>
        {
            _gPlayerInfo.Clear();
            _gGameRulesProxy = null;
        });

        RegisterListener<Listeners.OnClientConnected>(playerSlot =>
        {
            var player = Utilities.GetPlayerFromSlot(playerSlot);
            if (player == null || !player.IsValid || player.IsBot)
                return;

            var slot = (uint)player.Slot;

            if (_gPlayerInfo.ContainsKey(slot))
                return;

            _gPlayerInfo.Add(slot, new PlayerInfo
            {
                Angles = Vector3.Zero,
                Origin = Vector3.Zero
            });
        });

        RegisterListener<Listeners.OnClientDisconnectPost>(playerSlot =>
        {
            _gPlayerInfo.Remove((uint)playerSlot);
        });

        AddCommandListener("spec_mode", OnCommandListener);
        AddCommandListener("spec_next", OnCommandListener);
        AddCommandListener("spec_prev", OnCommandListener);
        AddCommandListener("+attack",   OnCommandListener);
        AddCommandListener("+attack2",  OnCommandListener);
    }

    public override void Unload(bool hotReload)
    {
        _afkTimer?.Kill();
        _afkTimer = null;
        _gPlayerInfo.Clear();
    }

    private void RefreshRuntimeState()
    {
        _gPlayerInfo.Clear();
        EnsureGameRulesProxy();

        var players = Utilities.GetPlayers().Where(x => x is { IsBot: false, Connected: PlayerConnectedState.Connected });
        foreach (var player in players)
        {
            GetOrCreatePlayerInfo((uint)player.Slot);
        }
    }

    private void EnsureGameRulesProxy()
    {
        if (_gGameRulesProxy != null && _gGameRulesProxy.IsValid)
            return;

        _gGameRulesProxy = null;
        _gGameRulesProxy = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault();
    }

    private PlayerInfo GetOrCreatePlayerInfo(uint playerSlot)
    {
        if (_gPlayerInfo.TryGetValue(playerSlot, out var data))
            return data;

        data = new PlayerInfo();
        _gPlayerInfo[playerSlot] = data;
        return data;
    }
}
