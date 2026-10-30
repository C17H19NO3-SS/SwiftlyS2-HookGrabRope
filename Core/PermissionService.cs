using HookGrabRope.Config;
using HookGrabRope.Models;
using HookGrabRope.Utils;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace HookGrabRope.Core;

/// <summary>
/// admin.hook, admin.grab ve admin.rope yetkilerini, herkese acik modlari (hook_enabled vb.)
/// ve amx_give_hook / amx_take_hook ile verilen oyuncu bazli yetkileri yonetir.
/// </summary>
public sealed class PermissionService
{
    private readonly ISwiftlyCore _core;
    private PluginConfig _config;
    private readonly Dictionary<int, PlayerHgrState> _states;

    public PermissionService(ISwiftlyCore core, PluginConfig config, Dictionary<int, PlayerHgrState> states)
    {
        _core = core;
        _config = config;
        _states = states;
        RegisterPermissionHierarchy();
    }

    public void UpdateConfig(PluginConfig config)
    {
        _config = config;
        RegisterPermissionHierarchy();
    }

    /// <summary>
    /// Ust yonetici yetkilerinin (admin.root, admin.hgr) alt yetkileri (admin.hook, admin.grab, admin.rope)
    /// otomatik kapsamasini saglar.
    /// </summary>
    public void RegisterPermissionHierarchy()
    {
        try
        {
            var hookPerm = _config.Permissions.HookPermission;
            var grabPerm = _config.Permissions.GrabPermission;
            var ropePerm = _config.Permissions.RopePermission;
            var managePerm = _config.Permissions.AdminManagePermission;

            if (!string.IsNullOrWhiteSpace(managePerm))
            {
                if (!string.IsNullOrWhiteSpace(hookPerm))
                    _core.Permission.AddSubPermission(managePerm, hookPerm);
                if (!string.IsNullOrWhiteSpace(grabPerm))
                    _core.Permission.AddSubPermission(managePerm, grabPerm);
                if (!string.IsNullOrWhiteSpace(ropePerm))
                    _core.Permission.AddSubPermission(managePerm, ropePerm);
            }

            _core.Permission.AddSubPermission("admin.root", "admin.hook");
            _core.Permission.AddSubPermission("admin.root", "admin.grab");
            _core.Permission.AddSubPermission("admin.root", "admin.rope");
            if (!string.IsNullOrWhiteSpace(managePerm))
                _core.Permission.AddSubPermission("admin.root", managePerm);
        }
        catch { /* izin hiyerarsisi desteklenmiyorsa yoksay */ }
    }

