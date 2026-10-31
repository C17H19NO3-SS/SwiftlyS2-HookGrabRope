using System.Net;
using HookGrabRope.Config;
using HookGrabRope.Models;
using HookGrabRope.Utils;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Sounds;
using SwiftlyS2.Shared.Trace;

namespace HookGrabRope.Core;

/// <summary>
/// Hook, Grab ve Rope mekaniklerinin ray-trace, fizik (velocity/gravity/pendulum),
/// CBeam lazer ve ses efektlerini yoneten cekirdek motor.
/// </summary>
public sealed class HgrEngine
{
    private readonly ISwiftlyCore _core;
    private PluginConfig _config;
    private readonly PermissionService _permissions;
    private readonly Dictionary<int, PlayerHgrState> _states;

    private Color _hookColor = new(0, 120, 255, 255);
    private Color _grabColor = new(255, 50, 50, 255);
    private Color _ropeColor = new(50, 255, 80, 255);

    private static readonly HashSet<string> NonGrabbableDesigners = new(StringComparer.OrdinalIgnoreCase)
    {
        "worldent",
        "cs_player_controller",
        "player",
        "beam",
        "env_beam",
        "cs_gamerules",
        "cs_team_manager",
        "cs_player_manager",
        "vote_controller",
        "func_brush",
        "func_wall",
        "func_buyzone",
        "func_bomb_target",
        "func_hostage_rescue",
        "trigger_multiple",
        "trigger_once",
        "trigger_hurt",
        "trigger_push",
        "trigger_teleport",
        "sky_camera",
        "light_environment",
        "info_player_terrorist",
        "info_player_counterterrorist"
    };

    public HgrEngine(
        ISwiftlyCore core,
        PluginConfig config,
        PermissionService permissions,
        Dictionary<int, PlayerHgrState> states)
    {
        _core = core;
        _config = config;
        _permissions = permissions;
        _states = states;
        RefreshColors();
    }

    public void UpdateConfig(PluginConfig config)
    {
        _config = config;
        RefreshColors();
    }

    private void RefreshColors()
    {
        _hookColor = BeamUtil.ParseHexColor(_config.Hook.BeamColor, new Color(0, 120, 255, 255));
        _grabColor = BeamUtil.ParseHexColor(_config.Grab.BeamColor, new Color(255, 50, 50, 255));
        _ropeColor = BeamUtil.ParseHexColor(_config.Rope.BeamColor, new Color(50, 255, 80, 255));
    }

    public PlayerHgrState GetOrCreateState(int slot)
    {
        if (!_states.TryGetValue(slot, out var state))
        {
            state = new PlayerHgrState();
            _states[slot] = state;
        }
        return state;
    }

    // ==============================================================================
    // 1. HOOK (amx_hook.sma Karsiligi + CS2 Fizigi)
    // ==============================================================================

    public void ToggleHook(IPlayer player)
    {
        var state = GetOrCreateState(player.Slot);
        if (state.IsHooking)
            StopHook(player);
        else
            StartHook(player);
    }

    public void StartHook(IPlayer player)
    {
        if (!PlayerUtil.IsUsable(player)) return;

        if (!_permissions.CanUseHook(player))
        {
            PlayerUtil.SendChat(player, _config.General.ChatPrefix,
                $"[red]Hook kullanmak için yetkiniz yok ([yellow]{_config.Permissions.HookPermission}[red]).");
            return;
        }

        if (!PlayerUtil.IsAlive(player))
        {
            PlayerUtil.SendChat(player, _config.General.ChatPrefix, "[red]Ölüyken Hook kullanamazsınız.");
            return;
        }

        var state = GetOrCreateState(player.Slot);
        if (state.IsHooking) return;

        // Rope aciksa kapat (Hook ve Rope ayni anda oyuncunun kendi hareketini kontrol ettigi icin cakismasin)
        if (state.IsRoping)
            StopRope(player);

        if (!TraceLookPoint(player, _config.Hook.MaxDistance, out var hitPos))
            return;

        SaveAndSetZeroGravity(player, state);

        state.IsHooking = true;
        state.HookTarget = hitPos;
        state.HasHookTarget = true;

        EmitAbilitySound(player, _config.Hook.SoundEventName);
        ProcessHookTick(player, state);
    }

