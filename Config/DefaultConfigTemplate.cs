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
        #   bind <tuş> +hook      -> Basılı tutarken baktığınız noktaya çeker (Mavi Lazer)
        #   bind <tuş> +grab      -> Basılı tutarken baktığınız oyuncuyu/nesneyi tutar (Kırmızı Lazer)
        #   bind <tuş> +rope      -> Basılı tutarken baktığınız noktadan halatla sallanır (Yeşil Lazer)
        #
        #   Alternatif Aç/Kapat Komutları:
        #   hook_toggle (!hook) | grab_toggle (!grab) | rope_toggle (!rope)
        #
        # ADMİN KOMUTLARI:
        #   sw_hookmenu / !hookmenu -> Görsel HGR Yönetim Menüsü
        #   amx_give_hook <isim/#id> | amx_take_hook <isim/#id>  (!givehook / !takehook)
        #   amx_give_grab <isim/#id> | amx_take_grab <isim/#id>  (!givegrab / !takegrab)
        #   amx_give_rope <isim/#id> | amx_take_rope <isim/#id>  (!giverope / !takerope)
        #   !hgrreload -> Yapılandırmayı yeniden yükler
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

        # true ise admin.hook yetkisine sahip olanlar da (amx_hook.sma'daki gibi) menü ve give/take komutlarını kullanabilir:
        allow_hook_admins_to_manage = true

        # true ise adminler başka bir admini Grab ile tutamaz veya yetkisini alamaz:
        admin_immunity = true

        [hook]
        # true yapılırsa sunucudaki tüm oyuncular (yetkisizler dahil) +hook kullanabilir (hook_enabled cvar):
        enabled_for_all = false

        # Hook hızı (amx_hook.sma varsayılanı: 5.0 -> 5.0 * 120 = 600 birim/sn):
        speed = 5.0
        max_distance = 8192.0
        arrival_stop_distance = 32.0
        allow_air_control = true
        beam_width = 2.5
        beam_color = "#0078FF"
        sound_event_name = "UI.ButtonRolloverLarge"

        [grab]
        # true yapılırsa sunucudaki tüm oyuncular +grab kullanabilir (grab_enabled cvar):
        enabled_for_all = false

        # Grab çekim hızı (grab_speed cvar):
        speed = 5.0
        max_distance = 4096.0
        min_hold_distance = 75.0
        max_hold_distance = 2500.0

        # Mouse1 (+push) ile ileri itme ve Mouse2 (+pull) ile kendine çekme adım hızı:
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
        # true yapılırsa sunucudaki tüm oyuncular +rope kullanabilir (rope_enabled cvar):
        enabled_for_all = false

        # Rope sallanma ve gerilim hızı (rope_speed cvar):
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

