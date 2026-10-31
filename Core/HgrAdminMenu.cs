using System.Net;
using System.Text;
using HookGrabRope.Config;
using HookGrabRope.Models;
using HookGrabRope.Utils;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HookGrabRope.Core;

/// <summary>
/// amx_hook_menu / !hgrmenu / !hookmenu komutuyla acilan CenterHTML tabanli gorsel yonetim menusu.
/// W/S: Yukari/Asagi | A/D: Deger degistir | E: Sec | R: Geri/Kapat
/// </summary>
public sealed class HgrAdminMenu
{
    private const int RowsPerPage = 6;
    private const int HtmlDurationMs = 2000;
    private const float ResendSeconds = 0.25f;

    private readonly ISwiftlyCore _core;
    private readonly ConfigLoader _loader;
    private readonly PermissionService _permissions;
    private readonly HgrEngine _engine;
    private readonly Action _syncConVarsCallback;
    private PluginConfig _config;

    private readonly Dictionary<int, Session> _sessions = new();
    private readonly Dictionary<int, (string Html, float Time)> _lastHtml = new();

    private enum RowStyle { Normal, Accent, On, Off, Danger, Muted }

    private sealed record Row(
        string Label,
        string? Value = null,
        bool Arrows = false,
        RowStyle Style = RowStyle.Normal,
        Action<IPlayer>? OnSelect = null,
        Action<IPlayer, int>? OnAdjust = null);

    private sealed class Page
    {
        public string Title { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
        public List<Row> Rows { get; init; } = new();
    }

    private delegate Page PageBuilder(IPlayer admin);

    private sealed class Session(PageBuilder root)
    {
        private readonly Stack<(PageBuilder Builder, int Cursor)> _history = new();
        public PageBuilder Current { get; private set; } = root;
        public int Cursor { get; set; }
        public GameButtonFlags LastButtons { get; set; }
        public float LastKeyTime { get; set; }

        public void Push(PageBuilder next)
        {
            _history.Push((Current, Cursor));
            Current = next;
            Cursor = 0;
        }

        public bool Back()
        {
            if (_history.Count == 0) return false;
            var (b, c) = _history.Pop();
            Current = b;
            Cursor = c;
            return true;
        }
    }

    public HgrAdminMenu(
        ISwiftlyCore core,
        ConfigLoader loader,
        PluginConfig config,
        PermissionService permissions,
        HgrEngine engine,
        Action syncConVarsCallback)
    {
        _core = core;
        _loader = loader;
        _config = config;
        _permissions = permissions;
        _engine = engine;
        _syncConVarsCallback = syncConVarsCallback;
    }

    public void UpdateConfig(PluginConfig config) => _config = config;

    public bool IsMenuOpen(int slot) => _sessions.ContainsKey(slot);

    public void Toggle(IPlayer admin)
    {
        if (IsMenuOpen(admin.Slot)) Close(admin);
        else Open(admin);
    }

    public void Open(IPlayer admin)
    {
        _sessions[admin.Slot] = new Session(BuildMainPage)
        {
            LastButtons = admin.PressedButtons,
            LastKeyTime = _core.Engine.GlobalVars.CurrentTime
        };
        SetFrozen(admin, true);
        PlayerUtil.SendChat(admin, _config.General.ChatPrefix,
            "HGR Yönetim Menüsü açıldı! [lime]W/S[default]: Gezin | [lime]A/D[default]: Değiştir | [lime]E[default]: Seç | [lime]R[default]: Geri/Kapat");
    }

    public void Close(IPlayer admin)
    {
        if (_sessions.Remove(admin.Slot))
        {
            SetFrozen(admin, false);
            if (_lastHtml.Remove(admin.Slot))
            {
                try { admin.SendCenterHTML(" ", 1); } catch { }
            }
        }
    }

    public void OnPlayerDisconnected(int slot)
    {
        _sessions.Remove(slot);
        _lastHtml.Remove(slot);
    }

    public void CloseAll()
    {
        foreach (var p in _core.PlayerManager.GetAllPlayers().Where(PlayerUtil.IsUsable))
        {
            if (_sessions.ContainsKey(p!.Slot)) Close(p);
        }
        _sessions.Clear();
        _lastHtml.Clear();
    }

