using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HookGrabRope.Models;

/// <summary>
/// Bir oyuncunun anlik Hook, Grab ve Rope fizik/lazer durumunu tutar.
/// </summary>
public sealed class PlayerHgrState
{
    // ============================== HOOK DURUMU ==============================

    /// <summary>Oyuncu su anda +hook basili tutuyor mu / hook aktif mi?</summary>
    public bool IsHooking { get; set; }

    /// <summary>Hook hedef noktasi (amx_hook.sma icindeki hook_to[id]).</summary>
    public Vector HookTarget { get; set; }

    /// <summary>Hook hedef noktasi henuz hesaplandi mi?</summary>
    public bool HasHookTarget { get; set; }

    /// <summary>Hook aktifken olusturulan mavi lazer varligi.</summary>
    public CBeam? HookBeam { get; set; }

    // ============================== GRAB DURUMU ==============================

    /// <summary>Oyuncu su anda +grab basili tutuyor mu?</summary>
    public bool IsGrabbing { get; set; }

    /// <summary>Hedef araniyor mu (nisangah bir oyuncunun uzerine gelene kadar bekler)?</summary>
    public bool IsSearchingGrabTarget { get; set; }

    /// <summary>Yakalanan oyuncu slotu (oyuncu degilse -1).</summary>
    public int GrabTargetPlayerSlot { get; set; } = -1;

    /// <summary>Yakalanan fiziksel varlik indeksi (prop/silah vb., yoksa 0).</summary>
    public uint GrabTargetEntityIndex { get; set; }

    /// <summary>Grab mesafesi (birim cinsinden).</summary>
    public float GrabDistance { get; set; } = 200.0f;

    /// <summary>Hedef oyuncu Grab sirasinda donduruldu mu?</summary>
    public bool IsGrabTargetFrozen { get; set; }

    /// <summary>Oyuncu +push komutunu basili tutuyor mu?</summary>
    public bool IsPushing { get; set; }

    /// <summary>Oyuncu +pull komutunu basili tutuyor mu?</summary>
    public bool IsPulling { get; set; }

    /// <summary>Grab aktifken olusturulan kirmizi lazer varligi.</summary>
    public CBeam? GrabBeam { get; set; }

    /// <summary>Son R tusu basilma zamani (firlatma / dondurma debounce).</summary>
    public float LastGrabActionTime { get; set; }

    // ============================== ROPE DURUMU ==============================

    /// <summary>Oyuncu su anda +rope basili tutuyor mu / halat aktif mi?</summary>
    public bool IsRoping { get; set; }

    /// <summary>Halatin tutundugu sabit tepe/duvar noktasi.</summary>
    public Vector RopeAnchor { get; set; }

    /// <summary>Halat tutunma noktasi belirlendi mi?</summary>
    public bool HasRopeAnchor { get; set; }

    /// <summary>Halatin mevcut uzunlugu.</summary>
    public float RopeLength { get; set; } = 300.0f;

    /// <summary>Rope aktifken olusturulan yesil halat lazeri.</summary>
    public CBeam? RopeBeam { get; set; }

    // ============================== ORTAK FIZIK & YETKI ==============================

    /// <summary>Hook veya Grab oncesi oyuncunun orijinal yercekimi olcegi.</summary>
    public float SavedGravityScale { get; set; } = 1.0f;

    /// <summary>Yercekimi bu eklenti tarafindan degistirildi mi?</summary>
    public bool GravityModified { get; set; }

    /// <summary>Oyun icinden amx_give_hook / !givehook vb. ile verilen gecici oturum yetkileri (has_hook[id]).</summary>
    public HgrAbility SessionGrantedAbilities { get; set; } = HgrAbility.None;

    /// <summary>Ayni tick icinde cift komut tetiklenmesini onlemek icin son islenen komut zamani.</summary>
    public float LastCommandToggleTime { get; set; }
}

