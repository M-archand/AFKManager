using CounterStrikeSharp.API.Core;

namespace AFKManager;

public class AFKManagerConfig : BasePluginConfig
{
    public int AfkPunishAfterWarnings { get; set; } = 3;
    public int AfkPunishment { get; set; } = 1;
    public int AfkKickMinPlayers { get; set; } = 5;
    public float AfkWarnInterval { get; set; } = 5.0f;
    public float AfkPositionTolerance { get; set; } = 1.0f;
    public float AfkAngleTolerance { get; set; } = 0.5f;
    public bool UseEyeAngles { get; set; } = false;
    public bool EnableDebug { get; set; } = false;
    public float SpecWarnInterval { get; set; } = 20.0f;
    public int SpecKickAfterWarnings { get; set; } = 5;
    public int SpecKickMinPlayers { get; set; } = 5;
    public bool SpecKickOnlyMovedByPlugin { get; set; } = false;
    public List<string> SpecSkipFlag { get; set; } = new List<string> { "@css/root", "@css/ban" };
    public List<string> AfkSkipFlag { get; set; } = new List<string> { "@css/root", "@css/ban" };
    public List<string> AntiCampSkipFlag { get; set; } = new List<string> { "@css/root", "@css/ban" };
    public string PlaySoundName { get; set; } = "ui/panorama/popup_reveal_01";
    public bool SkipWarmup { get; set; } = false;
    public int AntiCampMinPlayers { get; set; } = 5;
    public float AntiCampRadius { get; set; } = 130.0f;
    public int AntiCampPunishment { get; set; } = 1;
    public float AntiCampWarnInterval { get; set; } = 10.0f;
    public int AntiCampPunishAfterWarnings { get; set; } = 3;
    public bool AntiCampSkipBombPlanted { get; set; } = true;
    public int AntiCampSkipTeam { get; set; } = 3;
    public float Timer { get; set; } = 5.0f;
}