    public void OnTick()
    {
        if (_sessions.Count == 0) return;

        foreach (var kv in _sessions.ToList())
        {
            var admin = _core.PlayerManager.GetPlayer(kv.Key);
            if (!PlayerUtil.IsUsable(admin))
            {
                OnPlayerDisconnected(kv.Key);
                continue;
            }

            PollButtons(admin!, kv.Value);
            Render(admin!, kv.Value);
        }
    }

    private void PollButtons(IPlayer admin, Session session)
    {
        if (_config.General.FreezeWhileMenuOpen)
            SetFrozen(admin, true);

        var buttons = admin.PressedButtons;
        var rising = buttons & ~session.LastButtons;
        session.LastButtons = buttons;
        if (rising == 0) return;

        if ((rising & GameButtonFlags.W) != 0) HandleInput(admin, session, 0, -1, false, false);
        else if ((rising & GameButtonFlags.S) != 0) HandleInput(admin, session, 0, +1, false, false);
        else if ((rising & GameButtonFlags.A) != 0) HandleInput(admin, session, -1, 0, false, false);
        else if ((rising & GameButtonFlags.D) != 0) HandleInput(admin, session, +1, 0, false, false);
        else if ((rising & GameButtonFlags.E) != 0) HandleInput(admin, session, 0, 0, true, false);
        else if ((rising & GameButtonFlags.R) != 0) HandleInput(admin, session, 0, 0, false, true);
    }

    private void HandleInput(IPlayer admin, Session session, int adjustDir, int moveDir, bool select, bool back)
    {
        var now = _core.Engine.GlobalVars.CurrentTime;
        if (now - session.LastKeyTime < 0.12f) return;
        session.LastKeyTime = now;

        var page = session.Current(admin);
        int count = page.Rows.Count;
        if (count == 0) return;
        session.Cursor = Math.Clamp(session.Cursor, 0, count - 1);

        if (moveDir != 0)
        {
            session.Cursor = (session.Cursor + moveDir + count) % count;
        }
        else if (adjustDir != 0)
        {
            page.Rows[session.Cursor].OnAdjust?.Invoke(admin, adjustDir);
            _lastHtml.Remove(admin.Slot);
        }
        else if (select)
        {
            var r = page.Rows[session.Cursor];
            if (r.OnSelect != null) r.OnSelect(admin);
            else r.OnAdjust?.Invoke(admin, +1);
            _lastHtml.Remove(admin.Slot);
        }
        else if (back)
        {
            if (!session.Back()) Close(admin);
            else _lastHtml.Remove(admin.Slot);
        }
    }

    private void Render(IPlayer admin, Session session)
    {
        var page = session.Current(admin);
        if (page.Rows.Count > 0)
            session.Cursor = Math.Clamp(session.Cursor, 0, page.Rows.Count - 1);

        int pageIdx = session.Cursor / RowsPerPage;
        int pageCount = Math.Max(1, (page.Rows.Count + RowsPerPage - 1) / RowsPerPage);
        var slice = page.Rows.Skip(pageIdx * RowsPerPage).Take(RowsPerPage).ToList();
        int selectedInSlice = session.Cursor - pageIdx * RowsPerPage;

        var sb = new StringBuilder();
        sb.Append($"<font class='fontSize-m' color='#38BDF8'><b>✦ {E(page.Title)} ✦</b></font>");
        if (pageCount > 1)
            sb.Append($" <font class='fontSize-s' color='#94A3B8'>[{pageIdx + 1}/{pageCount}]</font>");
        sb.Append("<br>");

        if (!string.IsNullOrEmpty(page.Subtitle))
            sb.Append($"<font class='fontSize-xs' color='#CBD5E1'>{E(page.Subtitle)}</font><br>");

        for (int i = 0; i < slice.Count; i++)
        {
            var row = slice[i];
            bool sel = i == selectedInSlice;
            var labelColor = sel ? "#FACC15" : row.Style switch
            {
                RowStyle.Danger => "#F87171",
                RowStyle.Accent => "#38BDF8",
                RowStyle.Muted => "#64748B",
                _ => "#F8FAFC"
            };

            sb.Append("<font class='fontSize-sm'>");
            sb.Append(sel ? "<font color='#FACC15'><b>➤ </b></font>" : "<font color='#475569'>• </font>");
            sb.Append(sel
                ? $"<font color='{labelColor}'><b>{E(row.Label)}</b></font>"
                : $"<font color='{labelColor}'>{E(row.Label)}</font>");

            if (!string.IsNullOrEmpty(row.Value))
            {
                var valColor = row.Style switch
                {
                    RowStyle.On => "#4ADE80",
                    RowStyle.Off => "#F87171",
                    _ => "#E2E8F0"
                };
                var v = row.Arrows ? $"◀ {E(row.Value)} ▶" : $"[{E(row.Value)}]";
                sb.Append($" <font color='{valColor}'><b>{v}</b></font>");
            }
            sb.Append("</font><br>");
        }

        sb.Append("<font class='fontSize-xs' color='#94A3B8'>[W/S] Gezin • [A/D] Değiştir • [E] Seç • [R] Geri/Kapat</font>");

        var html = sb.ToString();
        var now = _core.Engine.GlobalVars.CurrentTime;
        if (_lastHtml.TryGetValue(admin.Slot, out var last) && last.Html == html && now - last.Time < ResendSeconds)
            return;

        _lastHtml[admin.Slot] = (html, now);
        admin.SendCenterHTML(html, HtmlDurationMs);
    }

