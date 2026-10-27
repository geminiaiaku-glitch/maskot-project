# Audit dan optimasi Momo 0.3.2

Audit kode dilakukan bersama model Astra atas permintaan pengguna. Fokus mencakup
lifecycle aplikasi, media/lirik, animasi, penyimpanan, desktop, privasi dan pengemasan.

## Temuan yang diperbaiki

| Area | Temuan | Perbaikan dan bukti |
| --- | --- | --- |
| Media | Setiap timeline/playback event membaca dan mendekode ulang artwork; callback bisa datang di luar UI thread | Playback diperbarui langsung, metadata hanya saat diperlukan, callback dimarshalling; regresi service pada Windows |
| Pergantian pemutar | Hasil async lama dapat tertunda atau memuat state usang | Refresh digabung, identitas sesi diperiksa, operasi native dibatalkan pada switch/dispose; simulasi event lambat dan gagal |
| Lirik/animasi | Scan dari awal dan alokasi berulang; panel tertutup masih diperbarui | Pencarian biner timestamp, pemakaian cue bersama, parsing baris tanpa Split penuh, alokasi array bar dihapus, refresh panel dibatasi saat terlihat |
| Penyimpanan | Primary hilang mengabaikan backup; autosave identik menghabiskan backup sebelumnya | Pemulihan backup, dedup dengan metadata file, penulisan temp di-flush dan penggantian atomik; regresi restart/korupsi |
| Data lama | Null entri dan nilai timer rusak dapat merusak rendering/penyimpanan | Normalisasi aman; properti shelf turunan tidak diserialisasi sehingga save tidak memeriksa file jaringan |
| Runtime/paket | Paket memakai runtime awal .NET 10.0.0 | SDK 10.0.114 dan runtime 10.0.14, lockfile diperbarui, hash resmi diverifikasi, lisensi dipilih sesuai runtime yang dibundel |

## Pemeriksaan tambahan

Shelf menawarkan copy/link, tidak menghapus atau memindahkan file asli. Taskbar
hanya dibaca geometrinya. Lirik tetap opt-in, HTTPS, tanpa akun/API key atau rekaman
mic; cancellation dan cache tetap dibatasi. Perubahan topmost tetap NOACTIVATE.
Jendela/panel, native handle, geometri monitor, drag/Escape, timer, tray dan autostart
ikut ditinjau. Regresi yang sudah ada tetap menjadi syarat rilis.

Sumber runtime resmi: https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json
Metadata yang diperiksa pada 27 Oktober 2026 menandai 10.0.14 sebagai rilis keamanan.
NuGet advisory check mencakup paket langsung/transitif pada saat build; tidak
membuktikan ketiadaan seluruh celah keamanan. Build portable belum ditandatangani.

## Validasi dan batas

97 tes core lulus di Linux. Build Windows memakai restore terkunci; runtime dan
commit tercatat di build-info.json. Windows self-test memiliki 52 pemeriksaan
termasuk render piksel, topmost, karaoke, frame clock dan media service dengan
adapter uji. Hasil run aktual dan screenshot dibundel dalam ZIP rilis.

Pengujian otomatis tidak membuktikan semua pemutar nyata, fullscreen eksklusif,
DPI campuran, hotplug monitor, taskbar auto-hide, Explorer drag/drop, login atau
pemakaian sepanjang hari. Checklist manual tetap di WINDOWS-ACCEPTANCE.md.
Optimasi mengurangi operasi yang dapat dihitung; belum ada benchmark CPU/RAM
sebelum/sesudah pada perangkat pengguna, jadi tidak diklaim angka persentase.
