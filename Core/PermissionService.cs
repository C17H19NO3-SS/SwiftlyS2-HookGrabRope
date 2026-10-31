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

    private readonly Dictionary<ulong, int> _dbImmunityCache = new();
    private DateTime _lastImmunitySync = DateTime.MinValue;
    private readonly object _immunityLock = new();

    public PermissionService(ISwiftlyCore core, PluginConfig config, Dictionary<int, PlayerHgrState> states)
    {
        _core = core;
        _config = config;
        _states = states;
        RegisterPermissionHierarchy();
        SyncImmunityCacheAsync();
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
    /// Admins tablosundan yöneticilerin Immunity değerlerini asenkron olarak çeker ve önbelleğe alır.
    /// </summary>
    public void SyncImmunityCacheAsync()
    {
        Task.Run(async () =>
        {
            try
            {
                System.Data.IDbConnection? conn = null;
                try { conn = _core.Database.GetConnection("default"); }
                catch { try { conn = _core.Database.GetConnection("mysql_detailed"); } catch { } }

                if (conn == null) return;
                using (conn)
                {
                    if (conn is System.Data.Common.DbConnection dbConn)
                        await dbConn.OpenAsync();
                    else
                        conn.Open();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT SteamId64, Immunity FROM admins";

                    using var reader = cmd is System.Data.Common.DbCommand dbCmd
                        ? await dbCmd.ExecuteReaderAsync()
                        : cmd.ExecuteReader();

                    var dict = new Dictionary<ulong, int>();
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                        {
                            ulong sid = Convert.ToUInt64(reader.GetValue(0));
                            int imm = Convert.ToInt32(reader.GetValue(1));
                            dict[sid] = imm;
                        }
                    }

                    lock (_immunityLock)
                    {
                        _dbImmunityCache.Clear();
                        foreach (var kv in dict) _dbImmunityCache[kv.Key] = kv.Value;
                        _lastImmunitySync = DateTime.UtcNow;
                    }
                }
            }
            catch { /* Veritabanı veya admins tablosu yoksa izin bazlı dokunulmazlığa devam et */ }
        });
    }

    /// <summary>
    /// Oyuncunun dokunulmazlık seviyesini hesaplar.
    /// Kaynaklar: Veritabanı (admins tablosundaki Immunity), immunity.<sayı> yetkisi, admin rolleri.
    /// </summary>
    public int GetPlayerImmunity(IPlayer? player)
    {
        if (player == null) return int.MaxValue; // Sunucu konsolu (RCON) en yüksek dokunulmazlığa sahip
        if (!PlayerUtil.IsUsable(player)) return 0;

        ulong sid = PlayerUtil.GetSteamId64(player);
        if (sid == 0) return 0;

        // 60 saniyede bir veritabanı senkronizasyonu tetikle
        if ((DateTime.UtcNow - _lastImmunitySync).TotalSeconds > 60)
        {
            _lastImmunitySync = DateTime.UtcNow;
            SyncImmunityCacheAsync();
        }

        int immunity = 0;

        // 1. Veritabanı önbelleği
        lock (_immunityLock)
        {
            if (_dbImmunityCache.TryGetValue(sid, out var dbImm))
                immunity = Math.Max(immunity, dbImm);
        }

        // 2. SwiftlyS2 yetki düğümleri (immunity.100, immunity.50, admin.root, * vb.)
        try
        {
            var perms = _core.Permission.GetPlayerPermissions(sid);
            if (perms != null)
            {
                foreach (var p in perms)
                {
                    if (string.Equals(p, "*", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p, "admin.root", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p, "admins.*", StringComparison.OrdinalIgnoreCase))
                    {
                        immunity = Math.Max(immunity, 100);
                    }
                    else if (p.StartsWith("immunity.", StringComparison.OrdinalIgnoreCase) &&
                             int.TryParse(p.AsSpan("immunity.".Length), out var val))
                    {
                        immunity = Math.Max(immunity, val);
                    }
                }
            }
        }
        catch { }

        // 3. Varsayılan admin yetki seviyeleri (hiçbir dokunulmazlık belirtilmemişse)
        if (immunity == 0)
        {
            if (HasDirectPermission(player, _config.Permissions.AdminManagePermission))
                immunity = Math.Max(immunity, 90);
            else if (HasDirectPermission(player, _config.Permissions.HookPermission) ||
                     HasDirectPermission(player, _config.Permissions.GrabPermission) ||
                     HasDirectPermission(player, _config.Permissions.RopePermission))
                immunity = Math.Max(immunity, 50);
        }

        return immunity;
    }

    /// <summary>
    /// Sadece dokunulmazlığı senden yüksek olan kişilere grab atılamaz.
    /// Dokunulmazlığı eşit veya daha düşük olanlara (ve diğer adminlere) grab atılabilir.
    /// </summary>
    public bool CanGrabTarget(IPlayer? grabber, IPlayer? target, out int grabberImmunity, out int targetImmunity)
    {
        grabberImmunity = GetPlayerImmunity(grabber);
        targetImmunity = GetPlayerImmunity(target);

        if (target == null) return true;
        if (grabber != null && grabber.Slot == target.Slot) return true;

        if (!_config.Permissions.AdminImmunity)
            return true;

        // SADECE dokunulmazlığı senden yüksek olan kişilere grab atılamaz
        if (targetImmunity > grabberImmunity)
            return false;

        return true;
    }

    /// <summary>
    /// Geriye dönük uyumluluk için hedefin dokunulmazlık seviyesini kontrol eder.
    /// </summary>
    public bool HasAdminImmunity(IPlayer? target)
    {
        return GetPlayerImmunity(target) > 0;
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