    private static string E(string s) => WebUtility.HtmlEncode(s);

    private void SetFrozen(IPlayer player, bool freeze)
    {
        if (!_config.General.FreezeWhileMenuOpen && freeze) return;
        try
        {
            var pawn = PlayerUtil.GetPawn(player);
            if (pawn == null) return;
            var target = freeze ? MoveType_t.MOVETYPE_NONE : MoveType_t.MOVETYPE_WALK;
            if (pawn.MoveType != target)
            {
                pawn.MoveType = target;
                pawn.ActualMoveType = target;
                pawn.MoveTypeUpdated();
            }
            if (freeze)
            {
                PlayerUtil.SetVelocity(player, SwiftlyS2.Shared.Natives.Vector.Zero);
            }
        }
        catch { }
    }

    private void SaveAndSync()
    {
        try { _loader.Save(_config); }
        catch { }
        _permissions.UpdateConfig(_config);
        _engine.UpdateConfig(_config);
        _syncConVarsCallback();
    }

    // ============================== MENU SAYFALARI ==============================

    private Page BuildMainPage(IPlayer admin)
    {
        int onlineCount = _core.PlayerManager.GetAllPlayers().Count(PlayerUtil.IsUsable);
        return new Page
        {
            Title = "Hook • Grab • Rope Yönetim",
            Subtitle = "Oyuncu İzinleri ve Fizik Ayarları",
            Rows =
            {
                new Row("1. Herkese Hook İzni", _config.Hook.EnabledForAll ? "AÇIK" : "KAPALI",
                    Arrows: true,
                    Style: _config.Hook.EnabledForAll ? RowStyle.On : RowStyle.Off,
                    OnAdjust: (_, _) => ToggleGlobalHook()),

                new Row("2. Herkese Grab İzni", _config.Grab.EnabledForAll ? "AÇIK" : "KAPALI",
                    Arrows: true,
                    Style: _config.Grab.EnabledForAll ? RowStyle.On : RowStyle.Off,
                    OnAdjust: (_, _) => ToggleGlobalGrab()),

                new Row("3. Herkese Rope İzni", _config.Rope.EnabledForAll ? "AÇIK" : "KAPALI",
                    Arrows: true,
                    Style: _config.Rope.EnabledForAll ? RowStyle.On : RowStyle.Off,
                    OnAdjust: (_, _) => ToggleGlobalRope()),

                new Row("👤 Oyunculara Hook/Grab/Rope Ver-Al", $"{onlineCount} oyuncu",
                    Style: RowStyle.Accent,
                    OnSelect: p => _sessions[p.Slot].Push(BuildOnlinePlayersPage)),

                new Row("⚡ Hız ve Fizik Ayarları",
                    Style: RowStyle.Accent,
                    OnSelect: p => _sessions[p.Slot].Push(BuildPhysicsSettingsPage)),

                new Row("❌ Menüyü Kapat", Style: RowStyle.Danger, OnSelect: Close)
            }
        };
    }

