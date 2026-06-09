using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using System.Numerics;
using System.Text.RegularExpressions;
using CssQAngle = CounterStrikeSharp.API.Modules.Utils.QAngle;
using CssVector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace AFKManager;

public partial class AFKManager
{
    private void DebugLog(string message)
    {
        if (!Config.EnableDebug)
            return;

        Logger.LogDebug("[{ModuleName}] {Message}", ModuleName, message);
    }

    private static float CalculateDistance2D(Vector3 point1, Vector3 point2)
    {
        var dx = point2.X - point1.X;
        var dy = point2.Y - point1.Y;

        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static float CalculateAngleDelta(float previousAngle, float currentAngle)
    {
        var delta = ((currentAngle - previousAngle + 540f) % 360f) - 180f;
        return Math.Abs(delta);
    }

    private static string FormatPosition(Vector3 position)
    {
        return $"{position.X:F2} {position.Y:F2} {position.Z:F2}";
    }

    private static Vector3 ToVector3(CssVector? value)
    {
        return value is not null ? (Vector3)value : Vector3.Zero;
    }

    private static Vector3 ToVector3(CssQAngle? value)
    {
        return value is not null ? (Vector3)value : Vector3.Zero;
    }

    private static string GetTeamColor(CsTeam team)
    {
        return team switch
        {
            CsTeam.Spectator        => $"{ChatColors.Grey}",
            CsTeam.Terrorist        => $"{ChatColors.Red}",
            CsTeam.CounterTerrorist => $"{ChatColors.Blue}",
            _                       => $"{ChatColors.Default}"
        };
    }

    private static readonly Regex ChatColorToken =
        new(@"\{[^}]*\}", RegexOptions.Compiled);

    private static string SanitizePlayerName(string name) => ChatColorToken.Replace(name, "");

    private string ReplaceVars(CCSPlayerController player, string message, float timeAmount = 0.0f)
    {
        return Localizer["ChatPrefix"] + message.Replace("{playerName}", SanitizePlayerName(player.PlayerName))
                      .Replace("{teamColor}", GetTeamColor(player.Team))
                      .Replace("{timeAmount}", $"{timeAmount:F1}");
    }
}