    public void StopHook(IPlayer? player)
    {
        if (player == null) return;
        if (!_states.TryGetValue(player.Slot, out var state) || !state.IsHooking)
            return;

        state.IsHooking = false;
        state.HasHookTarget = false;

        var beam = state.HookBeam;
        BeamUtil.DestroyBeam(ref beam);
        state.HookBeam = null;

        RestoreGravityIfIdle(player, state);
    }

    private void ProcessHookTick(IPlayer player, PlayerHgrState state)
    {
        if (!PlayerUtil.IsAlive(player) || !_permissions.CanUseHook(player))
        {
            StopHook(player);
            return;
        }

        if (!PlayerUtil.TryGetOrigin(player, out var origin))
        {
            StopHook(player);
            return;
        }

        if (!state.HasHookTarget)
        {
            if (!TraceLookPoint(player, _config.Hook.MaxDistance, out var hitPos))
            {
                StopHook(player);
                return;
            }
            state.HookTarget = hitPos;
            state.HasHookTarget = true;
        }

        // Yerçekimini 0'da tut (amx_hook.sma: set_user_gravity(id, 0.0))
        PlayerUtil.SetGravity(player, 0.0f);

        // Mavi lazer oluştur veya güncelle (amx_hook.sma: TE_BEAMENTPOINT)
        var beamStart = PlayerUtil.GetBeamOrigin(player);
        state.HookBeam = BeamUtil.CreateOrUpdateBeam(
            _core, state.HookBeam, beamStart, state.HookTarget, _hookColor, _config.Hook.BeamWidth);

        // Hız hesaplaması (amx_hook.sma satır 271-286: (hook_speed * 120.0) / floatsqroot(y))
        Vector delta = state.HookTarget - origin;
        float distance = delta.Length();
        float desiredSpeed = _config.Hook.Speed * 120.0f;

        Vector velocity;
        if (distance <= 1.0f)
        {
            velocity = Vector.Zero;
        }
        else
        {
            Vector dir = delta / distance;
            float stopDist = Math.Max(8.0f, _config.Hook.ArrivalStopDistance);
            float scale = distance < stopDist ? (distance / stopDist) : 1.0f;
            velocity = dir * (desiredSpeed * scale);
        }

        if (_config.Hook.AllowAirControl)
        {
            var buttons = player.PressedButtons;
            if ((buttons & GameButtonFlags.Space) != 0)
                velocity.Z += 140.0f;
            if ((buttons & GameButtonFlags.Ctrl) != 0)
                velocity *= 0.55f;
        }

        PlayerUtil.SetVelocity(player, velocity);
    }

    // ==============================================================================
    // 2. GRAB (admin.grab)
    // ==============================================================================

    public void ToggleGrab(IPlayer player)
    {
        var state = GetOrCreateState(player.Slot);
        if (state.IsGrabbing)
            StopGrab(player);
        else
            StartGrab(player);
    }

    public void StartGrab(IPlayer player)
    {
        if (!PlayerUtil.IsUsable(player)) return;

        if (!_permissions.CanUseGrab(player))
        {
            PlayerUtil.SendChat(player, _config.General.ChatPrefix,
                $"[red]Grab kullanmak için yetkiniz yok ([yellow]{_config.Permissions.GrabPermission}[red]).");
            return;
        }

        if (!PlayerUtil.IsAlive(player))
        {
            PlayerUtil.SendChat(player, _config.General.ChatPrefix, "[red]Ölüyken Grab kullanamazsınız.");
            return;
        }

        var state = GetOrCreateState(player.Slot);
        if (state.IsGrabbing) return;

        state.IsGrabbing = true;
        state.IsSearchingGrabTarget = true;
        state.GrabTargetPlayerSlot = -1;
        state.GrabTargetEntityIndex = 0;

        TryAcquireGrabTarget(player, state);
    }

    public void StopGrab(IPlayer? player)
    {
        if (player == null) return;
        if (!_states.TryGetValue(player.Slot, out var state) || !state.IsGrabbing)
            return;

        ReleaseGrabTarget(state);

        state.IsGrabbing = false;
        state.IsSearchingGrabTarget = false;
        state.IsPushing = false;
        state.IsPulling = false;

        var beam = state.GrabBeam;
        BeamUtil.DestroyBeam(ref beam);
        state.GrabBeam = null;

        if (_config.Grab.ShowCenterHud && PlayerUtil.IsUsable(player) && !player.IsFakeClient)
        {
            try { player.SendCenterHTML(" ", 1); } catch { }
        }
    }

