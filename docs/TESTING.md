# Laporan validasi 0.2 · 27 Oktober 2026

## Core dan cross-build di cloud

Debian 13 x64, .NET SDK 10.0.100 tepat, checksum SDK resmi diverifikasi.

- `bash scripts/dev.sh check`: **44 tes lulus, 0 gagal, 0 dilewati**; hasil terkini
  di `artifacts/test-results/core.trx`. Build WPF Windows: 0 peringatan, 0 error.
- Tes baru memeriksa langkah kaki berlawanan, blink terbuka/tertutup, animasi idle
  tanpa perpindahan posisi, pose lain, animasi dimatikan, migrasi default fullscreen
  dan animasi tanpa kehilangan data, pilihan pengguna setelah migrasi, respons
  Jalan-jalan serta notifikasi penutupan panel berulang.
- Tes sebelumnya tetap mencakup drag/click, Escape, docking/undock/hysteresis,
  monitor negatif/DPI, penempatan panel, timer/sleep, LRC, JSON backup dan shelf.
- Pengemasan PowerShell juga berhasil dijalankan di Linux; itu bukan uji UI Windows.

## Regresi aplikasi WPF pada Windows

[Run awal perbaikan berhasil](https://github.com/geminiaiaku-glitch/maskot-project/actions/runs/40440228617).
Workflow `.github/workflows/windows.yml` menjalankan aplikasi terpublikasi melalui
`scripts/test-windows-ui.ps1`. Instance memakai direktori data sementara, mutex
sendiri, media lokal tidak dibaca, dan renderer software untuk hasil yang konsisten.

16 pemeriksaan mencakup (15 pemeriksaan pertama sudah lulus pada run yang ditautkan;
pemeriksaan kontras tombol ditambahkan setelah pemeriksaan visual):

- View termuat dan terlihat; hide fullscreen mati secara default.
- HWND benar-benar berpindah saat berjalan, clock frame bertambah, **piksel frame
  hasil render berubah**, bukan hanya nilai model atau PID.
- Posisi diam tetap, tetapi piksel idle berubah; opsi animasi mati membuat frame
  stabil.
- Momo tetap terlihat dan berada lebih tinggi dalam Z-order daripada jendela uji
  fullscreen/topmost yang menutupi monitor.
- Panel terbuka tanpa menghentikan animasi karakter. Empat tab dirender ke PNG.
- Teks tombol utama pada latar putih memiliki rasio kontras setidaknya 4.5:1.
- Docking menampilkan notch. Clock karakter berhenti saat view tersembunyi.

Setiap run memakai folder unik. `result.json` harus menyatakan `passed: true` dan
proses harus berakhir 0. Kegagalan/time-out menggagalkan workflow dan penerbitan
rilis. PNG, JSON dan TRX run rilis disertakan dalam paket untuk pemeriksaan.

## Belum diuji manual pada Windows 11

- Game fullscreen eksklusif, semua aplikasi fullscreen pihak ketiga dan secure desktop.
- Transparansi alpha click-through, fokus/keyboard serta Explorer drag-and-drop.
- Sesi media Spotify/browser nyata, artwork/seek dan lirik online/offline.
- Taskbar auto-hide aktual, sleep/resume perangkat, mixed DPI dan monitor dicabut.
- Tray/autostart pada login pengguna nyata serta CPU/RAM jangka panjang.

Gunakan [WINDOWS-ACCEPTANCE.md](WINDOWS-ACCEPTANCE.md) untuk uji manual. Tes fullscreen
memakai jendela uji pada Windows runner, bukan bukti bahwa seluruh game atau semua
konfigurasi Windows 11 telah lulus. Render PNG tidak menggantikan pemeriksaan
click-through/fokus pada desktop pengguna.

## Perbaikan build sebelumnya

Workflow awal 0.1 gagal NU1004 karena roll-forward memilih SDK patch yang meminta
ILLink berbeda dari lockfile. SDK dikunci tepat 10.0.100; restore terkunci, build
Windows dan penerbitan 0.1.0-preview.2 kemudian berhasil. Dependency checksums/TLS
verification tidak dinonaktifkan dan lockfile tidak diubah untuk mengabaikan error.

Aset Momo adalah vektor orisinal sementara. Build portable belum ditandatangani;
belum ada installer, auto-update, ARM64, baterai, volume atau pengingat minum.

## Tambahan 0.3

79 tes core lulus pada cloud Linux: presentation karaoke, pencarian/cache HTTP,
opt-in, pembatalan, response track lama, perubahan metadata, seek/rewind, jeda,
plain/blank lyrics, pose sing/confused, geometri notch/balon dan timeline berdurasi
tidak diketahui. Build Windows cross-target juga berhasil tanpa error/peringatan.

Regresi WPF ditambah 15 pemeriksaan karaoke di atas 16 pemeriksaan sebelumnya:
notch membesar dan menampilkan teks yang benar, seek/rewind, mode tetap memakai
lirik yang sama tanpa refetch, mic dan mulut, balon visible/pasif, frame bernyanyi,
ekspresi bingung, error, jeda, tidak ada media dan opt-in. Hasil run rilis harus
menyatakan passed:true untuk seluruh pemeriksaan sebelum paket diterbitkan.

provider-probe.json memuat pengujian HTTPS LRCLIB menggunakan metadata publik
(bukan sesi pengguna), hanya mencatat ketersediaan dan jumlah baris tanpa teks lagu.
Jika layanan sedang unavailable, hasil itu terpisah dari tes logika/UI dan pengguna
akan mendapat state fallback. Fixture UI tidak membuktikan seluruh pemutar/lagu
pengguna nyata bekerja.

## Perbaikan 0.3.1

87 tes core lulus di cloud Linux, termasuk pause/resume untuk synced, plain,
missing, instrumental, error dan loading. Pose karaoke mati saat lagu dijeda;
mic saat musik diputar tidak mensyaratkan lirik online, dan opt-in tetap mencegah
lookup jaringan. State tanpa lirik tidak menghasilkan teks/balon atau notch besar.
Cross-build WPF berhasil dengan nol error/peringatan.

Regresi Windows juga memeriksa brows/mouth bingung dan perubahan piksel gestur,
tidak adanya teks pada karakter/balon, notch ringkas, mic hilang saat pause,
resume singing/confused, instrumental, loading, serta playback dengan lirik online
mati tanpa lookup. Lihat result.json dalam ZIP rilis untuk hasil native aktual;
penerbitan digagalkan jika salah satu pemeriksaan gagal. Pemutar pengguna nyata
tetap memerlukan pemeriksaan manual di perangkat pengguna.

## Audit dan optimasi 0.3.2

97 tes core lulus dengan SDK 10.0.114; cross-build WPF tanpa error/peringatan.
Tambahan mencakup pencarian lirik bertimestamp sama/berukuran besar, batas waktu,
backup ketika primary hilang, null pada data lama, autosave identik, normalisasi,
pemulihan korupsi setelah save dan pelestarian backup setelah restart.

Windows self-test menambah 11 pemeriksaan siklus media melalui adapter uji pada
service produksi: timeline tidak memuat ulang metadata, callback lintas thread,
pause selama artwork tertunda, penggabungan event, pergantian sumber, kegagalan
sementara dan disposal/cancellation. Satu pemeriksaan memastikan frame clock
berhenti ketika animasi tambahan mati. Total 52 pemeriksaan menjadi syarat rilis;
hasil aktual tersimpan dalam windows-smoke/result.json di ZIP.

NuGet advisory check dijalankan pada build Windows dan menyimpan dependency-audit.json.
build-info.json merekam commit, SDK dan framework yang disertakan. Audit luas dan
batas pengujian dijelaskan dalam AUDIT-0.3.2.md. Ini bukan hasil benchmark CPU/RAM
atau pengujian seluruh pemutar/DPI/perangkat Windows pengguna.
