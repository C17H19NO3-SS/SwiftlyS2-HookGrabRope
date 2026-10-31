# HookGrabRope (HGR) - CS2 SwiftlyS2 Eklentisi

Klasik AMX Mod X / CS:GO (`amx_hook.sma`) Hook, Grab ve Rope mekaniğinin **Counter-Strike 2** ve **SwiftlyS2 (.NET 10)** altyapısı için sıfırdan geliştirilmiş modern sürümüdür.

---

## Yetkiler (Permissions)

| Yetki Düğümü | Açıklama |
| :--- | :--- |
| `admin.hook` | **Hook** kullanma yetkisi (`sw_hook1`, `sw_hook0`, `sw_hook_toggle`, `!hook`) |
| `admin.grab` | **Grab** kullanma yetkisi (`sw_grab1`, `sw_grab0`, `sw_grab_toggle`, `!grab`, `sw_push1`/`sw_push0`, `sw_pull1`/`sw_pull0`) |
| `admin.rope` | **Rope** kullanma yetkisi (`sw_rope1`, `sw_rope0`, `sw_rope_toggle`, `!rope`) |
| `admin.hgr` *(veya `admin.root`)* | Görsel yönetim menüsü (`sw_hookmenu`, `!hookmenu`) ve oyunculara yetki verme/alma komutları *(Varsayılan olarak `admin.hook` yetkisi olanlar da menüyü kullanabilir)* |

---

## Oyuncu Tuş Atamaları (Konsol Bind)

CS2 istemcisi `+` ile başlayan özel konsol komutlarını doğrudan sunucuya iletmediği için komutlar **`hook1` / `hook0`** (Hook için doğrudan prefixsiz), **`sw_grab1` / `sw_grab0`** ve **`sw_rope1` / `sw_rope0`** şeklinde çalışır.

Oyuncular geliştirici konsoluna (`~`) aşağıdaki satırları yapıştırarak **basılı tutmalı** tuş ataması yapabilir:

```text
alias +hook "hook1"; alias -hook "hook0"; bind v +hook
alias +grab "sw_grab1"; alias -grab "sw_grab0"; bind c +grab
alias +rope "sw_rope1"; alias -rope "sw_rope0"; bind x +rope
```

*(Veya bas-aç / bas-kapat için doğrudan `bind v hook_toggle`, `bind c sw_grab_toggle`, `bind x sw_rope_toggle` kullanılabilir.)*

### Kontroller ve Fizik Detayları
1. **Hook (`hook1` / `hook0` - Mavi Lazer):**
   - Aktif olduğu sürece oyuncunun yerçekimini sıfırlar ve baktığı noktaya doğru `sw_hook_speed * 120` hızıyla çeker.
   - `Space` ile yukarı ivme, `Ctrl` ile frenleme/yavaşlama yapılabilir.
2. **Grab (`sw_grab1` / `sw_grab0` - Kırmızı Lazer):**
   - Baktığınız canlı oyuncuyu veya fiziksel nesneyi (silah, prop, C4 vb.) havada tutar ve bakış yönünüzle birlikte taşır.
   - **Dokunulmazlık Kuralı:** Genel admin dokunulmazlığı kaldırılmıştır; yalnızca dokunulmazlık seviyesi (Immunity) sizden daha yüksek olan oyunculara Grab atılamaz. Eşit veya daha düşük dokunulmazlıktaki oyuncular ve yöneticiler tutulabilir.
   - **Sol Tık (`Mouse1`)** veya `sw_push1` / `sw_push0`: Hedefi ileri iter.
   - **Sağ Tık (`Mouse2`)** veya `sw_pull1` / `sw_pull0`: Hedefi kendine çeker.
   - **`R` Tuşu**: Yakalanan hedefi baktığınız yöne doğru fırlatır.
3. **Rope (`sw_rope1` / `sw_rope0` - Yeşil Lazer):**
   - Baktığınız noktaya halat atar ve gerçek sarkaç (pendulum) fiziğiyle sallanmanızı sağlar.
   - **`Space`**: Halata tırmanır (halat boyunu kısaltır).
   - **`Ctrl`**: Halattan aşağı iner (halat boyunu uzatır).
   - **`W/A/S/D`**: Havada sallanırken bakış yönünde ivme kazandırır.

---

## Komutlar

### Oyuncu Komutları
- `hook1` (Aç) / `hook0` (Kapat) / `hook_toggle` / `!hook` (Alternatif: `sw_hook1` / `sw_hook0`)
- `sw_grab1` (Aç) / `sw_grab0` (Kapat) / `sw_grab_toggle` / `!grab` / `sw_push1`/`sw_push0` / `sw_pull1`/`sw_pull0`
- `sw_rope1` (Aç) / `sw_rope0` (Kapat) / `sw_rope_toggle` / `!rope`
- `sw_hgr` / `!hgr` — Oyuncunun mevcut Hook/Grab/Rope yetki durumunu gösterir (yetkiliyse yönetim menüsünü açar).

### Admin Komutları
- `sw_hookmenu` / `!hookmenu` — Görsel CenterHTML HGR Yönetim Menüsünü açar (`W/S` Gezin, `A/D` Değiştir, `E` Seç, `R` Geri/Kapat).
- `sw_givehook <isim|#slot>` / `!givehook <isim>` — Oyuncuya Hook yetkisi verir.
- `sw_takehook <isim|#slot>` / `!takehook <isim>` — Oyuncunun Hook yetkisini geri alır.
- `sw_givegrab <isim|#slot>` / `!givegrab <isim>` — Oyuncuya Grab yetkisi verir.
- `sw_takegrab <isim|#slot>` / `!takegrab <isim>` — Oyuncunun Grab yetkisini geri alır.
- `sw_giverope <isim|#slot>` / `!giverope <isim>` — Oyuncuya Rope yetkisi verir.
- `sw_takerope <isim|#slot>` / `!takerope <isim>` — Oyuncunun Rope yetkisini geri alır.
- `sw_givehgr <isim|#slot>` / `!givehgr <isim>` — Oyuncuya tüm HGR yetkilerini verir.
- `sw_takehgr <isim|#slot>` / `!takehgr <isim>` — Oyuncunun tüm HGR yetkilerini geri alır.
- `sw_hgrreload` / `!hgrreload` — `hgr_config.toml` yapılandırma dosyasını yeniden yükler.

---

## Sunucu ConVar'ları
- `sw_hook_speed` (Varsayılan: `5.0`)
- `sw_hook_enabled` (Varsayılan: `0` — `1` yapılırsa tüm oyuncular Hook kullanabilir)
- `sw_grab_speed` (Varsayılan: `5.0`)
- `sw_grab_enabled` (Varsayılan: `0` — `1` yapılırsa tüm oyuncular Grab kullanabilir)
- `sw_rope_speed` (Varsayılan: `5.0`)
- `sw_rope_enabled` (Varsayılan: `0` — `1` yapılırsa tüm oyuncular Rope kullanabilir)