    private Page BuildOnlinePlayersPage(IPlayer admin)
    {
        var rows = new List<Row>();
        var players = _core.PlayerManager.GetAllPlayers().Where(PlayerUtil.IsUsable).OrderBy(p => p!.Slot).ToList();

        foreach (var target in players)
        {
            int targetSlot = target!.Slot;
            string name = PlayerUtil.GetName(target);
            bool hasH = _permissions.CanUseHook(target);
            bool hasG = _permissions.CanUseGrab(target);
            bool hasR = _permissions.CanUseRope(target);
            string badge = $"{(hasH ? "H" : "-")}|{(hasG ? "G" : "-")}|{(hasR ? "R" : "-")}";

            rows.Add(new Row(
                $"{name} (#{targetSlot})",
                badge,
                Style: (hasH || hasG || hasR) ? RowStyle.On : RowStyle.Normal,
                OnSelect: p => _sessions[p.Slot].Push(a => BuildPlayerAbilityPage(a, targetSlot))));
        }

        rows.Add(new Row("⬅ Ana Menüye Dön", Style: RowStyle.Muted, OnSelect: p => _sessions[p.Slot].Back()));

        return new Page
        {
            Title = "Oyuncu Yetki Yönetimi",
            Subtitle = "Düzenlemek istediğiniz oyuncuyu seçin",
            Rows = rows
        };
    }

    private Page BuildPlayerAbilityPage(IPlayer admin, int targetSlot)
    {
        var target = _core.PlayerManager.GetPlayer(targetSlot);
        if (!PlayerUtil.IsUsable(target))
        {
            return new Page
            {
                Title = "Oyuncu Ayrıldı",
                Subtitle = "Seçilen oyuncu artık sunucuda değil",
                Rows = { new Row("⬅ Geri Dön", Style: RowStyle.Muted, OnSelect: p => _sessions[p.Slot].Back()) }
            };
        }

        string name = PlayerUtil.GetName(target);
        bool grantedHook = _permissions.HasGrantedAbility(target!, HgrAbility.Hook);
        bool grantedGrab = _permissions.HasGrantedAbility(target!, HgrAbility.Grab);
        bool grantedRope = _permissions.HasGrantedAbility(target!, HgrAbility.Rope);

        return new Page
        {
            Title = $"Oyuncu: {name}",
            Subtitle = "A/D veya E ile yetki verin / geri alın",
            Rows =
            {
                new Row("Hook Yetkisi (hook1 / hook0)", grantedHook ? "VERİLDİ" : "YOK",
                    Arrows: true,
                    Style: grantedHook ? RowStyle.On : RowStyle.Off,
                    OnAdjust: (a, _) => TogglePlayerAbility(a, target!, HgrAbility.Hook, "Hook", "hook1 / hook0")),

                new Row("Grab Yetkisi (sw_grab1 / sw_grab0)", grantedGrab ? "VERİLDİ" : "YOK",
                    Arrows: true,
                    Style: grantedGrab ? RowStyle.On : RowStyle.Off,
                    OnAdjust: (a, _) => TogglePlayerAbility(a, target!, HgrAbility.Grab, "Grab", "sw_grab1 / sw_grab0")),

                new Row("Rope Yetkisi (sw_rope1 / sw_rope0)", grantedRope ? "VERİLDİ" : "YOK",
                    Arrows: true,
                    Style: grantedRope ? RowStyle.On : RowStyle.Off,
                    OnAdjust: (a, _) => TogglePlayerAbility(a, target!, HgrAbility.Rope, "Rope", "sw_rope1 / sw_rope0")),

                new Row("✨ Tümünü Ver (Hook + Grab + Rope)", Style: RowStyle.Accent,
                    OnSelect: a =>
                    {
                        if (target!.Slot != a.Slot && !_permissions.CanGrabTarget(a, target, out var aImm, out var tImm))
                        {
                            PlayerUtil.SendChat(a, _config.General.ChatPrefix,
                                $"[red]{name}[default] dokunulmazlığı senden yüksek olduğu için yetki verilemez. (Hedef: [gold]{tImm}[default], Sen: [green]{aImm}[default])");
                            return;
                        }
                        _permissions.SetGrantedAbility(target, HgrAbility.All, true, saveToConfig: true);
                        SaveAndSync();
                        PlayerUtil.SendChat(a, _config.General.ChatPrefix, $"[lime]{name}[default] oyuncusuna tüm HGR yetkileri verildi.");
                        PlayerUtil.SendChat(target, _config.General.ChatPrefix, "[lime]Bir admin size Hook, Grab ve Rope yetkisi verdi! ([yellow]hook1/hook0 | sw_grab1/sw_grab0 | sw_rope1/sw_rope0[lime])");
                    }),

                new Row("🚫 Tümünü Geri Al", Style: RowStyle.Danger,
                    OnSelect: a =>
                    {
                        if (target!.Slot != a.Slot && !_permissions.CanGrabTarget(a, target, out var aImm, out var tImm))
                        {
                            PlayerUtil.SendChat(a, _config.General.ChatPrefix,
                                $"[red]{name}[default] dokunulmazlığı senden yüksek olduğu için yetki geri alınamaz. (Hedef: [gold]{tImm}[default], Sen: [green]{aImm}[default])");
                            return;
                        }
                        _permissions.SetGrantedAbility(target, HgrAbility.All, false, saveToConfig: true);
                        _engine.StopAllForPlayer(target);
                        SaveAndSync();
                        PlayerUtil.SendChat(a, _config.General.ChatPrefix, $"[red]{name}[default] oyuncusunun tüm HGR yetkileri alındı.");
                        PlayerUtil.SendChat(target, _config.General.ChatPrefix, "[red]Bir admin Hook, Grab ve Rope yetkilerinizi geri aldı.");
                    }),

                new Row("⬅ Oyuncu Listesine Dön", Style: RowStyle.Muted, OnSelect: p => _sessions[p.Slot].Back())
            }
        };
    }

