using HookGrabRope.Config;
using HookGrabRope.Core;
using HookGrabRope.Models;
using HookGrabRope.Utils;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;

namespace HookGrabRope;

[PluginMetadata(
    Id = "hookgrabrope",
    Version = "1.0.0",
    Name = "HookGrabRope (HGR)",
    Author = "CanDaysa & Devrim",
    Description = "CS2 icin SwiftlyS2 tabanli Hook (admin.hook), Grab (admin.grab) ve Rope (admin.rope) eklentisi."
)]
public sealed class Plugin(ISwiftlyCore core) : BasePlugin(core)
{
    private PluginConfig _config = new();
    private ConfigLoader? _configLoader;
    private readonly Dictionary<int, PlayerHgrState> _states = new();

    private PermissionService? _permissions;
    private HgrEngine? _engine;
    private HgrAdminMenu? _adminMenu;

    private readonly List<Guid> _commandGuids = new();
    private readonly List<Guid> _gameEventGuids = new();
    private Guid _clientCommandHookGuid;

    // amx_hook.sma uyumlu ConVar'lar + Grab/Rope ConVar'ları
    private IConVar<float>? _cvarHookSpeed;
    private IConVar<bool>? _cvarHookEnabled;
    private IConVar<float>? _cvarGrabSpeed;
    private IConVar<bool>? _cvarGrabEnabled;
    private IConVar<float>? _cvarRopeSpeed;
    private IConVar<bool>? _cvarRopeEnabled;

    private int _cvarSyncTickCounter;

    public override void Load(bool hotReload)
    {
        var configDir = ResolveConfigDirectory();
        _configLoader = new ConfigLoader(configDir);

        try
        {
            _config = _configLoader.Load();
        }
        catch (Exception ex)
        {
            Core.Logger.LogError(ex, "[HookGrabRope] Config yüklenemedi, varsayılan ayarlar kullanılıyor.");
            _config = new PluginConfig();
        }

        _permissions = new PermissionService(Core, _config, _states);
        _engine = new HgrEngine(Core, _config, _permissions, _states);
        _adminMenu = new HgrAdminMenu(Core, _configLoader, _config, _permissions, _engine, SyncConfigToConVars);

        InitializeConVars();
        RegisterCommands();
        RegisterEvents();

        Core.Logger.LogInformation(
            "[HookGrabRope] v1.0.0 yüklendi! Yetkiler: Hook={HookPerm}, Grab={GrabPerm}, Rope={RopePerm}",
            _config.Permissions.HookPermission,
            _config.Permissions.GrabPermission,
            _config.Permissions.RopePermission);
    }

    public override void Unload()
    {
        _adminMenu?.CloseAll();
        _engine?.ResetAllActiveAbilities();
        _states.Clear();

        UnregisterEvents();
        UnregisterCommands();

        Core.Logger.LogInformation("[HookGrabRope] Eklenti kapatıldı ve tüm aktif ışınlar/yerçekimleri temizlendi.");
    }

