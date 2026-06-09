using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using System.Numerics;

namespace AFKManager;

public partial class AFKManager
{
    private HookResult OnCommandListener(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var data = GetOrCreatePlayerInfo((uint)player.Slot);

        data.SpecAfkTime = 0;
        data.SpecWarningCount = 0;
        data.AfkWarningCount = 0;

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        var value = GetOrCreatePlayerInfo((uint)player.Slot);

        value.SpecAfkTime = 0;
        value.SpecWarningCount = 0;
        value.AfkWarningCount = 0;

        if (@event.Team != 1)
            value.MovedByPlugin = false;

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        var slot = (uint)player.Slot;
        var userId = player.UserId;

        AddTimer(0.2f, () =>
        {
            var p = Utilities.GetPlayerFromSlot((int)slot);
            if (p == null || !p.IsValid || p.UserId != userId || p.LifeState != (byte)LifeState_t.LIFE_ALIVE)
                return;

            if (!_gPlayerInfo.TryGetValue(slot, out var data))
                return;

            var origin = p.PlayerPawn.Value?.CBodyComponent?.SceneNode?.AbsOrigin;

            data.Angles = Vector3.Zero;
            data.Origin = ToVector3(origin);
            data.SpecAfkTime = 0;
            data.SpecWarningCount = 0;
            data.MovedByPlugin = false;
            data.AntiCampWarningCount = 0;
            data.AntiCampTime = 0;
        }, TimerFlags.STOP_ON_MAPCHANGE);

        return HookResult.Continue;
    }
}
