using System.Numerics;

namespace AFKManager;

internal class PlayerInfo
{
    public Vector3 Angles { get; set; } = Vector3.Zero;
    public Vector3 Origin { get; set; } = Vector3.Zero;
    public float AfkTime { get; set; }
    public int AfkWarningCount { get; set; }
    public int SpecWarningCount { get; set; }
    public float SpecAfkTime { get; set; }
    public bool MovedByPlugin { get; set; }
    public float AntiCampTime { get; set; }
    public int AntiCampWarningCount { get; set; }
}
