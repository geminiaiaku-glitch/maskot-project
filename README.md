# Maskot · Momo

Teman chibi kecil untuk Windows yang bisa berjalan di tepi layar, menetap di tempat
pilihanmu, dan berubah menjadi notch. Aplikasi native C#/.NET 10 + WPF dengan
penyimpanan lokal, tanpa akun atau backend.

## Mencoba aplikasi

Target utama: **Windows 11 x64**. API minimum Windows 10 2004 (build 19041), tetapi
Windows 10 belum diuji. Build ARM64 belum disediakan.

1. Jika versi lama sedang berjalan, klik kanan Momo atau ikon Momo di dekat jam Windows,
   lalu pilih **Keluar**.
2. [Unduh **Maskot 0.3.1 — Maskot-win-x64.zip**](https://github.com/geminiaiaku-glitch/maskot-project/releases/download/v0.3.1-preview.1/Maskot-win-x64.zip).
   [Halaman rilis](https://github.com/geminiaiaku-glitch/maskot-project/releases/tag/v0.3.1-preview.1).
   Untuk build lokal, paket ada di `artifacts/Maskot-win-x64.zip`.
3. Ekstrak ke folder tetap milikmu, lalu jalankan **Maskot.exe**. Paket self-contained
   sudah membawa runtime .NET; tidak perlu memasang .NET untuk menjalankannya.
4. Momo muncul di bawah layar. Klik tubuhnya untuk membuka panel; klik kanan untuk
   Jalan-jalan, Diam di sini, Pengaturan, atau Keluar.

Build pengembangan belum ditandatangani. Ini bukan installer dan tidak menambahkan
startup, layanan, atau aturan firewall. Jika kamu memilih jalankan saat login,
biarkan executable di lokasi tetap dan nonaktifkan opsi itu sebelum memindahkannya.

**Interaksi:** seret tubuh Momo ke tengah atas monitor. Penanda muncul saat dekat,
dan menyala saat memenuhi syarat. Lepas untuk menjadi notch. Tarik badan notch
lebih dari sekitar 62 DIP ke bawah untuk melepaskannya; lepas di tempat lain agar
diam di sana. Escape mengembalikan posisi dan mode sebelum drag. Drag tidak membuka
panel. Pilih Jalan-jalan untuk berjalan lagi. Pergantian bentuk tidak mereset fitur.

**Perbaikan 0.2:** animasi kaki, tangan, ekor, napas dan kedip terpisah dari gerakan
jendela. Momo tetap beranimasi saat diam/panel terbuka dan tetap di atas jendela
fullscreen secara default. Nilai bawaan versi lama dimigrasikan tanpa menghapus
catatan/tugas/posisi. Opsi menyembunyikan saat fullscreen dapat diaktifkan sendiri.
Panel hitam ringkas dan kontrol musik memakai Nusa sebagai referensi visual;
implementasi dan aset tetap orisinal. Lihat [catatan desain](docs/DESIGN.md).

**Baru di 0.3 — karaoke:** notch menampilkan baris lirik aktif/berikutnya; Momo
bernyanyi dengan animasi mulut dan mic, disertai balon lirik pasif. Tanpa lirik,
Momo menunjukkan wajah dan gestur bingung dengan mic, tanpa tulisan atau balon status.
Mic hanya terlihat saat musik diputar; dijeda/berhenti/tidak ada sesi akan menyembunyikannya.
Mic tetap bisa muncul saat lirik online mati, tanpa permintaan jaringan.
Klik kanan Momo → **Lirik & karaoke** →
**Aktifkan lirik online**. Audio tetap dari pemutar; mic tidak direkam.

Lirik diproses bersama di luar panel, sehingga panel tidak perlu terbuka. Saat
lagu dijeda Momo berhenti bernyanyi. Lirik biasa tanpa timestamp ditandai sebagai
tidak tersinkron; aplikasi tidak mengarang waktu baris. Tombol Cari lagi tersedia
untuk koneksi gagal atau lirik yang belum ditemukan.

## Fitur

- **Maskot:** transparan, tanpa title bar; animasi idle, jalan, diangkat, tidur dan
  sapaan. Momo adalah aset vektor orisinal sementara di `Assets/Momo.xaml`.
- **Musik:** Windows Global System Media Transport Controls (GSMTC), pilihan sumber,
  judul/artis/cover, progres, play/pause, previous/next, seek. Kontrol mengikuti
  kapabilitas pemutar. Spotify/browser harus mengekspos sesi media Windows.
- **Lirik:** opt-in di Setelan. Mengirim judul, artis, dan durasi via HTTPS ke
  [LRCLIB](https://lrclib.net). LRC disinkronkan dengan posisi media; lirik biasa,
  lagu tanpa lirik, offline, timeout, dan pembatalan ditangani terpisah dari musik.
- **Pomodoro:** fokus/istirahat 1–180 menit, mulai/jeda/lanjut/reset. Deadline UTC
  mempertahankan waktu saat panel ditutup, mode berubah, restart atau komputer
  tidur. Sesi selesai sekali, lalu fase berikutnya menunggu tombol Mulai.
- **Ruang:** catatan otomatis, tugas dapat diedit/dicentang/dihapus, file shelf
  untuk file/folder. Jatuhkan file ke tubuh Momo, notch, atau panel; buka file/lokasinya atau seret nama
  file keluar. Shelf tidak memindah, menghapus, atau menyimpan salinan file asli.
- **Setelan:** ukuran, kecepatan, animasi, sembunyikan saat fullscreen, login
  startup (default mati). Tray bisa menampilkan kembali Momo, membuka panel,
  atau menghentikan aplikasi sepenuhnya.
- **Desktop:** satu maskot, geometri physical pixel, posisi monitor disimpan,
  monitor hilang dipulihkan ke layar yang tersedia, target docking per monitor.
  Posisi berjalan menyesuaikan geometri taskbar aktual dengan transisi; tersedia
  margin 5 DIP agar tepi pemuncul auto-hide tetap bebas.
  Selalu di atas jendela desktop biasa/borderless; fullscreen eksklusif game dan
  secure desktop Windows tidak dapat ditimpa jendela WPF biasa.

## Data dan privasi

Data ada di `%LOCALAPPDATA%\Maskot\state.json`, dengan backup `state.json.bak`.
Penulisan memakai temporary file dan penggantian atomik. Primary rusak dipulihkan
dari backup dan dipertahankan sebagai `*.corrupt-*` saat penyimpanan berikutnya.
Jika keduanya rusak/hilang, aplikasi mulai dengan data kosong dan memberi tahu.
Data ini mencakup catatan dan path file shelf, dan **tidak dienkripsi**.

Tidak ada telemetry, AI, cloud sync, atau kebutuhan API key. Tidak ada koneksi
jaringan aplikasi saat lirik online mati. Artwork dan metadata diambil dari sesi
media lokal Windows. Log kesalahan lokal: `%LOCALAPPDATA%\Maskot\errors.log`.
Login startup memakai hanya `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Maskot`.

## Mengembangkan dan membangun

Pasang [.NET SDK 10.0.100](https://dotnet.microsoft.com/download/dotnet/10.0)
(versi dikunci tepat di global.json agar dependency lockfile konsisten di cloud dan CI).

Di Windows, dari root proyek:

```powershell
./scripts/build-windows.ps1 -Run
```

Untuk menjalankan regresi UI pada desktop/runner Windows, sesudah publish:

```powershell
./scripts/test-windows-ui.ps1
```

Tes ini memulai instance terisolasi, tidak membaca data/media pengguna, mengukur
posisi HWND, membandingkan piksel frame animasi, menguji Z-order di atas jendela
fullscreen uji, dan menyimpan PNG serta JSON pada `artifacts/windows-smoke/`.

Atau:

```powershell
dotnet restore Maskot.slnx --locked-mode
dotnet test tests/Maskot.Core.Tests/Maskot.Core.Tests.csproj -c Release --no-restore
dotnet run --project src/Maskot.Windows/Maskot.Windows.csproj
dotnet publish src/Maskot.Windows/Maskot.Windows.csproj -c Release -r win-x64 --self-contained true -p:RestoreLockedMode=true -o artifacts/win-x64
```

Di cloud Linux (tidak menjalankan UI Windows):

```bash
bash scripts/bootstrap-cloud.sh
bash scripts/dev.sh check
bash scripts/dev.sh publish
python3 scripts/package.py
```

SDK resmi diperiksa SHA512 sebelum dipasang di `/workspace/.tools/dotnet`.
Dependensi NuGet memakai lockfile; tidak ada perubahan kode atau lockfile oleh
skrip setup. Tujuan jaringan setup: `builds.dotnet.microsoft.com`, `api.nuget.org`
dan CDN NuGet yang dipakai registry. Lirik memerlukan `lrclib.net` hanya saat aktif.

GitHub Actions (`.github/workflows/windows.yml`) menyediakan restore terkunci,
tes core, publish Windows dan upload artefak. Status dapat dilihat pada tab Actions.
Tag versi `v*` membuat rilis percobaan beserta ZIP unduhan setelah build dan tes
Windows berhasil. Workflow juga menjalankan regresi WPF otomatis; pengujian manual
Windows 11 untuk taskbar, media, fokus, click-through dan monitor/DPI tetap terpisah.

## Struktur

```text
src/Maskot.Core/        state machine, geometri, Pomodoro, LRC, penyimpanan
src/Maskot.Windows/     WPF, aset Momo, Win32/WinRT, tray, panel fitur
tests/Maskot.Core.Tests/ tes interaksi dan data, berjalan di Linux/Windows
scripts/               setup cloud dan build Windows
docs/                  spesifikasi, checklist penerimaan, laporan validasi
```

## Status dan keterbatasan

Lihat [laporan pengujian](docs/TESTING.md) untuk hasil yang benar-benar dijalankan
dan [checklist Windows](docs/WINDOWS-ACCEPTANCE.md) sebelum rilis harian.

Regresi WPF otomatis sudah berjalan pada Windows runner. Alpha click-through,
fokus pada aplikasi lain, media nyata, taskbar auto-hide, monitor/DPI, dan tray
**belum diuji manual di Windows 11**. Taskbar memakai sampling geometri
1 Hz, bukan event visibilitas resmi; konfigurasi shell khusus dapat berbeda. Margin
tepi memberi fallback stabil tanpa mengubah setting taskbar, tetapi auto-hide tetap
harus diuji di Windows. Startup dengan Windows, balloon notification, file shell
integration, dan animasi juga memerlukan pemeriksaan langsung.

Tidak semua pemutar menyediakan durasi, seek, artwork, atau kontrol lengkap;
kontrol yang tidak tersedia dinonaktifkan. DRM atau sesi yang tidak diekspos GSMTC
tidak dapat dipaksakan. Jendela fullscreen dikenali dari ukuran foreground window;
ini bukan deteksi eksklusif untuk semua game. Aplikasi elevated mungkin menolak
drag-and-drop dari proses biasa. Tidak ada fitur baterai/volume/pengingat minum
di versi pertama ini; fokusnya alur maskot dan fitur utama.

Kode ditulis untuk proyek ini; tidak memakai kode Nusa atau menyalin proyek lain.
Lisensi komponen runtime NuGet/.NET tetap berlaku; lihat [NOTICE](NOTICE.md).
