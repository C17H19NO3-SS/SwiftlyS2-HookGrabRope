using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HookGrabRope.Utils;

/// <summary>
/// Oyuncu, Pawn, Fizik (Velocity/Gravity), EyePosition/EyeAngles ve hedef arama yardimcilari.
/// </summary>
public static class PlayerUtil
{
    public static bool IsUsable(IPlayer? player)
    {
        try { return player is { IsValid: true }; }
        catch { return false; }
    }

    public static bool IsAlive(IPlayer? player)
    {
        try { return player is { IsValid: true, IsAlive: true }; }
        catch { return false; }
    }

    public static CCSPlayerPawn? GetPawn(IPlayer? player)
    {
        try
        {
            if (!IsUsable(player)) return null;
            var pawn = player!.PlayerPawn;
            return pawn is { IsValid: true } ? pawn : null;
        }
        catch { return null; }
    }

    public static ulong GetSteamId64(IPlayer? player)
    {
        try { return IsUsable(player) ? player!.SteamID : 0UL; }
        catch { return 0UL; }
    }

    public static string GetName(IPlayer? player)
    {
        try
        {
            if (!IsUsable(player)) return "Bilinmeyen";
            var name = player!.Controller?.PlayerName;
            if (string.IsNullOrWhiteSpace(name)) name = player.Name;
            return string.IsNullOrWhiteSpace(name) ? $"Oyuncu#{player.Slot}" : name;
        }
        catch { return "Bilinmeyen"; }
    }

