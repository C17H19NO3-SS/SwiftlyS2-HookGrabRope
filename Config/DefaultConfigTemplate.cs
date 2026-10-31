namespace HookGrabRope.Config;

/// <summary>
/// Ilk calistirmada olusturulan kapsamli Turkce aciklamali varsayilan hgr_config.toml sablonu.
/// </summary>
public static class DefaultConfigTemplate
{
    public const string Content = """
        # ==============================================================================
        # HookGrabRope (HGR) - CS2 SwiftlyS2 Yapılandırma Dosyası
        # Klasik AMX Mod X / CS:GO Hook, Grab ve Rope eklentisinin CS2 uyarlaması.
        # ==============================================================================
        #
        # KULLANIM (Konsol Tuş Atamaları - Bind):
        #   CS2'de + ile başlayan özel komutlar istemci tarafından engellendiği için
        #   Hook komutları hook1/hook0, Grab ve Rope ise sw_grab1/sw_grab0 ve sw_rope1/sw_rope0 olarak çalışır:
        #
        #   alias +hook "hook1"; alias -hook "hook0"; bind <tuş> +hook
        #   alias +grab "sw_grab1"; alias -grab "sw_grab0"; bind <tuş> +grab
        #   alias +rope "sw_rope1"; alias -rope "sw_rope0"; bind <tuş> +rope
        #
        #   Alternatif Aç/Kapat Komutları:
        #   hook_toggle (!hook) | sw_grab_toggle (!grab) | sw_rope_toggle (!rope)
        #
        # ADMİN KOMUTLARI:
        #   sw_hookmenu / !hookmenu -> Görsel HGR Yönetim Menüsü
        #   sw_givehook <isim/#id> | sw_takehook <isim/#id>  (!givehook / !takehook)
        #   sw_givegrab <isim/#id> | sw_takegrab <isim/#id>  (!givegrab / !takegrab)
        #   sw_giverope <isim/#id> | sw_takerope <isim/#id>  (!giverope / !takerope)
        #   sw_givehgr <isim/#id>  | sw_takehgr <isim/#id>   (!givehgr / !takehgr)
        #   sw_hgrreload -> Yapılandırmayı yeniden yükler
        # ==============================================================================

        [general]
        chat_prefix = "[gold][HGR][default]"
        freeze_while_menu_open = true
        enable_sounds = true
        reset_on_round_start = true

        [permissions]
        # Yetenek bazlı SwiftlyS2 yetki düğümleri:
        hook_permission = "admin.hook"
        grab_permission = "admin.grab"
        rope_permission = "admin.rope"

        # Yönetim menüsü ve oyuncuya yetki verme/alma komutları için üst yetki:
        admin_manage_permission = "admin.hgr"

        # true ise admin.hook yetkisine sahip olanlar da menü ve give/take komutlarını kullanabilir:
        allow_hook_admins_to_manage = true

        # Dokunulmazlık kontrolü (true ise sadece dokunulmazlığı senden yüksek olan oyunculara Grab atılamaz):
        admin_immunity = true

        [hook]
        # true yapılırsa sunucudaki tüm oyuncular (yetkisizler dahil) hook kullanabilir (sw_hook_enabled cvar):
        enabled_for_all = false

        # Hook hızı (sw_hook_speed varsayılanı: 5.0 -> 5.0 * 120 = 600 birim/sn):
        speed = 5.0
        max_distance = 8192.0
        arrival_stop_distance = 32.0
        allow_air_control = true
        beam_width = 2.5
        beam_color = "#0078FF"
        sound_event_name = "UI.ButtonRolloverLarge"

        [grab]
        # true yapılırsa sunucudaki tüm oyuncular grab kullanabilir (sw_grab_enabled cvar):
        enabled_for_all = false

        # Grab çekim hızı (sw_grab_speed cvar):
        speed = 5.0
        max_distance = 4096.0
        min_hold_distance = 75.0
        max_hold_distance = 2500.0

        # Mouse1 / sw_push1 ile ileri itme ve Mouse2 / sw_pull1 ile kendine çekme adım hızı:
        push_pull_step = 16.0

        # Grab sırasında R tuşu ile hedefi baktığınız yöne fırlatma gücü:
        throw_force = 950.0

        # Oyuncuların yanı sıra silahları ve fiziksel nesneleri (prop) tutabilme:
        allow_grab_entities = true
        beam_width = 2.5
        beam_color = "#FF3232"
        sound_event_name = "UI.ArmsRace.BecomeLeader"
        show_center_hud = true

        [rope]
        # true yapılırsa sunucudaki tüm oyuncular rope kullanabilir (sw_rope_enabled cvar):
        enabled_for_all = false

        # Rope sallanma ve gerilim hızı (sw_rope_speed cvar):
        speed = 5.0
        max_distance = 8192.0
        min_length = 60.0
        max_length = 4096.0

        # Space (yukarı tırmanma) ve Ctrl (aşağı inme) hızı:
        climb_step = 6.5

        # Sallanırken W/A/S/D tuşlarıyla havada kazanılan ivme:
        swing_air_boost = 14.0
        beam_width = 2.2
        beam_color = "#32FF50"
        sound_event_name = "UI.ButtonRolloverLarge"

        # ==============================================================================
        # Kalıcı Yetki Verilmiş Özel Oyuncular (Opsiyonel)
        # Oyun içi !hgrmenu üzerinden de eklenip çıkarılabilir.
        # ==============================================================================
        # [[granted_players]]
        # steam_id = 76561198000000000
        # name = "OrnekOyuncu"
        # hook = true
        # grab = false
        # rope = true
        """;
}