    /// <summary>
    /// Oyuncunun dogrudan SwiftlyS2 yetki sisteminde ilgili yetkiye sahip olup olmadigini kontrol eder.
    /// </summary>
    public bool HasDirectPermission(IPlayer? player, string permissionNode)
    {
        if (!PlayerUtil.IsUsable(player) || string.IsNullOrWhiteSpace(permissionNode))
            return false;

        ulong sid = PlayerUtil.GetSteamId64(player);
        if (sid == 0) return false;

        try
        {
            if (_core.Permission.PlayerHasPermission(sid, permissionNode))
                return true;
            if (_core.Permission.PlayerHasPermission(sid, "admin.root") ||
                _core.Permission.PlayerHasPermission(sid, "admin.*") ||
                _core.Permission.PlayerHasPermission(sid, "*"))
                return true;
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Oyuncunun Hook kullanma izni var mi?
    /// (1: Herkese acik hook_enabled | 2: admin.hook yetkisi | 3: amx_give_hook ile verilmis yetki)
    /// </summary>
    public bool CanUseHook(IPlayer? player)
    {
        if (!PlayerUtil.IsUsable(player)) return false;
        if (_config.Hook.EnabledForAll) return true;
        if (HasDirectPermission(player, _config.Permissions.HookPermission)) return true;
        return HasGrantedAbility(player!, HgrAbility.Hook);
    }

    /// <summary>
    /// Oyuncunun Grab kullanma izni var mi?
    /// (1: Herkese acik grab_enabled | 2: admin.grab yetkisi | 3: amx_give_grab ile verilmis yetki)
    /// </summary>
    public bool CanUseGrab(IPlayer? player)
    {
        if (!PlayerUtil.IsUsable(player)) return false;
        if (_config.Grab.EnabledForAll) return true;
        if (HasDirectPermission(player, _config.Permissions.GrabPermission)) return true;
        return HasGrantedAbility(player!, HgrAbility.Grab);
    }

    /// <summary>
    /// Oyuncunun Rope kullanma izni var mi?
    /// (1: Herkese acik rope_enabled | 2: admin.rope yetkisi | 3: amx_give_rope ile verilmis yetki)
    /// </summary>
    public bool CanUseRope(IPlayer? player)
    {
        if (!PlayerUtil.IsUsable(player)) return false;
        if (_config.Rope.EnabledForAll) return true;
        if (HasDirectPermission(player, _config.Permissions.RopePermission)) return true;
        return HasGrantedAbility(player!, HgrAbility.Rope);
    }

    /// <summary>
    /// Oyuncu HGR yonetim menusunu (!hgrmenu / amx_hook_menu) ve give/take komutlarini kullanabilir mi?
    /// </summary>
    public bool CanManage(IPlayer? player)
    {
        if (player == null) return true; // Sunucu konsolu (RCON) her zaman yetkili
        if (!PlayerUtil.IsUsable(player)) return false;

        if (HasDirectPermission(player, _config.Permissions.AdminManagePermission))
            return true;

        if (_config.Permissions.AllowHookAdminsToManage &&
            HasDirectPermission(player, _config.Permissions.HookPermission))
            return true;

        return false;
    }

    /// <summary>
    /// amx_hook.sma satir 144 (get_user_flags(target) & ADMIN_LEVEL_Q) karsiligi:
    /// Hedef oyuncu admin yetkisine sahipse ve dokunulmazlik aciksa true doner.
    /// </summary>
    public bool HasAdminImmunity(IPlayer? target)
    {
        if (!_config.Permissions.AdminImmunity || !PlayerUtil.IsUsable(target))
            return false;

        return HasDirectPermission(target, _config.Permissions.AdminManagePermission) ||
               HasDirectPermission(target, _config.Permissions.HookPermission) ||
               HasDirectPermission(target, _config.Permissions.GrabPermission) ||
               HasDirectPermission(target, _config.Permissions.RopePermission);
    }

    /// <summary>
    /// Oyuncuya oturumluk (veya kalici config uzerinden) verilmis yetenegi kontrol eder.
    /// </summary>
    public bool HasGrantedAbility(IPlayer player, HgrAbility ability)
    {
        if (_states.TryGetValue(player.Slot, out var st) && (st.SessionGrantedAbilities & ability) != 0)
            return true;

        ulong sid = PlayerUtil.GetSteamId64(player);
        if (sid != 0)
        {
            var entry = _config.GrantedPlayers.FirstOrDefault(x => x.SteamId64 == sid);
            if (entry != null)
            {
                if ((ability & HgrAbility.Hook) != 0 && entry.Hook) return true;
                if ((ability & HgrAbility.Grab) != 0 && entry.Grab) return true;
                if ((ability & HgrAbility.Rope) != 0 && entry.Rope) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Oyuncuya belirli bir yetenegi (Hook, Grab veya Rope) verir veya geri alir.
    /// </summary>
    public void SetGrantedAbility(IPlayer target, HgrAbility ability, bool grant, bool saveToConfig = false)
    {
        if (!_states.TryGetValue(target.Slot, out var st))
        {
            st = new PlayerHgrState();
            _states[target.Slot] = st;
        }

        if (grant)
            st.SessionGrantedAbilities |= ability;
        else
            st.SessionGrantedAbilities &= ~ability;

        ulong sid = PlayerUtil.GetSteamId64(target);
        if (sid == 0) return;

        var entry = _config.GrantedPlayers.FirstOrDefault(x => x.SteamId64 == sid);
        if (saveToConfig || entry != null)
        {
            if (entry == null && grant)
            {
                entry = new GrantedPlayerEntry
                {
                    SteamId64 = sid,
                    Name = PlayerUtil.GetName(target)
                };
                _config.GrantedPlayers.Add(entry);
            }

            if (entry != null)
            {
                entry.Name = PlayerUtil.GetName(target);
                if ((ability & HgrAbility.Hook) != 0) entry.Hook = grant;
                if ((ability & HgrAbility.Grab) != 0) entry.Grab = grant;
                if ((ability & HgrAbility.Rope) != 0) entry.Rope = grant;

                if (!entry.Hook && !entry.Grab && !entry.Rope)
                    _config.GrantedPlayers.Remove(entry);
            }
        }
    }
}

