using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using System.Numerics;

namespace AFKManager;

public partial class AFKManager
{
    private void AfkTimer_Callback()
    {
        try
        {
            EnsureGameRulesProxy();

            if (_gGameRulesProxy == null)
            {
                DebugLog("AFK timer tick skipped: game rules proxy unavailable.");
                return;
            }

            var gameRules = _gGameRulesProxy.GameRules;
            if (gameRules == null)
            {
                DebugLog("AFK timer tick skipped: game rules unavailable.");
                return;
            }

            if (gameRules.FreezePeriod)
            {
                DebugLog("AFK timer tick skipped: freeze period active.");
                return;
            }

            if (Config.SkipWarmup && gameRules.WarmupPeriod)
            {
                DebugLog("AFK timer tick skipped: warmup active.");
                return;
            }

            var players = Utilities.GetPlayers().Where(x => x is { IsBot: false, Connected: PlayerConnectedState.Connected }).ToList();
            var playersCount = players.Count;

            foreach (var player in players)
            {
                var data = GetOrCreatePlayerInfo((uint)player.Slot);

                if (player is { LifeState: (byte)LifeState_t.LIFE_ALIVE, Team: CsTeam.Terrorist or CsTeam.CounterTerrorist })
                {
                    var playerPawn = player.PlayerPawn.Value;
                    if (playerPawn == null) continue;
                    var playerFlags = playerPawn.Flags;

                    if ((playerFlags & ((uint)PlayerFlags.FL_ONGROUND | (uint)PlayerFlags.FL_FROZEN)) != (uint)PlayerFlags.FL_ONGROUND)
                        continue;

                    var shouldCheckAfk = Config.AfkPunishAfterWarnings != 0
                                         && playersCount >= Config.AfkKickMinPlayers
                                         && !(Config.AfkSkipFlag.Count >= 1 && AdminManager.PlayerHasPermissions(player, _afkSkipFlags));

                    var shouldCheckAntiCamp = Config.AntiCampPunishAfterWarnings != 0
                                              && playersCount >= Config.AntiCampMinPlayers
                                              && !(Config.AntiCampSkipBombPlanted && gameRules.BombPlanted)
                                              && !(Config.AntiCampSkipFlag.Count >= 1 && AdminManager.PlayerHasPermissions(player, _antiCampSkipFlags))
                                              && player.TeamNum != Config.AntiCampSkipTeam;

                    if (!shouldCheckAfk && !shouldCheckAntiCamp)
                    {
                        data.AfkTime = 0;
                        data.AfkWarningCount = 0;
                        data.AntiCampTime = 0;
                        data.AntiCampWarningCount = 0;
                        continue;
                    }

                    var origin = playerPawn.CBodyComponent?.SceneNode?.AbsOrigin;
                    var originVector = ToVector3(origin);
                    var allowAntiCampChecks = shouldCheckAntiCamp;
                    var afkWarnedThisTick = false;

                    if (shouldCheckAfk)
                    {
                        var positionDelta = CalculateDistance2D(data.Origin, originVector);
                        var pitchDelta = 0.0f;
                        var yawDelta = 0.0f;
                        var isStationary = positionDelta <= Config.AfkPositionTolerance;

                        if (Config.UseEyeAngles)
                        {
                            var angles = playerPawn?.EyeAngles;
                            var anglesVector = ToVector3(angles);

                            pitchDelta = Math.Abs(data.Angles.X - anglesVector.X);
                            yawDelta = CalculateAngleDelta(data.Angles.Y, anglesVector.Y);
                            isStationary = isStationary
                                && pitchDelta <= Config.AfkAngleTolerance
                                && yawDelta <= Config.AfkAngleTolerance;

                            data.Angles = anglesVector;
                        }
                        else
                        {
                            data.Angles = Vector3.Zero;
                        }

                        if (Config.EnableDebug)
                            DebugLog(
                                $"Checked player: {player.PlayerName}. " +
                                $"Position:       {FormatPosition(originVector)}. " +
                                $"PosDelta2D:     {positionDelta:F3}. " +
                                $"PitchDelta:     {(Config.UseEyeAngles ? $"{pitchDelta:F3}" : "disabled")}. " +
                                $"YawDelta:       {(Config.UseEyeAngles ? $"{yawDelta:F3}" : "disabled")}. " +
                                $"Stationary:     {isStationary}. " +
                                $"AfkTime:        {data.AfkTime:F1}. " +
                                $"Warnings:       {data.AfkWarningCount}");

                        if (isStationary)
                        {
                            data.AfkTime += Config.Timer;

                            if (data.AfkTime < Config.AfkWarnInterval)
                            {
                                allowAntiCampChecks = false;
                            }
                            else if (data.AfkWarningCount >= Config.AfkPunishAfterWarnings)
                            {
                                switch (Config.AfkPunishment)
                                {
                                    case 0:
                                        Server.PrintToChatAll(ReplaceVars(player, Localizer["ChatKillMessage"].Value));
                                        playerPawn?.CommitSuicide(false, true);
                                        break;
                                    case 1:
                                        Server.PrintToChatAll(ReplaceVars(player, Localizer["ChatMoveMessage"].Value));
                                        if (playerPawn != null)
                                        {
                                            playerPawn.CommitSuicide(false, true);
                                            player.ChangeTeam(CsTeam.Spectator);
                                            data.MovedByPlugin = true;
                                        }
                                        break;
                                    case 2:
                                        Server.PrintToChatAll(ReplaceVars(player, Localizer["ChatKickMessage"].Value));
                                        if (player.UserId.HasValue && player.UserId >= 0)
                                            Server.ExecuteCommand($"kickid {player.UserId}");
                                        break;
                                }

                                data.AfkWarningCount = 0;
                                data.AfkTime = 0;
                                allowAntiCampChecks = false;
                            }
                            else
                            {
                                switch (Config.AfkPunishment)
                                {
                                    case 0:
                                        player.PrintToChat(ReplaceVars(player, Localizer["ChatWarningKillMessage"].Value, (Config.AfkPunishAfterWarnings - data.AfkWarningCount) * Config.AfkWarnInterval));
                                        break;
                                    case 1:
                                        player.PrintToChat(ReplaceVars(player, Localizer["ChatWarningMoveMessage"].Value, (Config.AfkPunishAfterWarnings - data.AfkWarningCount) * Config.AfkWarnInterval));
                                        break;
                                    case 2:
                                        player.PrintToChat(ReplaceVars(player, Localizer["ChatWarningKickMessage"].Value, (Config.AfkPunishAfterWarnings - data.AfkWarningCount) * Config.AfkWarnInterval));
                                        break;
                                }

                                if (!string.IsNullOrEmpty(Config.PlaySoundName))
                                    player.ExecuteClientCommand($"play {Config.PlaySoundName}");

                                data.AfkTime = 0;
                                data.AfkWarningCount++;
                                afkWarnedThisTick = true;
                            }
                        }
                        else
                        {
                            data.AfkTime = 0;
                            data.AfkWarningCount = 0;
                        }
                    }
                    else
                    {
                        data.AfkTime = 0;
                        data.AfkWarningCount = 0;
                        data.Angles = Vector3.Zero;
                    }

                    if (allowAntiCampChecks && !afkWarnedThisTick)
                    {
                        if (CalculateDistance2D(data.Origin, originVector) < Config.AntiCampRadius)
                        {
                            data.AntiCampTime += Config.Timer;

                            if (data.AntiCampTime >= Config.AntiCampWarnInterval)
                            {
                                if (data.AntiCampWarningCount >= Config.AntiCampPunishAfterWarnings)
                                {
                                    switch (Config.AntiCampPunishment)
                                    {
                                        case 1:
                                            Server.PrintToChatAll(ReplaceVars(player, Localizer["AntiCampSpecMessage"].Value));
                                            if (playerPawn != null)
                                            {
                                                playerPawn.CommitSuicide(false, true);
                                                player.ChangeTeam(CsTeam.Spectator);
                                                data.MovedByPlugin = true;
                                            }
                                            break;
                                        case 2:
                                            Server.PrintToChatAll(ReplaceVars(player, Localizer["AntiCampKickMessage"].Value));
                                            if (player.UserId.HasValue && player.UserId >= 0)
                                                Server.ExecuteCommand($"kickid {player.UserId}");
                                            break;
                                    }

                                    data.AntiCampWarningCount = 0;
                                    data.AntiCampTime = 0;
                                }
                                else
                                {
                                    switch (Config.AntiCampPunishment)
                                    {
                                        case 1:
                                            player.PrintToChat(ReplaceVars(player, Localizer["AntiCampSpecWarningMessage"].Value, Config.AntiCampPunishAfterWarnings * Config.AntiCampWarnInterval - data.AntiCampWarningCount * Config.AntiCampWarnInterval));
                                            break;
                                        case 2:
                                            player.PrintToChat(ReplaceVars(player, Localizer["AntiCampKickWarningMessage"].Value, Config.AntiCampPunishAfterWarnings * Config.AntiCampWarnInterval - data.AntiCampWarningCount * Config.AntiCampWarnInterval));
                                            break;
                                    }

                                    if (!string.IsNullOrEmpty(Config.PlaySoundName))
                                        player.ExecuteClientCommand($"play {Config.PlaySoundName}");

                                    data.AntiCampWarningCount++;
                                    data.AntiCampTime = 0;
                                }
                            }
                        }
                        else
                        {
                            data.AntiCampWarningCount = 0;
                            data.AntiCampTime = 0;
                        }
                    }
                    else
                    {
                        data.AntiCampTime = 0;
                        data.AntiCampWarningCount = 0;
                    }

                    data.Origin = originVector;

                    continue;
                }

                if (Config.SpecKickAfterWarnings != 0
                    && player.TeamNum == 1
                    && playersCount >= Config.SpecKickMinPlayers)
                {
                    if ((Config.SpecKickOnlyMovedByPlugin && !data.MovedByPlugin) || (Config.SpecSkipFlag.Count >= 1 && AdminManager.PlayerHasPermissions(player, _specSkipFlags)))
                        continue;

                    data.SpecAfkTime += Config.Timer;

                    if (!(data.SpecAfkTime >= Config.SpecWarnInterval))
                        continue;

                    if (data.SpecWarningCount >= Config.SpecKickAfterWarnings)
                    {
                        Server.PrintToChatAll(ReplaceVars(player, Localizer["SpecKickMessage"].Value));
                        if (player.UserId.HasValue && player.UserId >= 0)
                            Server.ExecuteCommand($"kickid {player.UserId}");

                        data.SpecWarningCount = 0;
                        data.SpecAfkTime = 0;

                        continue;
                    }

                    player.PrintToChat(ReplaceVars(player, Localizer["SpecKickWarningMessage"].Value, (Config.SpecKickAfterWarnings - data.SpecWarningCount) * Config.SpecWarnInterval));
                    data.SpecWarningCount++;
                    data.SpecAfkTime = 0;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{ModuleName}: Unhandled exception in AFK timer callback.", ModuleName);
        }
    }
}
