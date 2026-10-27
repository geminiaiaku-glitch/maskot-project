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

15 pemeriksaan mencakup:

- View termuat dan terlihat; hide fullscreen mati secara default.
- HWND benar-benar berpindah saat berjalan, clock frame bertambah, **piksel frame
  hasil render berubah**, bukan hanya nilai model atau PID.
- Posisi diam tetap, tetapi piksel idle berubah; opsi animasi mati membuat frame
  stabil.
- Momo tetap terlihat dan berada lebih tinggi dalam Z-order daripada jendela uji
  fullscreen/topmost yang menutupi monitor.
- Panel terbuka tanpa menghentikan animasi karakter. Empat tab dirender ke PNG.
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