    /// <summary>
    /// Oyuncunun ayak/govde konumunu (AbsOrigin) dondurur.
    /// </summary>
    public static bool TryGetOrigin(IPlayer? player, out Vector origin)
    {
        origin = Vector.Zero;
        var pawn = GetPawn(player);
        if (pawn == null) return false;

        try
        {
            var abs = pawn.AbsOrigin;
            if (abs.HasValue)
            {
                origin = abs.Value;
                return true;
            }
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Oyuncunun goz konumunu (EyePosition) ve bakis acisini (EyeAngles) dondurur.
    /// </summary>
    public static bool TryGetEyeTransform(IPlayer? player, out Vector eyePos, out QAngle eyeAngles)
    {
        eyePos = Vector.Zero;
        eyeAngles = QAngle.Zero;
        var pawn = GetPawn(player);
        if (pawn == null) return false;

        try
        {
            if (pawn.EyePosition.HasValue)
            {
                eyePos = pawn.EyePosition.Value;
            }
            else if (pawn.AbsOrigin.HasValue)
            {
                var org = pawn.AbsOrigin.Value;
                eyePos = new Vector(org.X, org.Y, org.Z + 64.0f);
            }
            else
            {
                return false;
            }

            eyeAngles = pawn.EyeAngles;
            if (eyeAngles.Pitch == 0f && eyeAngles.Yaw == 0f && eyeAngles.Roll == 0f)
            {
                eyeAngles = pawn.V_angle;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Lazerin oyuncunun tam gozunun icine girmemesi icin goz/bel hizasinda baslangic noktasi hesaplar.
    /// </summary>
    public static Vector GetBeamOrigin(IPlayer player)
    {
        if (TryGetOrigin(player, out var origin))
        {
            return new Vector(origin.X, origin.Y, origin.Z + 42.0f);
        }
        return Vector.Zero;
    }

    /// <summary>
    /// Oyuncuya hiz (velocity) uygular (amx_hook.sma icindeki set_velo karsiligi).
    /// </summary>
    public static void SetVelocity(IPlayer? player, Vector velocity)
    {
        var pawn = GetPawn(player);
        if (pawn == null) return;

        try
        {
            pawn.AbsVelocity = velocity;
            pawn.Teleport(null, null, velocity);
        }
        catch { }
    }

    /// <summary>
    /// Herhangi bir CBaseEntity varligina hiz uygular.
    /// </summary>
    public static void SetEntityVelocity(CBaseEntity? entity, Vector velocity)
    {
        if (entity is not { IsValid: true }) return;
        try
        {
            entity.AbsVelocity = velocity;
            entity.Teleport(null, null, velocity);
        }
        catch { }
    }

    /// <summary>
    /// Oyuncunun yercekimi olcegini ayarlar (amx_hook.sma icindeki set_user_gravity karsiligi).
    /// </summary>
    public static void SetGravity(IPlayer? player, float gravityScale)
    {
        var pawn = GetPawn(player);
        if (pawn == null) return;

        try
        {
            pawn.GravityScale = gravityScale;
            pawn.ActualGravityScale = gravityScale;
            pawn.GravityScaleUpdated();
        }
        catch { }
    }

    /// <summary>
    /// Oyuncunun mevcut yercekimi olcegini okur.
    /// </summary>
    public static float GetGravity(IPlayer? player)
    {
        var pawn = GetPawn(player);
        if (pawn == null) return 1.0f;

        try
        {
            float g = pawn.GravityScale;
            return g > 0.01f ? g : 1.0f;
        }
        catch
        {
            return 1.0f;
        }
    }

    /// <summary>
    /// amx_hook.sma icindeki cmd_target(id, arg, ...) gibi hedef oyuncuyu isim, #slot veya SteamID64 ile bulur.
    /// </summary>
    public static IPlayer? FindTarget(ISwiftlyCore core, string query, out string? errorMessage)
    {
        errorMessage = null;
        var q = (query ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(q))
        {
            errorMessage = "Hedef oyuncu adı veya #slot belirtilmedi.";
            return null;
        }

        var online = core.PlayerManager.GetAllPlayers().Where(IsUsable).ToList();

        // 1. #slot veya #userid
        if (q.StartsWith('#') && int.TryParse(q.AsSpan(1), out int idVal))
        {
            var bySlot = online.FirstOrDefault(p => p!.Slot == idVal || p.UserID == idVal || p.PlayerID == idVal);
            if (bySlot != null) return bySlot;
            errorMessage = $"#{idVal} numaralı oyuncu bulunamadı.";
            return null;
        }

        // 2. SteamID64
        if (ulong.TryParse(q, out ulong sid) && sid >= 76561197960265728UL)
        {
            var bySid = online.FirstOrDefault(p => GetSteamId64(p) == sid);
            if (bySid != null) return bySid;
        }

        // 3. Tam isim eşleşmesi
        var exact = online.Where(p => GetName(p).Equals(q, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count == 1) return exact[0];

        // 4. Kısmi isim eşleşmesi
        var partial = online.Where(p => GetName(p).Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        if (partial.Count == 1) return partial[0];
        if (partial.Count > 1)
        {
            errorMessage = $"'{q}' ile eşleşen birden fazla oyuncu var ({string.Join(", ", partial.Select(GetName))}).";
            return null;
        }

        errorMessage = $"'{q}' adında bir oyuncu bulunamadı.";
        return null;
    }

    /// <summary>
    /// Renk etiketlerini normalize edip oyuncuya sohbet mesaji gonderir.
    /// </summary>
    public static void SendChat(IPlayer? player, string prefix, string message)
    {
        if (!IsUsable(player) || player!.IsFakeClient) return;
        try
        {
            var full = string.IsNullOrWhiteSpace(prefix) ? message : $"{prefix} {message}";
            player.SendChat(Helper.Colored(full));
        }
        catch { }
    }

    /// <summary>
    /// Sunucudaki tum oyunculara sohbet mesaji gonderir (amx_hook.sma client_print(0, print_chat, ...) karsiligi).
    /// </summary>
    public static void BroadcastChat(ISwiftlyCore core, string prefix, string message)
    {
        try
        {
            var full = string.IsNullOrWhiteSpace(prefix) ? message : $"{prefix} {message}";
            var colored = Helper.Colored(full);
            foreach (var p in core.PlayerManager.GetAllPlayers())
            {
                if (IsUsable(p) && !p!.IsFakeClient)
                {
                    p.SendChat(colored);
                }
            }
        }
        catch { }
    }
}