    private Page BuildPhysicsSettingsPage(IPlayer admin)
    {
        return new Page
        {
            Title = "Hız ve Fizik Ayarları",
            Subtitle = "Değişiklikler anında kaydedilir",
            Rows =
            {
                new Row("Hook Hızı (sw_hook_speed)", $"{_config.Hook.Speed:0.0}", Arrows: true,
                    Style: RowStyle.Accent,
                    OnAdjust: (_, dir) =>
                    {
                        _config.Hook.Speed = Math.Clamp(_config.Hook.Speed + dir * 0.5f, 0.5f, 25.0f);
                        SaveAndSync();
                    }),

                new Row("Grab Hızı (sw_grab_speed)", $"{_config.Grab.Speed:0.0}", Arrows: true,
                    Style: RowStyle.Accent,
                    OnAdjust: (_, dir) =>
                    {
                        _config.Grab.Speed = Math.Clamp(_config.Grab.Speed + dir * 0.5f, 0.5f, 25.0f);
                        SaveAndSync();
                    }),

                new Row("Rope Hızı (sw_rope_speed)", $"{_config.Rope.Speed:0.0}", Arrows: true,
                    Style: RowStyle.Accent,
                    OnAdjust: (_, dir) =>
                    {
                        _config.Rope.Speed = Math.Clamp(_config.Rope.Speed + dir * 0.5f, 0.5f, 25.0f);
                        SaveAndSync();
                    }),

                new Row("Grab Fırlatma Gücü", $"{_config.Grab.ThrowForce:0}", Arrows: true,
                    Style: RowStyle.Accent,
                    OnAdjust: (_, dir) =>
                    {
                        _config.Grab.ThrowForce = Math.Clamp(_config.Grab.ThrowForce + dir * 100.0f, 200.0f, 3000.0f);
                        SaveAndSync();
                    }),

                new Row("Admin Dokunulmazlığı", _config.Permissions.AdminImmunity ? "AÇIK" : "KAPALI", Arrows: true,
                    Style: _config.Permissions.AdminImmunity ? RowStyle.On : RowStyle.Off,
                    OnAdjust: (_, _) =>
                    {
                        _config.Permissions.AdminImmunity = !_config.Permissions.AdminImmunity;
                        SaveAndSync();
                    }),

                new Row("Ses Efektleri", _config.General.EnableSounds ? "AÇIK" : "KAPALI", Arrows: true,
                    Style: _config.General.EnableSounds ? RowStyle.On : RowStyle.Off,
                    OnAdjust: (_, _) =>
                    {
                        _config.General.EnableSounds = !_config.General.EnableSounds;
                        SaveAndSync();
                    }),

                new Row("⬅ Ana Menüye Dön", Style: RowStyle.Muted, OnSelect: p => _sessions[p.Slot].Back())
            }
        };
    }