    public void SetPushState(IPlayer player, bool active)
    {
        if (!_states.TryGetValue(player.Slot, out var state)) return;
        state.IsPushing = active;
    }

    public void SetPullState(IPlayer player, bool active)
    {
        if (!_states.TryGetValue(player.Slot, out var state)) return;
        state.IsPulling = active;
    }

    private void ReleaseGrabTarget(PlayerHgrState state)
    {
        if (state.GrabTargetPlayerSlot >= 0)
        {
            var targetPlayer = _core.PlayerManager.GetPlayer(state.GrabTargetPlayerSlot);
            if (PlayerUtil.IsUsable(targetPlayer))
            {
                var targetState = GetOrCreateState(targetPlayer!.Slot);
                if (!targetState.IsHooking && !targetState.IsRoping)
                {
                    PlayerUtil.SetGravity(targetPlayer, 1.0f);
                }
            }
            state.GrabTargetPlayerSlot = -1;
        }

        state.GrabTargetEntityIndex = 0;
        state.IsGrabTargetFrozen = false;
    }

    private bool TryAcquireGrabTarget(IPlayer grabber, PlayerHgrState state)
    {
        if (!PlayerUtil.TryGetEyeTransform(grabber, out var eyePos, out var eyeAngles))
            return false;

        var grabberPawn = PlayerUtil.GetPawn(grabber);
        if (grabberPawn == null) return false;

        eyeAngles.ToDirectionVectors(out var forward, out _, out _);
        forward.Normalize();

        float maxDist = _config.Grab.MaxDistance;

        // 1. Ray-trace ile doğrudan vuruş kontrolü
        TraceParams? traceParams = TraceParams.Builder()
            .IgnoreEntity(grabberPawn)
            .InteractWith(MaskTrace.Solid | MaskTrace.Hitbox | MaskTrace.Player | MaskTrace.PhysicsProp | MaskTrace.CarriedObject | MaskTrace.Pushaway)
            .Build();

        var trace = _core.Trace.TraceShapeAngle(in eyePos, in eyeAngles, maxDist, in traceParams);
        float wallDist = trace.DidHit ? trace.Distance : maxDist;

        // 1A. Ray doğrudan bir oyuncuya çarptı mı?
        if (trace.DidHit && trace.HitPlayer(out var directPlayer) &&
            directPlayer != null && PlayerUtil.IsAlive(directPlayer) && directPlayer.Slot != grabber.Slot)
        {
            return LockGrabOnPlayer(grabber, state, directPlayer, eyePos);
        }

        // 1B. Nişangah hattına (görüş konisi / silindiri) en yakın canlı oyuncuyu bul
        IPlayer? bestPlayer = null;
        float bestRayPerpDist = 42.0f; // maksimum 42 birim nişangah toleransı
        float bestForwardDist = float.MaxValue;

        foreach (var candidate in _core.PlayerManager.GetAllPlayers())
        {
            if (!PlayerUtil.IsAlive(candidate) || candidate!.Slot == grabber.Slot)
                continue;

            if (!PlayerUtil.TryGetOrigin(candidate, out var candOrigin))
                continue;

            // Oyuncunun gövde merkezi (~Z+38)
            var candCenter = new Vector(candOrigin.X, candOrigin.Y, candOrigin.Z + 38.0f);
            var toCand = candCenter - eyePos;
            float proj = toCand.Dot(forward);

            if (proj <= 10.0f || proj > Math.Min(maxDist, wallDist + 48.0f))
                continue;

            var closestOnRay = eyePos + forward * proj;
            float perpDist = candCenter.Distance(closestOnRay);

            if (perpDist < bestRayPerpDist || (Math.Abs(perpDist - bestRayPerpDist) < 4.0f && proj < bestForwardDist))
            {
                bestRayPerpDist = perpDist;
                bestForwardDist = proj;
                bestPlayer = candidate;
            }
        }

        if (bestPlayer != null)
        {
            return LockGrabOnPlayer(grabber, state, bestPlayer, eyePos);
        }

        // 2. Fiziksel varlık (prop_physics, silahlar, tavuk, C4 vb.) kontrolü
        if (_config.Grab.AllowGrabEntities && trace.DidHit && trace.HitEntity<CBaseEntity>(out var hitEntity) && hitEntity != null)
        {
            if (IsGrabbableEntity(hitEntity, grabberPawn))
            {
                var entOrigin = hitEntity.AbsOrigin ?? trace.EndPos;
                state.GrabTargetPlayerSlot = -1;
                state.GrabTargetEntityIndex = hitEntity.Index;
                state.GrabDistance = Math.Clamp(eyePos.Distance(entOrigin), _config.Grab.MinHoldDistance, _config.Grab.MaxHoldDistance);
                state.IsSearchingGrabTarget = false;

                EmitAbilitySound(grabber, _config.Grab.SoundEventName);
                return true;
            }
        }

        return false;
    }

