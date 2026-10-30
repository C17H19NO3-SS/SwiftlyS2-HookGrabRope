namespace HookGrabRope.Config;

/// <summary>
/// HookGrabRope (HGR) eklentisinin ana yapilandirma modeli (hgr_config.toml).
/// </summary>
public sealed class PluginConfig
{
    public GeneralConfig General { get; set; } = new();
    public PermissionsConfig Permissions { get; set; } = new();
    public HookConfig Hook { get; set; } = new();
    public GrabConfig Grab { get; set; } = new();
    public RopeConfig Rope { get; set; } = new();
    public List<GrantedPlayerEntry> GrantedPlayers { get; set; } = new();
}

public sealed class GeneralConfig
{
    /// <summary>Sohbet mesajlarinin basina eklenecek on ek.</summary>
    public string ChatPrefix { get; set; } = "[gold][HGR][default]";

    /// <summary>Admin menusu acildiginda oyuncuyu dondur.</summary>
    public bool FreezeWhileMenuOpen { get; set; } = true;

    /// <summary>Yetenek kullanildiginda ses efekti calinsin mi?</summary>
    public bool EnableSounds { get; set; } = true;

    /// <summary>El basinda (RoundStart) aktif Hook/Grab/Rope baglantilarini sifirla.</summary>
    public bool ResetOnRoundStart { get; set; } = true;
}

public sealed class PermissionsConfig
{
    /// <summary>Hook kullanma yetkisi (Varsayilan: admin.hook).</summary>
    public string HookPermission { get; set; } = "admin.hook";

    /// <summary>Grab kullanma yetkisi (Varsayilan: admin.grab).</summary>
    public string GrabPermission { get; set; } = "admin.grab";

    /// <summary>Rope kullanma yetkisi (Varsayilan: admin.rope).</summary>
    public string RopePermission { get; set; } = "admin.rope";

    /// <summary>Yonetim menusu (sw_hookmenu / !hookmenu) ve yetki verme/alma komutlari icin ust yetki.</summary>
    public string AdminManagePermission { get; set; } = "admin.hgr";

    /// <summary>amx_hook.sma uyumlulugu icin admin.hook yetkisine sahip olanlar da yonetim komutlarini kullanabilsin mi?</summary>
    public bool AllowHookAdminsToManage { get; set; } = true;

    /// <summary>Adminlerin diger adminleri Grab ile yakalamasini veya yetkisini almasini engelle (amx_hook.sma ADMIN_LEVEL_Q korumasi).</summary>
    public bool AdminImmunity { get; set; } = true;
}

public sealed class HookConfig
{
    /// <summary>Herkese acik mod (amx_hook.sma icindeki hook_enabled cvar karsiligi).</summary>
    public bool EnabledForAll { get; set; } = false;

    /// <summary>Hook hiz carpani (amx_hook.sma icindeki hook_speed = 5; gercek hiz = Speed * 120 birim/sn).</summary>
    public float Speed { get; set; } = 5.0f;

    /// <summary>Hook maksimum menzili (birim).</summary>
    public float MaxDistance { get; set; } = 8192.0f;

    /// <summary>Hedefe bu mesafeden daha fazla yaklasildiginda titremeyi onlemek icin hizi yumusat.</summary>
    public float ArrivalStopDistance { get; set; } = 32.0f;

    /// <summary>Hook sirasinda Space (Yukari) ve Ctrl (Yavaslatma) tus kontrolune izin ver.</summary>
    public bool AllowAirControl { get; set; } = true;

    /// <summary>Mavi lazer kalinligi (amx_hook.sma icindeki width = 5 -> CS2 CBeam karsiligi ~2.5).</summary>
    public float BeamWidth { get; set; } = 2.5f;

    /// <summary>Hook lazer rengi (#RRGGBB formatinda; amx_hook.sma varsayilani mavi #0000FF / #0078FF).</summary>
    public string BeamColor { get; set; } = "#0078FF";

