using System.Globalization;
using System.Text;
using Tomlyn;
using Tomlyn.Model;

namespace HookGrabRope.Config;

/// <summary>
/// hgr_config.toml dosyasini okuyan ve atomik olarak kaydeden yapilandirma yoneticisi.
/// </summary>
public sealed class ConfigLoader
{
    private readonly string _configDir;
    public string ConfigPath { get; }

    public ConfigLoader(string configDir)
    {
        _configDir = configDir;
        ConfigPath = Path.Combine(_configDir, "hgr_config.toml");
    }

    public PluginConfig Load()
    {
        Directory.CreateDirectory(_configDir);
        if (!File.Exists(ConfigPath))
        {
            File.WriteAllText(ConfigPath, DefaultConfigTemplate.Content, new UTF8Encoding(false));
        }

        var text = File.ReadAllText(ConfigPath, Encoding.UTF8);
        var model = Toml.ToModel(text);
        var cfg = new PluginConfig();

        if (model.TryGetValue("general", out var genObj) && genObj is TomlTable gen)
        {
            cfg.General.ChatPrefix = GetString(gen, "chat_prefix", cfg.General.ChatPrefix);
            cfg.General.FreezeWhileMenuOpen = GetBool(gen, "freeze_while_menu_open", cfg.General.FreezeWhileMenuOpen);
            cfg.General.EnableSounds = GetBool(gen, "enable_sounds", cfg.General.EnableSounds);
            cfg.General.ResetOnRoundStart = GetBool(gen, "reset_on_round_start", cfg.General.ResetOnRoundStart);
        }

        if (model.TryGetValue("permissions", out var permObj) && permObj is TomlTable perm)
        {
            cfg.Permissions.HookPermission = GetString(perm, "hook_permission", cfg.Permissions.HookPermission);
            cfg.Permissions.GrabPermission = GetString(perm, "grab_permission", cfg.Permissions.GrabPermission);
            cfg.Permissions.RopePermission = GetString(perm, "rope_permission", cfg.Permissions.RopePermission);
            cfg.Permissions.AdminManagePermission = GetString(perm, "admin_manage_permission", cfg.Permissions.AdminManagePermission);
            cfg.Permissions.AllowHookAdminsToManage = GetBool(perm, "allow_hook_admins_to_manage", cfg.Permissions.AllowHookAdminsToManage);
            cfg.Permissions.AdminImmunity = GetBool(perm, "admin_immunity", cfg.Permissions.AdminImmunity);
        }

        if (model.TryGetValue("hook", out var hookObj) && hookObj is TomlTable hook)
        {
            cfg.Hook.EnabledForAll = GetBool(hook, "enabled_for_all", cfg.Hook.EnabledForAll);
            cfg.Hook.Speed = Math.Clamp(GetFloat(hook, "speed", cfg.Hook.Speed), 0.5f, 30.0f);
            cfg.Hook.MaxDistance = Math.Clamp(GetFloat(hook, "max_distance", cfg.Hook.MaxDistance), 200.0f, 32768.0f);
            cfg.Hook.ArrivalStopDistance = Math.Clamp(GetFloat(hook, "arrival_stop_distance", cfg.Hook.ArrivalStopDistance), 8.0f, 150.0f);
            cfg.Hook.AllowAirControl = GetBool(hook, "allow_air_control", cfg.Hook.AllowAirControl);
            cfg.Hook.BeamWidth = Math.Clamp(GetFloat(hook, "beam_width", cfg.Hook.BeamWidth), 0.5f, 15.0f);
            cfg.Hook.BeamColor = GetString(hook, "beam_color", cfg.Hook.BeamColor);
            cfg.Hook.SoundEventName = GetString(hook, "sound_event_name", cfg.Hook.SoundEventName);
        }

        if (model.TryGetValue("grab", out var grabObj) && grabObj is TomlTable grab)
        {
            cfg.Grab.EnabledForAll = GetBool(grab, "enabled_for_all", cfg.Grab.EnabledForAll);
            cfg.Grab.Speed = Math.Clamp(GetFloat(grab, "speed", cfg.Grab.Speed), 0.5f, 30.0f);
            cfg.Grab.MaxDistance = Math.Clamp(GetFloat(grab, "max_distance", cfg.Grab.MaxDistance), 200.0f, 16384.0f);
            cfg.Grab.MinHoldDistance = Math.Clamp(GetFloat(grab, "min_hold_distance", cfg.Grab.MinHoldDistance), 40.0f, 500.0f);
            cfg.Grab.MaxHoldDistance = Math.Clamp(GetFloat(grab, "max_hold_distance", cfg.Grab.MaxHoldDistance), 100.0f, 8192.0f);
            cfg.Grab.PushPullStep = Math.Clamp(GetFloat(grab, "push_pull_step", cfg.Grab.PushPullStep), 2.0f, 100.0f);
            cfg.Grab.ThrowForce = Math.Clamp(GetFloat(grab, "throw_force", cfg.Grab.ThrowForce), 100.0f, 4000.0f);
            cfg.Grab.AllowGrabEntities = GetBool(grab, "allow_grab_entities", cfg.Grab.AllowGrabEntities);
            cfg.Grab.BeamWidth = Math.Clamp(GetFloat(grab, "beam_width", cfg.Grab.BeamWidth), 0.5f, 15.0f);
            cfg.Grab.BeamColor = GetString(grab, "beam_color", cfg.Grab.BeamColor);
            cfg.Grab.SoundEventName = GetString(grab, "sound_event_name", cfg.Grab.SoundEventName);
            cfg.Grab.ShowCenterHud = GetBool(grab, "show_center_hud", cfg.Grab.ShowCenterHud);
        }

        if (model.TryGetValue("rope", out var ropeObj) && ropeObj is TomlTable rope)
        {
            cfg.Rope.EnabledForAll = GetBool(rope, "enabled_for_all", cfg.Rope.EnabledForAll);
            cfg.Rope.Speed = Math.Clamp(GetFloat(rope, "speed", cfg.Rope.Speed), 0.5f, 30.0f);
            cfg.Rope.MaxDistance = Math.Clamp(GetFloat(rope, "max_distance", cfg.Rope.MaxDistance), 200.0f, 32768.0f);
            cfg.Rope.MinLength = Math.Clamp(GetFloat(rope, "min_length", cfg.Rope.MinLength), 30.0f, 500.0f);
            cfg.Rope.MaxLength = Math.Clamp(GetFloat(rope, "max_length", cfg.Rope.MaxLength), 150.0f, 16384.0f);
            cfg.Rope.ClimbStep = Math.Clamp(GetFloat(rope, "climb_step", cfg.Rope.ClimbStep), 1.0f, 50.0f);
            cfg.Rope.SwingAirBoost = Math.Clamp(GetFloat(rope, "swing_air_boost", cfg.Rope.SwingAirBoost), 0.0f, 100.0f);
            cfg.Rope.BeamWidth = Math.Clamp(GetFloat(rope, "beam_width", cfg.Rope.BeamWidth), 0.5f, 15.0f);
            cfg.Rope.BeamColor = GetString(rope, "beam_color", cfg.Rope.BeamColor);
            cfg.Rope.SoundEventName = GetString(rope, "sound_event_name", cfg.Rope.SoundEventName);
        }

        if (model.TryGetValue("granted_players", out var gpObj) && gpObj is TomlTableArray gpArr)
        {
            foreach (var item in gpArr)
            {
                ulong sid = GetUlong(item, "steam_id", 0UL);
                if (sid == 0) continue;

                cfg.GrantedPlayers.Add(new GrantedPlayerEntry
                {
                    SteamId64 = sid,
                    Name = GetString(item, "name", string.Empty),
                    Hook = GetBool(item, "hook", false),
                    Grab = GetBool(item, "grab", false),
                    Rope = GetBool(item, "rope", false)
                });
            }
        }

        return cfg;
    }