    private bool LockGrabOnPlayer(IPlayer grabber, PlayerHgrState state, IPlayer target, Vector grabberEyePos)
    {
        if (target.Slot != grabber.Slot && !_permissions.CanGrabTarget(grabber, target, out var grabberImm, out var targetImm))
        {
            PlayerUtil.SendChat(grabber, _config.General.ChatPrefix,
                $"[red]{PlayerUtil.GetName(target)}[default] dokunulmazlığı senden yüksek olduğu için Grab ile tutulamaz. (Hedef: [gold]{targetImm}[default], Sen: [green]{grabberImm}[default])");
            state.IsGrabbing = false;
            state.IsSearchingGrabTarget = false;
            return false;
        }

        if (!PlayerUtil.TryGetOrigin(target, out var targetOrigin))
            return false;

        state.GrabTargetPlayerSlot = target.Slot;
        state.GrabTargetEntityIndex = 0;
        state.GrabDistance = Math.Clamp(
            grabberEyePos.Distance(targetOrigin),
            _config.Grab.MinHoldDistance,
            _config.Grab.MaxHoldDistance);
        state.IsSearchingGrabTarget = false;

        // Hedefin aktif Hook/Rope bağlantısı varsa kes
        StopHook(target);
        StopRope(target);
        PlayerUtil.SetGravity(target, 0.0f);

        EmitAbilitySound(grabber, _config.Grab.SoundEventName);
        PlayerUtil.SendChat(grabber, _config.General.ChatPrefix,
            $"[lime]{PlayerUtil.GetName(target)}[default] yakalandı! ([yellow]Sol Tık[default]: İt | [yellow]Sağ Tık[default]: Çek | [yellow]R[default]: Fırlat)");
        PlayerUtil.SendChat(target, _config.General.ChatPrefix,
            $"[gold]{PlayerUtil.GetName(grabber)}[default] sizi Grab ile tuttu!");

        return true;
    }