    private void ToggleGlobalHook()
    {
        _config.Hook.EnabledForAll = !_config.Hook.EnabledForAll;
        SaveAndSync();

        if (_config.Hook.EnabledForAll)
        {
            PlayerUtil.BroadcastChat(_core, _config.General.ChatPrefix,
                "[lime]Admin tüm oyuncular için Hook kullanımını açtı! Konsola: [yellow]alias +hook \"sw_hook1\"; alias -hook \"sw_hook0\"; bind <tuş> +hook");
        }
        else
        {
            PlayerUtil.BroadcastChat(_core, _config.General.ChatPrefix,
                "[red]Admin yetkisiz oyuncular için Hook kullanımını kapattı.");
        }
    }

    private void ToggleGlobalGrab()
    {
        _config.Grab.EnabledForAll = !_config.Grab.EnabledForAll;
        SaveAndSync();

        if (_config.Grab.EnabledForAll)
        {
            PlayerUtil.BroadcastChat(_core, _config.General.ChatPrefix,
                "[lime]Admin tüm oyuncular için Grab kullanımını açtı! Konsola: [yellow]alias +grab \"sw_grab1\"; alias -grab \"sw_grab0\"; bind <tuş> +grab");
        }
        else
        {
            PlayerUtil.BroadcastChat(_core, _config.General.ChatPrefix,
                "[red]Admin yetkisiz oyuncular için Grab kullanımını kapattı.");
        }
    }

    private void ToggleGlobalRope()
    {
        _config.Rope.EnabledForAll = !_config.Rope.EnabledForAll;
        SaveAndSync();

        if (_config.Rope.EnabledForAll)
        {
            PlayerUtil.BroadcastChat(_core, _config.General.ChatPrefix,
                "[lime]Admin tüm oyuncular için Rope kullanımını açtı! Konsola: [yellow]alias +rope \"sw_rope1\"; alias -rope \"sw_rope0\"; bind <tuş> +rope");
        }
        else
        {
            PlayerUtil.BroadcastChat(_core, _config.General.ChatPrefix,
                "[red]Admin yetkisiz oyuncular için Rope kullanımını kapattı.");
        }
    }

    private void TogglePlayerAbility(IPlayer admin, IPlayer target, HgrAbility ability, string label, string bindCmd)
    {
        if (target.Slot != admin.Slot && !_permissions.CanGrabTarget(admin, target, out var adminImm, out var targetImm))
        {
            PlayerUtil.SendChat(admin, _config.General.ChatPrefix,
                $"[red]{PlayerUtil.GetName(target)}[default] dokunulmazlığı senden yüksek olduğu için yetki değişikliği yapılamaz. (Hedef: [gold]{targetImm}[default], Sen: [green]{adminImm}[default])");
            return;
        }

        bool currentlyHas = _permissions.HasGrantedAbility(target, ability);
        bool nextState = !currentlyHas;
        _permissions.SetGrantedAbility(target, ability, nextState, saveToConfig: true);

        if (!nextState)
        {
            if (ability == HgrAbility.Hook) _engine.StopHook(target);
            if (ability == HgrAbility.Grab) _engine.StopGrab(target);
            if (ability == HgrAbility.Rope) _engine.StopRope(target);
        }

        SaveAndSync();

        if (nextState)
        {
            PlayerUtil.SendChat(admin, _config.General.ChatPrefix,
                $"[lime]{PlayerUtil.GetName(target)}[default] oyuncusuna [gold]{label}[default] yetkisi verildi.");
            PlayerUtil.SendChat(target, _config.General.ChatPrefix,
                $"[lime]Bir admin size {label} yetkisi verdi! Konsol komutları: [yellow]{bindCmd}");
        }
        else
        {
            PlayerUtil.SendChat(admin, _config.General.ChatPrefix,
                $"[red]{PlayerUtil.GetName(target)}[default] oyuncusunun [gold]{label}[default] yetkisi geri alındı.");
            PlayerUtil.SendChat(target, _config.General.ChatPrefix,
                $"[red]Bir admin {label} yetkinizi geri aldı.");
        }
    }
}