    /// <summary>Hook atildiginda calinacak CS2 ses olayi.</summary>
    public string SoundEventName { get; set; } = "UI.ButtonRolloverLarge";
}

public sealed class GrabConfig
{
    /// <summary>Herkese acik mod (grab_enabled cvar karsiligi).</summary>
    public bool EnabledForAll { get; set; } = false;

    /// <summary>Grab cekim hizi/esneklik carpani (grab_speed = 5).</summary>
    public float Speed { get; set; } = 5.0f;

    /// <summary>Grab maksimum yakalama menzili (birim).</summary>
    public float MaxDistance { get; set; } = 4096.0f;

    /// <summary>Minimum tutma mesafesi.</summary>
    public float MinHoldDistance { get; set; } = 75.0f;

    /// <summary>Maksimum tutma mesafesi.</summary>
    public float MaxHoldDistance { get; set; } = 2500.0f;

    /// <summary>Mouse1 / +push (ileri itme) ve Mouse2 / +pull (kendine cekme) hizi (tick basina birim).</summary>
    public float PushPullStep { get; set; } = 16.0f;

    /// <summary>Grab sirasinda R tusuna basildiginda hedefi baktigin yone firlatma hizi.</summary>
    public float ThrowForce { get; set; } = 950.0f;

    /// <summary>Oyuncular disindaki fiziksel nesnelerin (silahlar, prop_physics, tavuk vb.) yakalanabilmesine izin ver.</summary>
    public bool AllowGrabEntities { get; set; } = true;

    /// <summary>Grab lazer kalinligi.</summary>
    public float BeamWidth { get; set; } = 2.5f;

    /// <summary>Grab lazer rengi (#RRGGBB formatinda; varsayilan kirmizi).</summary>
    public string BeamColor { get; set; } = "#FF3232";

    /// <summary>Grab basladiginda calinacak CS2 ses olayi.</summary>
    public string SoundEventName { get; set; } = "UI.ArmsRace.BecomeLeader";

    /// <summary>Grab esnasinda ekranin ortasinda (CenterHTML) hedef ve kontrol bilgisini goster.</summary>
    public bool ShowCenterHud { get; set; } = true;
}

public sealed class RopeConfig
{
    /// <summary>Herkese acik mod (rope_enabled cvar karsiligi).</summary>
    public bool EnabledForAll { get; set; } = false;

    /// <summary>Rope sallanma / gerilim hiz carpani (rope_speed = 5).</summary>
    public float Speed { get; set; } = 5.0f;

    /// <summary>Rope maksimum tutunma menzili (birim).</summary>
    public float MaxDistance { get; set; } = 8192.0f;

    /// <summary>Halatin kisalabilecegi minimum uzunluk.</summary>
    public float MinLength { get; set; } = 60.0f;

    /// <summary>Halatin uzayabilecegi maksimum uzunluk.</summary>
    public float MaxLength { get; set; } = 4096.0f;

    /// <summary>Space (tirmanma) ve Ctrl (inme) ile halat boyu degisim hizi (tick basina birim).</summary>
    public float ClimbStep { get; set; } = 6.5f;

    /// <summary>Rope ile sallanirken W/A/S/D tuslariyla verilen ivme destegi.</summary>
    public float SwingAirBoost { get; set; } = 14.0f;

    /// <summary>Rope halat lazer kalinligi.</summary>
    public float BeamWidth { get; set; } = 2.2f;

    /// <summary>Rope lazer rengi (#RRGGBB formatinda; varsayilan yesil).</summary>
    public string BeamColor { get; set; } = "#32FF50";

    /// <summary>Rope atildiginda calinacak CS2 ses olayi.</summary>
    public string SoundEventName { get; set; } = "UI.ButtonRolloverLarge";
}

public sealed class GrantedPlayerEntry
{
    public ulong SteamId64 { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Hook { get; set; }
    public bool Grab { get; set; }
    public bool Rope { get; set; }
}