    public void Save(PluginConfig cfg)
    {
        Directory.CreateDirectory(_configDir);
        var sb = new StringBuilder();

        sb.AppendLine("# ==============================================================================");
        sb.AppendLine("# HookGrabRope (HGR) - CS2 SwiftlyS2 Yapılandırma Dosyası");
        sb.AppendLine("# ==============================================================================");
        sb.AppendLine();

        sb.AppendLine("[general]");
        sb.AppendLine($"chat_prefix = {Quote(cfg.General.ChatPrefix)}");
        sb.AppendLine($"freeze_while_menu_open = {B(cfg.General.FreezeWhileMenuOpen)}");
        sb.AppendLine($"enable_sounds = {B(cfg.General.EnableSounds)}");
        sb.AppendLine($"reset_on_round_start = {B(cfg.General.ResetOnRoundStart)}");
        sb.AppendLine();

        sb.AppendLine("[permissions]");
        sb.AppendLine($"hook_permission = {Quote(cfg.Permissions.HookPermission)}");
        sb.AppendLine($"grab_permission = {Quote(cfg.Permissions.GrabPermission)}");
        sb.AppendLine($"rope_permission = {Quote(cfg.Permissions.RopePermission)}");
        sb.AppendLine($"admin_manage_permission = {Quote(cfg.Permissions.AdminManagePermission)}");
        sb.AppendLine($"allow_hook_admins_to_manage = {B(cfg.Permissions.AllowHookAdminsToManage)}");
        sb.AppendLine($"admin_immunity = {B(cfg.Permissions.AdminImmunity)}");
        sb.AppendLine();

        sb.AppendLine("[hook]");
        sb.AppendLine($"enabled_for_all = {B(cfg.Hook.EnabledForAll)}");
        sb.AppendLine($"speed = {F(cfg.Hook.Speed)}");
        sb.AppendLine($"max_distance = {F(cfg.Hook.MaxDistance)}");
        sb.AppendLine($"arrival_stop_distance = {F(cfg.Hook.ArrivalStopDistance)}");
        sb.AppendLine($"allow_air_control = {B(cfg.Hook.AllowAirControl)}");
        sb.AppendLine($"beam_width = {F(cfg.Hook.BeamWidth)}");
        sb.AppendLine($"beam_color = {Quote(cfg.Hook.BeamColor)}");
        sb.AppendLine($"sound_event_name = {Quote(cfg.Hook.SoundEventName)}");
        sb.AppendLine();

        sb.AppendLine("[grab]");
        sb.AppendLine($"enabled_for_all = {B(cfg.Grab.EnabledForAll)}");
        sb.AppendLine($"speed = {F(cfg.Grab.Speed)}");
        sb.AppendLine($"max_distance = {F(cfg.Grab.MaxDistance)}");
        sb.AppendLine($"min_hold_distance = {F(cfg.Grab.MinHoldDistance)}");
        sb.AppendLine($"max_hold_distance = {F(cfg.Grab.MaxHoldDistance)}");
        sb.AppendLine($"push_pull_step = {F(cfg.Grab.PushPullStep)}");
        sb.AppendLine($"throw_force = {F(cfg.Grab.ThrowForce)}");
        sb.AppendLine($"allow_grab_entities = {B(cfg.Grab.AllowGrabEntities)}");
        sb.AppendLine($"beam_width = {F(cfg.Grab.BeamWidth)}");
        sb.AppendLine($"beam_color = {Quote(cfg.Grab.BeamColor)}");
        sb.AppendLine($"sound_event_name = {Quote(cfg.Grab.SoundEventName)}");
        sb.AppendLine($"show_center_hud = {B(cfg.Grab.ShowCenterHud)}");
        sb.AppendLine();

        sb.AppendLine("[rope]");
        sb.AppendLine($"enabled_for_all = {B(cfg.Rope.EnabledForAll)}");
        sb.AppendLine($"speed = {F(cfg.Rope.Speed)}");
        sb.AppendLine($"max_distance = {F(cfg.Rope.MaxDistance)}");
        sb.AppendLine($"min_length = {F(cfg.Rope.MinLength)}");
        sb.AppendLine($"max_length = {F(cfg.Rope.MaxLength)}");
        sb.AppendLine($"climb_step = {F(cfg.Rope.ClimbStep)}");
        sb.AppendLine($"swing_air_boost = {F(cfg.Rope.SwingAirBoost)}");
        sb.AppendLine($"beam_width = {F(cfg.Rope.BeamWidth)}");
        sb.AppendLine($"beam_color = {Quote(cfg.Rope.BeamColor)}");
        sb.AppendLine($"sound_event_name = {Quote(cfg.Rope.SoundEventName)}");
        sb.AppendLine();

        foreach (var gp in cfg.GrantedPlayers.Where(x => x.SteamId64 != 0 && (x.Hook || x.Grab || x.Rope)))
        {
            sb.AppendLine("[[granted_players]]");
            sb.AppendLine($"steam_id = {gp.SteamId64}");
            sb.AppendLine($"name = {Quote(gp.Name)}");
            sb.AppendLine($"hook = {B(gp.Hook)}");
            sb.AppendLine($"grab = {B(gp.Grab)}");
            sb.AppendLine($"rope = {B(gp.Rope)}");
            sb.AppendLine();
        }

        var tmpPath = ConfigPath + ".tmp";
        File.WriteAllText(tmpPath, sb.ToString(), new UTF8Encoding(false));
        File.Move(tmpPath, ConfigPath, overwrite: true);
    }

    private static string Quote(string s)
        => $"\"{(s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

    private static string B(bool b) => b ? "true" : "false";

    private static string F(float f) => f.ToString("0.0#", CultureInfo.InvariantCulture);

    private static string GetString(TomlTable t, string key, string def)
        => t.TryGetValue(key, out var v) && v != null ? v.ToString() ?? def : def;

    private static bool GetBool(TomlTable t, string key, bool def)
    {
        if (!t.TryGetValue(key, out var v) || v == null) return def;
        if (v is bool b) return b;
        return bool.TryParse(v.ToString(), out var parsed) ? parsed : def;
    }

    private static float GetFloat(TomlTable t, string key, float def)
    {
        if (!t.TryGetValue(key, out var v) || v == null) return def;
        return v switch
        {
            double d => (float)d,
            float f => f,
            long l => l,
            int i => i,
            _ => float.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : def
        };
    }

    private static ulong GetUlong(TomlTable t, string key, ulong def)
    {
        if (!t.TryGetValue(key, out var v) || v == null) return def;
        return v switch
        {
            ulong u => u,
            long l and >= 0 => (ulong)l,
            _ => ulong.TryParse(v.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : def
        };
    }
}