    private static bool IsGrabbableEntity(CBaseEntity? entity, CCSPlayerPawn grabberPawn)
    {
        if (entity is not { IsValid: true } || entity.Index == 0 || entity.Index == grabberPawn.Index)
            return false;

        // Oyuncu kontrolcülerini (1..64) ve sabit harita fırçalarını filtrele
        if (entity.Index <= 64)
            return false;

        var designer = entity.DesignerName ?? string.Empty;
        if (string.IsNullOrEmpty(designer) || NonGrabbableDesigners.Contains(designer))
            return false;

        if (designer.StartsWith("func_", StringComparison.OrdinalIgnoreCase) ||
            designer.StartsWith("trigger_", StringComparison.OrdinalIgnoreCase) ||
            designer.StartsWith("info_", StringComparison.OrdinalIgnoreCase) ||
            designer.StartsWith("env_", StringComparison.OrdinalIgnoreCase) ||
            designer.StartsWith("light_", StringComparison.OrdinalIgnoreCase) ||
            designer.StartsWith("point_", StringComparison.OrdinalIgnoreCase) ||
            designer.StartsWith("filter_", StringComparison.OrdinalIgnoreCase) ||
            designer.StartsWith("logic_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private void ProcessGrabTick(IPlayer grabber, PlayerHgrState state)
    {
        if (!PlayerUtil.IsAlive(grabber) || !_permissions.CanUseGrab(grabber))
        {
            StopGrab(grabber);
            return;
        }

        // Henüz hedef yakalanmadıysa nişangah taramasına devam et
        if (state.IsSearchingGrabTarget)
        {
            if (!TryAcquireGrabTarget(grabber, state))
                return;
        }

        if (!PlayerUtil.TryGetEyeTransform(grabber, out var eyePos, out var eyeAngles))
        {
            StopGrab(grabber);
            return;
        }

        // Mesafe kontrolü (Mouse1 / +push -> İt, Mouse2 / +pull -> Çek)
        var buttons = grabber.PressedButtons;
        float step = _config.Grab.PushPullStep;
        if ((buttons & GameButtonFlags.Mouse1) != 0 || state.IsPushing)
        {
            state.GrabDistance = Math.Min(_config.Grab.MaxHoldDistance, state.GrabDistance + step);
        }
        if ((buttons & GameButtonFlags.Mouse2) != 0 || state.IsPulling)
        {
            state.GrabDistance = Math.Max(_config.Grab.MinHoldDistance, state.GrabDistance - step);
        }

        eyeAngles.ToDirectionVectors(out var forward, out _, out _);
        forward.Normalize();

        Vector desiredPos = eyePos + forward * state.GrabDistance;
        Vector beamStart = PlayerUtil.GetBeamOrigin(grabber);

        // Hedef bir oyuncu mu?
        if (state.GrabTargetPlayerSlot >= 0)
        {
            var target = _core.PlayerManager.GetPlayer(state.GrabTargetPlayerSlot);
            if (!PlayerUtil.IsAlive(target) || !PlayerUtil.TryGetOrigin(target, out var targetOrigin))
            {
                PlayerUtil.SendChat(grabber, _config.General.ChatPrefix, "[yellow]Yakalanan oyuncu artık uygun değil, Grab bırakıldı.");
                StopGrab(grabber);
                return;
            }

            // R tuşu: Hedefi baktığın yöne fırlat!
            if ((buttons & GameButtonFlags.R) != 0)
            {
                Vector throwVel = forward * _config.Grab.ThrowForce + new Vector(0f, 0f, 220.0f);
                PlayerUtil.SetGravity(target, 1.0f);
                PlayerUtil.SetVelocity(target, throwVel);
                PlayerUtil.SendChat(grabber, _config.General.ChatPrefix,
                    $"[lime]{PlayerUtil.GetName(target)}[default] ileri fırlatıldı!");
                StopGrab(grabber);
                return;
            }

            PlayerUtil.SetGravity(target, 0.0f);

            // Hedefin gövde merkezini desiredPos'a hizala
            Vector targetDesiredOrigin = new(desiredPos.X, desiredPos.Y, desiredPos.Z - 36.0f);
            Vector diff = targetDesiredOrigin - targetOrigin;
            float pullFactor = Math.Max(1.0f, _config.Grab.Speed) * 3.2f;
            Vector vel = diff * pullFactor;

            float speedLen = vel.Length();
            if (speedLen > 1800.0f)
                vel = (vel / speedLen) * 1800.0f;

            PlayerUtil.SetVelocity(target, vel);

            Vector beamEnd = new(targetOrigin.X, targetOrigin.Y, targetOrigin.Z + 38.0f);
            state.GrabBeam = BeamUtil.CreateOrUpdateBeam(
                _core, state.GrabBeam, beamStart, beamEnd, _grabColor, _config.Grab.BeamWidth);

            if (_config.Grab.ShowCenterHud && !grabber.IsFakeClient)
            {
                string safeName = WebUtility.HtmlEncode(PlayerUtil.GetName(target));
                string hud = $"<font class='fontSize-m' color='#F87171'><b>✋ GRAB: {safeName}</b></font> " +
                             $"<font class='fontSize-s' color='#94A3B8'>({state.GrabDistance:0} birim)</font><br>" +
                             $"<font class='fontSize-xs' color='#E2E8F0'>[Sol Tık] İt • [Sağ Tık] Çek • [R] Fırlat</font>";
                grabber.SendCenterHTML(hud, 300);
            }
            return;
        }

        // Hedef bir fiziksel nesne (prop / silah) mi?
        if (state.GrabTargetEntityIndex > 0)
        {
            var ent = _core.EntitySystem.GetEntityByIndex<CBaseEntity>(state.GrabTargetEntityIndex);
            if (ent is not { IsValid: true } || !ent.AbsOrigin.HasValue)
            {
                StopGrab(grabber);
                return;
            }

            Vector entOrigin = ent.AbsOrigin.Value;

            if ((buttons & GameButtonFlags.R) != 0)
            {
                Vector throwVel = forward * _config.Grab.ThrowForce + new Vector(0f, 0f, 180.0f);
                PlayerUtil.SetEntityVelocity(ent, throwVel);
                StopGrab(grabber);
                return;
            }

            Vector diff = desiredPos - entOrigin;
            float pullFactor = Math.Max(1.0f, _config.Grab.Speed) * 3.5f;
            Vector vel = diff * pullFactor;

            float speedLen = vel.Length();
            if (speedLen > 1800.0f)
                vel = (vel / speedLen) * 1800.0f;

            PlayerUtil.SetEntityVelocity(ent, vel);

            state.GrabBeam = BeamUtil.CreateOrUpdateBeam(
                _core, state.GrabBeam, beamStart, entOrigin, _grabColor, _config.Grab.BeamWidth);
        }
    }

    // ==============================================================================
    // 3. ROPE (admin.rope - Halat & Sarkac Fizigi)
    // ==============================================================================

    public void ToggleRope(IPlayer player)
    {
        var state = GetOrCreateState(player.Slot);
        if (state.IsRoping)
            StopRope(player);
        else
            StartRope(player);
    }

    public void StartRope(IPlayer player)
    {
        if (!PlayerUtil.IsUsable(player)) return;

        if (!_permissions.CanUseRope(player))
        {
            PlayerUtil.SendChat(player, _config.General.ChatPrefix,
                $"[red]Rope kullanmak için yetkiniz yok ([yellow]{_config.Permissions.RopePermission}[red]).");
            return;
        }

        if (!PlayerUtil.IsAlive(player))
        {
            PlayerUtil.SendChat(player, _config.General.ChatPrefix, "[red]Ölüyken Rope kullanamazsınız.");
            return;
        }

        var state = GetOrCreateState(player.Slot);
        if (state.IsRoping) return;

        if (state.IsHooking)
            StopHook(player);

        if (!PlayerUtil.TryGetOrigin(player, out var origin))
            return;

        if (!TraceLookPoint(player, _config.Rope.MaxDistance, out var hitPos))
            return;

        float initialLen = origin.Distance(hitPos);
        if (initialLen > _config.Rope.MaxLength)
        {
            PlayerUtil.SendChat(player, _config.General.ChatPrefix, "[red]Hedef nokta Rope için çok uzakta!");
            return;
        }

        state.IsRoping = true;
        state.RopeAnchor = hitPos;
        state.HasRopeAnchor = true;
        state.RopeLength = Math.Clamp(initialLen, _config.Rope.MinLength, _config.Rope.MaxLength);

        EmitAbilitySound(player, _config.Rope.SoundEventName);
        ProcessRopeTick(player, state);
    }

    public void StopRope(IPlayer? player)
    {
        if (player == null) return;
        if (!_states.TryGetValue(player.Slot, out var state) || !state.IsRoping)
            return;

        state.IsRoping = false;
        state.HasRopeAnchor = false;

        var beam = state.RopeBeam;
        BeamUtil.DestroyBeam(ref beam);
        state.RopeBeam = null;

        RestoreGravityIfIdle(player, state);
    }

    private void ProcessRopeTick(IPlayer player, PlayerHgrState state)
    {
        if (!PlayerUtil.IsAlive(player) || !_permissions.CanUseRope(player))
        {
            StopRope(player);
            return;
        }

        var pawn = PlayerUtil.GetPawn(player);
        if (pawn == null || !PlayerUtil.TryGetOrigin(player, out var origin))
        {
            StopRope(player);
            return;
        }

        var buttons = player.PressedButtons;

        // Space -> Halata tırman (halat boyunu kısalt), Ctrl -> Halattan in (halat boyunu uzat)
        if ((buttons & GameButtonFlags.Space) != 0)
        {
            state.RopeLength = Math.Max(_config.Rope.MinLength, state.RopeLength - _config.Rope.ClimbStep);
        }
        if ((buttons & GameButtonFlags.Ctrl) != 0)
        {
            state.RopeLength = Math.Min(_config.Rope.MaxLength, state.RopeLength + _config.Rope.ClimbStep);
        }

        // Yeşil halat lazerini güncelle
        var beamStart = PlayerUtil.GetBeamOrigin(player);
        state.RopeBeam = BeamUtil.CreateOrUpdateBeam(
            _core, state.RopeBeam, beamStart, state.RopeAnchor, _ropeColor, _config.Rope.BeamWidth);

        Vector toAnchor = state.RopeAnchor - origin;
        float dist = toAnchor.Length();
        if (dist < 1.0f) return;

        Vector ropeDir = toAnchor / dist;
        Vector vel = pawn.AbsVelocity;
        bool modified = false;

        // Halat gerildiğinde (dist > RopeLength) veya Space ile yukarı tırmanırken sarkaç gerilimi uygula
        if (dist > state.RopeLength || (buttons & GameButtonFlags.Space) != 0)
        {
            // Tutunma noktasından dışarı doğru olan radyal hızı sönümle (sarkaç yörüngesinde tut)
            float radialVel = vel.Dot(ropeDir);
            if (radialVel < 0f)
            {
                vel -= ropeDir * radialVel;
            }

            // Esneme miktarına göre merkeze doğru çekme kuvveti
            float stretch = Math.Max(0f, dist - state.RopeLength);
            float pullSpeed = Math.Min(
                stretch * (_config.Rope.Speed * 2.5f) + ((buttons & GameButtonFlags.Space) != 0 ? _config.Rope.Speed * 45.0f : 0f),
                _config.Rope.Speed * 165.0f);

            vel += ropeDir * pullSpeed;
            modified = true;
        }

        // Havada sallanırken W/A/S/D tuşlarıyla bakış yönünde teğetsel ivme desteği
        if (_config.Rope.SwingAirBoost > 0.01f && dist >= state.RopeLength * 0.75f &&
            PlayerUtil.TryGetEyeTransform(player, out _, out var eyeAngles))
        {
            var flatYaw = new QAngle(0f, eyeAngles.Yaw, 0f);
            flatYaw.ToDirectionVectors(out var fwd2D, out var right2D, out _);

            Vector wish = Vector.Zero;
            if ((buttons & GameButtonFlags.W) != 0) wish += fwd2D;
            if ((buttons & GameButtonFlags.S) != 0) wish -= fwd2D;
            if ((buttons & GameButtonFlags.D) != 0) wish += right2D;
            if ((buttons & GameButtonFlags.A) != 0) wish -= right2D;

            if (wish.LengthSquared() > 0.01f)
            {
                wish.Normalize();
                // İp doğrultusuna dik (teğet) bileşeni al
                wish -= ropeDir * wish.Dot(ropeDir);
                vel += wish * _config.Rope.SwingAirBoost;
                modified = true;
            }
        }

        if (modified)
        {
            PlayerUtil.SetVelocity(player, vel);
        }
    }

    // ==============================================================================
    // TICK & LIFECYCLE YONETIMI
    // ==============================================================================

    public void OnTick()
    {
        if (_states.Count == 0) return;

        foreach (var kv in _states.ToList())
        {
            var state = kv.Value;
            if (!state.IsHooking && !state.IsGrabbing && !state.IsRoping)
                continue;

            var player = _core.PlayerManager.GetPlayer(kv.Key);
            if (!PlayerUtil.IsUsable(player))
            {
                CleanupPlayerSlot(kv.Key);
                continue;
            }

            if (state.IsHooking)
                ProcessHookTick(player!, state);

            if (state.IsRoping)
                ProcessRopeTick(player!, state);

            if (state.IsGrabbing)
                ProcessGrabTick(player!, state);
        }
    }

    public void StopAllForPlayer(IPlayer? player)
    {
        if (player == null) return;
        StopHook(player);
        StopGrab(player);
        StopRope(player);

        // Eğer başka bir admin bu oyuncuyu Grab ile tutuyorsa onu da serbest bırak
        foreach (var kv in _states)
        {
            if (kv.Value.IsGrabbing && kv.Value.GrabTargetPlayerSlot == player.Slot)
            {
                var grabber = _core.PlayerManager.GetPlayer(kv.Key);
                StopGrab(grabber);
            }
        }
    }

    public void CleanupPlayerSlot(int slot)
    {
        if (_states.TryGetValue(slot, out var state))
        {
            ReleaseGrabTarget(state);

            var hb = state.HookBeam;
            BeamUtil.DestroyBeam(ref hb);
            state.HookBeam = null;

            var gb = state.GrabBeam;
            BeamUtil.DestroyBeam(ref gb);
            state.GrabBeam = null;

            var rb = state.RopeBeam;
            BeamUtil.DestroyBeam(ref rb);
            state.RopeBeam = null;

            state.IsHooking = false;
            state.IsGrabbing = false;
            state.IsRoping = false;
            _states.Remove(slot);
        }

        foreach (var kv in _states)
        {
            if (kv.Value.IsGrabbing && kv.Value.GrabTargetPlayerSlot == slot)
            {
                var grabber = _core.PlayerManager.GetPlayer(kv.Key);
                StopGrab(grabber);
            }
        }
    }

    public void ResetAllActiveAbilities()
    {
        foreach (var slot in _states.Keys.ToList())
        {
            var player = _core.PlayerManager.GetPlayer(slot);
            if (PlayerUtil.IsUsable(player))
            {
                StopAllForPlayer(player);
            }
            else
            {
                var st = _states[slot];
                var hb = st.HookBeam;
                BeamUtil.DestroyBeam(ref hb);
                st.HookBeam = null;

                var gb = st.GrabBeam;
                BeamUtil.DestroyBeam(ref gb);
                st.GrabBeam = null;

                var rb = st.RopeBeam;
                BeamUtil.DestroyBeam(ref rb);
                st.RopeBeam = null;

                st.IsHooking = false;
                st.IsGrabbing = false;
                st.IsRoping = false;
            }
        }
    }

    // ==============================================================================
    // YARDIMCI METOTLAR (RAY-TRACE, GRAVITY, SES)
    // ==============================================================================

    private bool TraceLookPoint(IPlayer player, float maxDistance, out Vector hitPoint)
    {
        hitPoint = Vector.Zero;
        var pawn = PlayerUtil.GetPawn(player);
        if (pawn == null || !PlayerUtil.TryGetEyeTransform(player, out var eyePos, out var eyeAngles))
            return false;

        try
        {
            TraceParams? traceParams = TraceParams.Builder()
                .IgnoreEntity(pawn)
                .Build();

            var trace = _core.Trace.TraceShapeAngle(in eyePos, in eyeAngles, maxDistance, in traceParams);
            if (trace.DidHit)
            {
                hitPoint = trace.EndPos;
                return true;
            }

            // Maksimum menzil içinde bir yüzeye çarpmadıysa bakış yönündeki uç noktayı kullan
            eyeAngles.ToDirectionVectors(out var forward, out _, out _);
            forward.Normalize();
            hitPoint = eyePos + forward * maxDistance;
            return true;
        }
        catch
        {
            eyeAngles.ToDirectionVectors(out var forward, out _, out _);
            forward.Normalize();
            hitPoint = eyePos + forward * Math.Min(maxDistance, 2048.0f);
            return true;
        }
    }

    private static void SaveAndSetZeroGravity(IPlayer player, PlayerHgrState state)
    {
        if (!state.GravityModified)
        {
            state.SavedGravityScale = PlayerUtil.GetGravity(player);
            state.GravityModified = true;
        }
        PlayerUtil.SetGravity(player, 0.0f);
    }

    private static void RestoreGravityIfIdle(IPlayer player, PlayerHgrState state)
    {
        if (state.IsHooking || state.IsRoping) return;
        if (!state.GravityModified) return;

        state.GravityModified = false;
        if (PlayerUtil.IsAlive(player))
        {
            PlayerUtil.SetGravity(player, state.SavedGravityScale > 0.01f ? state.SavedGravityScale : 1.0f);
        }
    }

    private void EmitAbilitySound(IPlayer player, string soundEventName)
    {
        if (!_config.General.EnableSounds || string.IsNullOrWhiteSpace(soundEventName))
            return;

        try
        {
            using var sound = new SoundEvent(soundEventName, volume: 0.75f, pitch: 1.0f);
            sound.SourceEntityIndex = player.Slot;
            if (PlayerUtil.TryGetOrigin(player, out var pos))
            {
                sound.SetFloat3("public.position", pos);
            }
            sound.Emit();
        }
        catch { /* ses olayı bulunamazsa yoksay */ }
    }
}
