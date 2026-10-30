namespace HookGrabRope.Models;

/// <summary>
/// Oyuncunun sahip olabilecegi veya aktif kullandigi HGR (Hook, Grab, Rope) yetenek bayraklari.
/// </summary>
[Flags]
public enum HgrAbility
{
    None = 0,
    Hook = 1 << 0,
    Grab = 1 << 1,
    Rope = 1 << 2,
    All = Hook | Grab | Rope
}