    private string ResolveConfigDirectory()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(Core.Configuration.BasePath))
                return Core.Configuration.BasePath;
        }
        catch { }

        return Path.Combine(Core.PluginPath, "HookGrabRope");
    }

    // ==============================================================================
    // CONVAR YONETIMI (amx_hook.sma: hook_speed, hook_enabled + grab/rope)
    // ==============================================================================

    private void InitializeConVars()
    {
        try
        {
            _cvarHookSpeed = Core.ConVar.Create("hook_speed", "Hook hiz carpani (Varsayilan: 5.0)", _config.Hook.Speed, 0.5f, 30.0f);
            _cvarHookEnabled = Core.ConVar.Create("hook_enabled", "Herkesin Hook kullanabilmesini acar/kapatir (0/1)", _config.Hook.EnabledForAll);

            _cvarGrabSpeed = Core.ConVar.Create("grab_speed", "Grab cekim hizi carpani (Varsayilan: 5.0)", _config.Grab.Speed, 0.5f, 30.0f);
            _cvarGrabEnabled = Core.ConVar.Create("grab_enabled", "Herkesin Grab kullanabilmesini acar/kapatir (0/1)", _config.Grab.EnabledForAll);

            _cvarRopeSpeed = Core.ConVar.Create("rope_speed", "Rope sallanma hizi carpani (Varsayilan: 5.0)", _config.Rope.Speed, 0.5f, 30.0f);
            _cvarRopeEnabled = Core.ConVar.Create("rope_enabled", "Herkesin Rope kullanabilmesini acar/kapatir (0/1)", _config.Rope.EnabledForAll);
        }
        catch (Exception ex)
        {
            Core.Logger.LogWarning(ex, "[HookGrabRope] ConVar oluşturulurken uyarı oluştu.");
        }
    }

    private void SyncConfigToConVars()
    {
        try
        {
            if (_cvarHookSpeed != null) _cvarHookSpeed.Value = _config.Hook.Speed;
            if (_cvarHookEnabled != null) _cvarHookEnabled.Value = _config.Hook.EnabledForAll;

            if (_cvarGrabSpeed != null) _cvarGrabSpeed.Value = _config.Grab.Speed;
            if (_cvarGrabEnabled != null) _cvarGrabEnabled.Value = _config.Grab.EnabledForAll;

            if (_cvarRopeSpeed != null) _cvarRopeSpeed.Value = _config.Rope.Speed;
            if (_cvarRopeEnabled != null) _cvarRopeEnabled.Value = _config.Rope.EnabledForAll;
        }
        catch { }
    }

    private void PollConVarChanges()
    {
        try
        {
            if (_cvarHookSpeed != null && Math.Abs(_cvarHookSpeed.Value - _config.Hook.Speed) > 0.01f)
                _config.Hook.Speed = Math.Clamp(_cvarHookSpeed.Value, 0.5f, 30.0f);

            if (_cvarHookEnabled != null && _cvarHookEnabled.Value != _config.Hook.EnabledForAll)
                _config.Hook.EnabledForAll = _cvarHookEnabled.Value;

            if (_cvarGrabSpeed != null && Math.Abs(_cvarGrabSpeed.Value - _config.Grab.Speed) > 0.01f)
                _config.Grab.Speed = Math.Clamp(_cvarGrabSpeed.Value, 0.5f, 30.0f);

            if (_cvarGrabEnabled != null && _cvarGrabEnabled.Value != _config.Grab.EnabledForAll)
                _config.Grab.EnabledForAll = _cvarGrabEnabled.Value;

            if (_cvarRopeSpeed != null && Math.Abs(_cvarRopeSpeed.Value - _config.Rope.Speed) > 0.01f)
                _config.Rope.Speed = Math.Clamp(_cvarRopeSpeed.Value, 0.5f, 30.0f);

            if (_cvarRopeEnabled != null && _cvarRopeEnabled.Value != _config.Rope.EnabledForAll)
                _config.Rope.EnabledForAll = _cvarRopeEnabled.Value;
        }
        catch { }
    }

    // ==============================================================================
    // KOMUT KAYITLARI (+hook/-hook, +grab/-grab, +rope/-rope, give/take, menu)
    // ==============================================================================

    private void RegisterCommands()
    {
        // 1. İstemci konsolundan gelen ham +hook / -hook / +grab / -grab / +rope / -rope yakalayıcısı
        _clientCommandHookGuid = Core.Command.HookClientCommand(OnClientCommandHook);

        // 2. Doğrudan konsol (+hook/-hook vb.) ve toggle komutları
        AddCmd("+hook", OnHookOnCmd, registerRaw: true, help: "Use: bind <key> +hook");
        AddCmd("-hook", OnHookOffCmd, registerRaw: true, help: "Stops active hook");
        AddCmd("hook_toggle", OnHookToggleCmd, registerRaw: true, help: "Toggles your hook on and off");
        AddCmd("hook", OnHookToggleCmd, registerRaw: false, help: "Toggles your hook on and off (!hook)");

        AddCmd("+grab", OnGrabOnCmd, registerRaw: true, help: "Use: bind <key> +grab");
        AddCmd("-grab", OnGrabOffCmd, registerRaw: true, help: "Releases grabbed target");
        AddCmd("grab_toggle", OnGrabToggleCmd, registerRaw: true, help: "Toggles your grab on and off");
        AddCmd("grab", OnGrabToggleCmd, registerRaw: false, help: "Toggles your grab on and off (!grab)");

        AddCmd("+push", OnPushOnCmd, registerRaw: true, help: "Pushes grabbed target away");
        AddCmd("-push", OnPushOffCmd, registerRaw: true, help: "Stops pushing grabbed target");
        AddCmd("+pull", OnPullOnCmd, registerRaw: true, help: "Pulls grabbed target closer");
        AddCmd("-pull", OnPullOffCmd, registerRaw: true, help: "Stops pulling grabbed target");

        AddCmd("+rope", OnRopeOnCmd, registerRaw: true, help: "Use: bind <key> +rope");
        AddCmd("-rope", OnRopeOffCmd, registerRaw: true, help: "Releases active rope");
        AddCmd("rope_toggle", OnRopeToggleCmd, registerRaw: true, help: "Toggles your rope on and off");
        AddCmd("rope", OnRopeToggleCmd, registerRaw: false, help: "Toggles your rope on and off (!rope)");

        // 3. amx_hook.sma uyumlu Give / Take komutları + Grab ve Rope karşılıkları
        AddCmd("amx_give_hook", ctx => HandleGiveTakeCommand(ctx, HgrAbility.Hook, grant: true), registerRaw: true,
            help: "Give a player the ability to hook <nick|#userid>");
        AddCmd("amx_take_hook", ctx => HandleGiveTakeCommand(ctx, HgrAbility.Hook, grant: false), registerRaw: true,
            help: "Take a player's ability to hook <nick|#userid>");
        AddCmd("givehook", ctx => HandleGiveTakeCommand(ctx, HgrAbility.Hook, grant: true), registerRaw: false,
            help: "Bir oyuncuya Hook yetkisi verir: !givehook <isim>");
        AddCmd("takehook", ctx => HandleGiveTakeCommand(ctx, HgrAbility.Hook, grant: false), registerRaw: false,
            help: "Bir oyuncunun Hook yetkisini alır: !takehook <isim>");

        AddCmd("amx_give_grab", ctx => HandleGiveTakeCommand(ctx, HgrAbility.Grab, grant: true), registerRaw: true,
            help: "Give a player the ability to grab <nick|#userid>");
        AddCmd("amx_take_grab", ctx => HandleGiveTakeCommand(ctx, HgrAbility.Grab, grant: false), registerRaw: true,
            help: "Take a player's ability to grab <nick|#userid>");
        AddCmd("givegrab", ctx => HandleGiveTakeCommand(ctx, HgrAbility.Grab, grant: true), registerRaw: false,
            help: "Bir oyuncuya Grab yetkisi verir: !givegrab <isim>");
        AddCmd("takegrab", ctx => HandleGiveTakeCommand(ctx, HgrAbility.Grab, grant: false), registerRaw: false,
            help: "Bir oyuncunun Grab yetkisini alır: !takegrab <isim>");

        AddCmd("amx_give_rope", ctx => HandleGiveTakeCommand(ctx, HgrAbility.Rope, grant: true), registerRaw: true,
            help: "Give a player the ability to rope <nick|#userid>");
        AddCmd("amx_take_rope", ctx => HandleGiveTakeCommand(ctx, HgrAbility.Rope, grant: false), registerRaw: true,
            help: "Take a player's ability to rope <nick|#userid>");
        AddCmd("giverope", ctx => HandleGiveTakeCommand(ctx, HgrAbility.Rope, grant: true), registerRaw: false,
            help: "Bir oyuncuya Rope yetkisi verir: !giverope <isim>");
        AddCmd("takerope", ctx => HandleGiveTakeCommand(ctx, HgrAbility.Rope, grant: false), registerRaw: false,
            help: "Bir oyuncunun Rope yetkisini alır: !takerope <isim>");

        AddCmd("amx_give_hgr", ctx => HandleGiveTakeCommand(ctx, HgrAbility.All, grant: true), registerRaw: true,
            help: "Give a player Hook, Grab and Rope <nick|#userid>");
        AddCmd("amx_take_hgr", ctx => HandleGiveTakeCommand(ctx, HgrAbility.All, grant: false), registerRaw: true,
            help: "Take a player's Hook, Grab and Rope <nick|#userid>");
        AddCmd("hgrver", ctx => HandleGiveTakeCommand(ctx, HgrAbility.All, grant: true), registerRaw: false,
            help: "Bir oyuncuya tüm HGR yetkilerini verir: !hgrver <isim>");
        AddCmd("hgral", ctx => HandleGiveTakeCommand(ctx, HgrAbility.All, grant: false), registerRaw: false,
            help: "Bir oyuncunun tüm HGR yetkilerini alır: !hgral <isim>");

        // 4. Menü ve yönetim komutları (sw_hookmenu / !hookmenu)
        AddCmd("hookmenu", OnMenuCommand, registerRaw: false,
            help: "HGR görsel yönetim menüsünü açar (sw_hookmenu / !hookmenu)");
        AddCmd("hgr", OnHgrCommand, registerRaw: false,
            help: "HGR bilgi ve yönetim menüsünü açar (!hgr)");
        AddCmd("hgrreload", OnReloadCommand, registerRaw: false,
            help: "HGR yapılandırma dosyasını yeniden yükler (!hgrreload)");
    }

    private void AddCmd(string name, ICommandService.CommandListener handler, bool registerRaw, string help)
    {
        try
        {
            var guid = Core.Command.RegisterCommand(name, handler, registerRaw, permission: string.Empty, helpText: help);
            _commandGuids.Add(guid);
        }
        catch (Exception ex)
        {
            Core.Logger.LogWarning(ex, "[HookGrabRope] '{Cmd}' komutu kaydedilemedi.", name);
        }
    }

    private void UnregisterCommands()
    {
        if (_clientCommandHookGuid != Guid.Empty)
        {
            try { Core.Command.UnhookClientCommand(_clientCommandHookGuid); } catch { }
            _clientCommandHookGuid = Guid.Empty;
        }

        foreach (var guid in _commandGuids)
        {
            try { Core.Command.UnregisterCommand(guid); } catch { }
        }
        _commandGuids.Clear();
    }

    /// <summary>
    /// Oyuncularin konsola 'bind <tus> +hook' / '+grab' / '+rope' atadiginda gonderilen
    /// + ve - komutlarini yakalar ve 'Unknown command' uyarisini engeller.
    /// </summary>
    private HookResult OnClientCommandHook(int playerId, string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine) || _engine == null)
            return HookResult.Continue;

        var player = Core.PlayerManager.GetPlayer(playerId);
        if (!PlayerUtil.IsUsable(player))
            return HookResult.Continue;

        var span = commandLine.AsSpan().Trim();
        int spaceIdx = span.IndexOf(' ');
        var cmdToken = (spaceIdx >= 0 ? span[..spaceIdx] : span).ToString();

        switch (cmdToken.ToLowerInvariant())
        {
            case "+hook":
            case "sw_+hook":
                _engine.StartHook(player!);
                return HookResult.Handled;

            case "-hook":
            case "sw_-hook":
                _engine.StopHook(player!);
                return HookResult.Handled;

            case "hook_toggle":
            case "sw_hook_toggle":
                if (CanToggleNow(player!.Slot)) _engine.ToggleHook(player!);
                return HookResult.Handled;

            case "+grab":
            case "sw_+grab":
                _engine.StartGrab(player!);
                return HookResult.Handled;

            case "-grab":
            case "sw_-grab":
                _engine.StopGrab(player!);
                return HookResult.Handled;

            case "grab_toggle":
            case "sw_grab_toggle":
                if (CanToggleNow(player!.Slot)) _engine.ToggleGrab(player!);
                return HookResult.Handled;

            case "+push":
            case "sw_+push":
                _engine.SetPushState(player!, true);
                return HookResult.Handled;

            case "-push":
            case "sw_-push":
                _engine.SetPushState(player!, false);
                return HookResult.Handled;

            case "+pull":
            case "sw_+pull":
                _engine.SetPullState(player!, true);
                return HookResult.Handled;

            case "-pull":
            case "sw_-pull":
                _engine.SetPullState(player!, false);
                return HookResult.Handled;

            case "+rope":
            case "sw_+rope":
                _engine.StartRope(player!);
                return HookResult.Handled;

            case "-rope":
            case "sw_-rope":
                _engine.StopRope(player!);
                return HookResult.Handled;

            case "rope_toggle":
            case "sw_rope_toggle":
                if (CanToggleNow(player!.Slot)) _engine.ToggleRope(player!);
                return HookResult.Handled;

            default:
                return HookResult.Continue;
        }
    }

    private bool CanToggleNow(int slot)
    {
        if (_engine == null) return false;
        var st = _engine.GetOrCreateState(slot);
        float now = Core.Engine.GlobalVars.CurrentTime;
        if (now - st.LastCommandToggleTime < 0.05f)
            return false;
        st.LastCommandToggleTime = now;
        return true;
    }

    private void OnHookOnCmd(ICommandContext ctx)
    {
        if (ctx.Sender is { } p) _engine?.StartHook(p);
    }

    private void OnHookOffCmd(ICommandContext ctx)
    {
        if (ctx.Sender is { } p) _engine?.StopHook(p);
    }

    private void OnHookToggleCmd(ICommandContext ctx)
    {
        if (ctx.Sender is { } p && CanToggleNow(p.Slot)) _engine?.ToggleHook(p);
    }

    private void OnGrabOnCmd(ICommandContext ctx)
    {
        if (ctx.Sender is { } p) _engine?.StartGrab(p);
    }

    private void OnGrabOffCmd(ICommandContext ctx)
    {
        if (ctx.Sender is { } p) _engine?.StopGrab(p);
    }

    private void OnGrabToggleCmd(ICommandContext ctx)
    {
        if (ctx.Sender is { } p && CanToggleNow(p.Slot)) _engine?.ToggleGrab(p);
    }

    private void OnPushOnCmd(ICommandContext ctx)
    {
        if (ctx.Sender is { } p) _engine?.SetPushState(p, true);
    }

    private void OnPushOffCmd(ICommandContext ctx)
    {
        if (ctx.Sender is { } p) _engine?.SetPushState(p, false);
    }

    private void OnPullOnCmd(ICommandContext ctx)
    {
        if (ctx.Sender is { } p) _engine?.SetPullState(p, true);
    }

    private void OnPullOffCmd(ICommandContext ctx)
    {
        if (ctx.Sender is { } p) _engine?.SetPullState(p, false);
    }

    private void OnRopeOnCmd(ICommandContext ctx)
    {
        if (ctx.Sender is { } p) _engine?.StartRope(p);
    }

    private void OnRopeOffCmd(ICommandContext ctx)
    {
        if (ctx.Sender is { } p) _engine?.StopRope(p);
    }

    private void OnRopeToggleCmd(ICommandContext ctx)
    {
        if (ctx.Sender is { } p && CanToggleNow(p.Slot)) _engine?.ToggleRope(p);
    }

    /// <summary>
    /// amx_hook.sma icindeki cmd_givetake (satir 126-185) karsiligi:
    /// amx_give_hook, amx_take_hook, amx_give_grab, amx_take_grab, amx_give_rope, amx_take_rope.
    /// </summary>
    private void HandleGiveTakeCommand(ICommandContext ctx, HgrAbility ability, bool grant)
    {
        if (_permissions == null || _engine == null) return;

        var sender = ctx.Sender;
        if (sender != null && !_permissions.CanManage(sender))
        {
            ReplyToContext(ctx, "[red]Bu komutu kullanmak için yetkiniz yok.");
            return;
        }

        if (ctx.Args.Length < 1 || string.IsNullOrWhiteSpace(ctx.Args[0]))
        {
            ReplyToContext(ctx, $"[yellow]Kullanım: {ctx.CommandName} <oyuncu_adı | #slot>");
            return;
        }

        var target = PlayerUtil.FindTarget(Core, ctx.Args[0], out var error);
        if (target == null)
        {
            ReplyToContext(ctx, $"[red]{error}");
            return;
        }

        string targetName = PlayerUtil.GetName(target);
        if (_permissions.HasAdminImmunity(target) && (sender == null || sender.Slot != target.Slot))
        {
            ReplyToContext(ctx, $"[red]Admin {targetName} üzerinde yetki verme/alma işlemi yapılamaz.");
            return;
        }

        string abilityLabel = ability switch
        {
            HgrAbility.Hook => "Hook",
            HgrAbility.Grab => "Grab",
            HgrAbility.Rope => "Rope",
            _ => "Hook/Grab/Rope"
        };

        string bindHint = ability switch
        {
            HgrAbility.Hook => "bind <tuş> +hook",
            HgrAbility.Grab => "bind <tuş> +grab",
            HgrAbility.Rope => "bind <tuş> +rope",
            _ => "bind <tuş> +hook / +grab / +rope"
        };

        bool alreadyHas = _permissions.HasGrantedAbility(target, ability);
        if (grant)
        {
            if (alreadyHas)
            {
                ReplyToContext(ctx, $"[yellow]{targetName} zaten {abilityLabel} yetkisine sahip.");
                return;
            }

            _permissions.SetGrantedAbility(target, ability, grant: true, saveToConfig: false);
            ReplyToContext(ctx, $"[lime]{targetName}[default] oyuncusuna [gold]{abilityLabel}[default] yetkisi verildi.");
            PlayerUtil.SendChat(target, _config.General.ChatPrefix,
                $"[lime]Bir admin size {abilityLabel} yetkisi verdi! Konsola: [yellow]{bindHint}");
        }
        else
        {
            if (!alreadyHas)
            {
                ReplyToContext(ctx, $"[yellow]{targetName} zaten {abilityLabel} yetkisine sahip değil.");
                return;
            }

            _permissions.SetGrantedAbility(target, ability, grant: false, saveToConfig: false);
            if ((ability & HgrAbility.Hook) != 0) _engine.StopHook(target);
            if ((ability & HgrAbility.Grab) != 0) _engine.StopGrab(target);
            if ((ability & HgrAbility.Rope) != 0) _engine.StopRope(target);

            ReplyToContext(ctx, $"[red]{targetName}[default] oyuncusunun [gold]{abilityLabel}[default] yetkisi geri alındı.");
            PlayerUtil.SendChat(target, _config.General.ChatPrefix,
                $"[red]Bir admin {abilityLabel} yetkinizi geri aldı.");
        }
    }

    private void OnMenuCommand(ICommandContext ctx)
    {
        if (_permissions == null || _adminMenu == null) return;

        if (ctx.Sender is not { } player)
        {
            ctx.Reply("[HGR] Bu menü sadece oyun içinden açılabilir.");
            return;
        }

        if (!_permissions.CanManage(player))
        {
            PlayerUtil.SendChat(player, _config.General.ChatPrefix,
                $"[red]HGR menüsünü açmak için yetkiniz yok ([yellow]{_config.Permissions.AdminManagePermission}[red]).");
            return;
        }

        _adminMenu.Toggle(player);
        player.SendConsole("[HGR] Menü açıldı.");
    }

    private void OnHgrCommand(ICommandContext ctx)
    {
        if (_permissions == null || _adminMenu == null) return;

        if (ctx.Sender is not { } player)
        {
            ctx.Reply($"[HGR] Hook={_config.Permissions.HookPermission}, Grab={_config.Permissions.GrabPermission}, Rope={_config.Permissions.RopePermission}");
            return;
        }

        if (_permissions.CanManage(player))
        {
            _adminMenu.Toggle(player);
            return;
        }

        bool h = _permissions.CanUseHook(player);
        bool g = _permissions.CanUseGrab(player);
        bool r = _permissions.CanUseRope(player);

        PlayerUtil.SendChat(player, _config.General.ChatPrefix,
            $"Yetkileriniz -> Hook: {(h ? "[lime]AÇIK" : "[red]KAPALI")}[default] | Grab: {(g ? "[lime]AÇIK" : "[red]KAPALI")}[default] | Rope: {(r ? "[lime]AÇIK" : "[red]KAPALI")}[default]");
        PlayerUtil.SendChat(player, _config.General.ChatPrefix,
            "Konsol tuş ataması: [yellow]bind <tuş> +hook[default] | [yellow]bind <tuş> +grab[default] | [yellow]bind <tuş> +rope");
    }

    private void OnReloadCommand(ICommandContext ctx)
    {
        if (_permissions == null || _configLoader == null || _engine == null || _adminMenu == null)
            return;

        if (ctx.Sender is { } player && !_permissions.CanManage(player))
        {
            ReplyToContext(ctx, "[red]Bu komutu kullanmak için yetkiniz yok.");
            return;
        }

        try
        {
            _config = _configLoader.Load();
            _permissions.UpdateConfig(_config);
            _engine.UpdateConfig(_config);
            _adminMenu.UpdateConfig(_config);
            SyncConfigToConVars();
            ReplyToContext(ctx, "[lime]hgr_config.toml başarıyla yeniden yüklendi.");
        }
        catch (Exception ex)
        {
            Core.Logger.LogError(ex, "[HookGrabRope] Config yeniden yüklenemedi.");
            ReplyToContext(ctx, $"[red]Config yüklenirken hata oluştu: {ex.Message}");
        }
    }

    private void ReplyToContext(ICommandContext ctx, string message)
    {
        if (ctx.Sender is { } player)
            PlayerUtil.SendChat(player, _config.General.ChatPrefix, message);
        else
            ctx.Reply($"[HGR] {message}");
    }

    // ==============================================================================
    // OYUN OLAYLARI & ENGINE HOOKLARI
    // ==============================================================================

    private void RegisterEvents()
    {
        Core.Event.OnTick += OnTick;
        Core.Event.OnClientPutInServer += OnClientPutInServer;
        Core.Event.OnClientDisconnected += OnClientDisconnected;
        Core.Event.OnMapLoad += OnMapLoad;
        Core.Event.OnMapUnload += OnMapUnload;

        _gameEventGuids.Add(Core.GameEvent.HookPost<EventPlayerDeath>(OnPlayerDeath));
        _gameEventGuids.Add(Core.GameEvent.HookPost<EventPlayerSpawn>(OnPlayerSpawn));
        _gameEventGuids.Add(Core.GameEvent.HookPost<EventPlayerTeam>(OnPlayerTeam));
        _gameEventGuids.Add(Core.GameEvent.HookPost<EventRoundStart>(OnRoundStart));
        _gameEventGuids.Add(Core.GameEvent.HookPost<EventRoundEnd>(OnRoundEnd));
    }

    private void UnregisterEvents()
    {
        Core.Event.OnTick -= OnTick;
        Core.Event.OnClientPutInServer -= OnClientPutInServer;
        Core.Event.OnClientDisconnected -= OnClientDisconnected;
        Core.Event.OnMapLoad -= OnMapLoad;
        Core.Event.OnMapUnload -= OnMapUnload;

        foreach (var g in _gameEventGuids)
        {
            try { Core.GameEvent.Unhook(g); } catch { }
        }
        _gameEventGuids.Clear();
    }

    private void OnTick()
    {
        if (++_cvarSyncTickCounter >= 32)
        {
            _cvarSyncTickCounter = 0;
            PollConVarChanges();
        }

        _engine?.OnTick();
        _adminMenu?.OnTick();
    }

    /// <summary>
    /// amx_hook.sma client_putinserver(id) -> has_hook[id] = false
    /// </summary>
    private void OnClientPutInServer(IOnClientPutInServerEvent e)
    {
        _engine?.CleanupPlayerSlot(e.PlayerId);
        _states[e.PlayerId] = new PlayerHgrState();
    }

    private void OnClientDisconnected(IOnClientDisconnectedEvent e)
    {
        _adminMenu?.OnPlayerDisconnected(e.PlayerId);
        _engine?.CleanupPlayerSlot(e.PlayerId);
    }

    private void OnMapLoad(IOnMapLoadEvent e)
    {
        _adminMenu?.CloseAll();
        _engine?.ResetAllActiveAbilities();
    }

    private void OnMapUnload(IOnMapUnloadEvent e)
    {
        _adminMenu?.CloseAll();
        _engine?.ResetAllActiveAbilities();
    }

    private HookResult OnPlayerDeath(EventPlayerDeath e)
    {
        var victim = e.UserIdPlayer;
        if (PlayerUtil.IsUsable(victim))
        {
            _engine?.StopAllForPlayer(victim);
        }
        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn e)
    {
        var player = e.UserIdPlayer;
        if (PlayerUtil.IsUsable(player))
        {
            _engine?.StopAllForPlayer(player);
        }
        return HookResult.Continue;
    }

    private HookResult OnPlayerTeam(EventPlayerTeam e)
    {
        var player = e.UserIdPlayer;
        if (PlayerUtil.IsUsable(player))
        {
            _engine?.StopAllForPlayer(player);
        }
        return HookResult.Continue;
    }

    private HookResult OnRoundStart(EventRoundStart e)
    {
        if (_config.General.ResetOnRoundStart)
        {
            _engine?.ResetAllActiveAbilities();
        }
        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd e)
    {
        _engine?.ResetAllActiveAbilities();
        return HookResult.Continue;
    }
}

