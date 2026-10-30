# HookGrabRope (HGR) - CS2 SwiftlyS2 Eklentisi

Klasik AMX Mod X / CS:GO (`amx_hook.sma`) Hook, Grab ve Rope mekaniğinin **Counter-Strike 2** ve **SwiftlyS2 (.NET 10)** altyapısı için sıfırdan geliştirilmiş modern sürümüdür.

---

## Yetkiler (Permissions)

| Yetki Düğümü | Açıklama |
| :--- | :--- |
| `admin.hook` | **Hook** kullanma yetkisi (`+hook`, `-hook`, `hook_toggle`, `!hook`) |
| `admin.grab` | **Grab** kullanma yetkisi (`+grab`, `-grab`, `grab_toggle`, `!grab`, `+push`, `+pull`) |
| `admin.rope` | **Rope** kullanma yetkisi (`+rope`, `-rope`, `rope_toggle`, `!rope`) |
| `admin.hgr` *(veya `admin.root`)* | Görsel yönetim menüsü (`!hgrmenu`, `amx_hook_menu`) ve oyunculara yetki verme/alma komutları *(Varsayılan olarak `admin.hook` yetkisi olanlar da menüyü kullanabilir)* |

---

## Oyuncu Tuş Atamaları (Konsol Bind)

Oyuncular geliştirici konsoluna (`~`) aşağıdaki komutları yazarak tuş ataması yapabilir:

```text
bind v +hook
bind c +grab
bind x +rope
```

### Kontroller ve Fizik Detayları
1. **Hook (`+hook` - Mavi Lazer):**
   - Basılı tutulduğu sürece oyuncunun yerçekimini sıfırlar ve baktığı noktaya doğru `hook_speed * 120` hızıyla çeker.
   - `Space` ile yukarı ivme, `Ctrl` ile frenleme/yavaşlama yapılabilir.
2. **Grab (`+grab` - Kırmızı Lazer):**
   - Baktığınız canlı oyuncuyu veya fiziksel nesneyi (silah, prop, C4 vb.) havada tutar ve bakış yönünüzle birlikte taşır.
   - **Sol Tık (`Mouse1`)** veya `+push`: Hedefi ileri iter.
   - **Sağ Tık (`Mouse2`)** veya `+pull`: Hedefi kendine çeker.
   - **`R` Tuşu**: Yakalanan hedefi baktığınız yöne doğru fırlatır.
3. **Rope (`+rope` - Yeşil Lazer):**
   - Baktığınız noktaya halat atar ve gerçek sarkaç (pendulum) fiziğiyle sallanmanızı sağlar.
   - **`Space`**: Halata tırmanır (halat boyunu kısaltır).
   - **`Ctrl`**: Halattan aşağı iner (halat boyunu uzatır).
   - **`W/A/S/D`**: Havada sallanırken bakış yönünde ivme kazandırır.

---

## Komutlar

### Oyuncu Komutları
- `+hook` / `-hook` / `hook_toggle` / `!hook`
- `+grab` / `-grab` / `grab_toggle` / `!grab` / `+push` / `+pull`
- `+rope` / `-rope` / `rope_toggle` / `!rope`
- `!hgr` — Oyuncunun mevcut Hook/Grab/Rope yetki durumunu gösterir (yetkiliyse yönetim menüsünü açar).

### Admin Komutları (`amx_hook.sma` Uyumlu + Kısayollar)
- `sw_hookmenu` / `!hookmenu` — Görsel CenterHTML HGR Yönetim Menüsünü açar (`W/S` Gezin, `A/D` Değiştir, `E` Seç, `R` Geri/Kapat).
- `amx_give_hook <isim|#slot>` / `!givehook <isim>` — Oyuncuya Hook yetkisi verir.
- `amx_take_hook <isim|#slot>` / `!takehook <isim>` — Oyuncunun Hook yetkisini geri alır.
- `amx_give_grab <isim|#slot>` / `!givegrab <isim>` — Oyuncuya Grab yetkisi verir.
- `amx_take_grab <isim|#slot>` / `!takegrab <isim>` — Oyuncunun Grab yetkisini geri alır.
- `amx_give_rope <isim|#slot>` / `!giverope <isim>` — Oyuncuya Rope yetkisi verir.
- `amx_take_rope <isim|#slot>` / `!takerope <isim>` — Oyuncunun Rope yetkisini geri alır.
- `amx_give_hgr <isim|#slot>` / `!hgrver <isim>` — Oyuncuya tüm HGR yetkilerini verir.
- `amx_take_hgr <isim|#slot>` / `!hgral <isim>` — Oyuncunun tüm HGR yetkilerini geri alır.
- `!hgrreload` — `hgr_config.toml` yapılandırma dosyasını yeniden yükler.

---

## Sunucu ConVar'ları
- `hook_speed` (Varsayılan: `5.0`)
- `hook_enabled` (Varsayılan: `0` — `1` yapılırsa tüm oyuncular Hook kullanabilir)
- `grab_speed` (Varsayılan: `5.0`)
- `grab_enabled` (Varsayılan: `0` — `1` yapılırsa tüm oyuncular Grab kullanabilir)
- `rope_speed` (Varsayılan: `5.0`)
- `rope_enabled` (Varsayılan: `0` — `1` yapılırsa tüm oyuncular Rope kullanabilir)

