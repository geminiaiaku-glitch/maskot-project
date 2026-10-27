Perbaikan **Maskot / Momo 0.2** untuk Windows 11 x64.

## Yang diperbaiki

- Animasi terlihat jelas: kaki melangkah bergantian, ekor bergoyang, badan bernapas,
  mata berkedip, kepala bergerak dan tangan menyapa. Animasi tetap aktif saat Momo
  diam atau panel terbuka, dan berhenti saat karakter tersembunyi/animasi dimatikan.
- Momo tetap tampil di lapisan teratas saat aplikasi lain fullscreen. Nilai default
  versi lama yang menyembunyikannya diperbarui otomatis. Catatan, tugas, timer,
  posisi, mode dan file shelf tetap dipertahankan.
- Panel mengikuti arah visual Nusa: hitam, sudut membulat, cover di kiri, seek tipis,
  kontrol musik putih, navigasi bawah dan lirik bernuansa biru. Implementasi/aset orisinal.
- Status Berjalan/Diam/Notch dan tombol Jalan-jalan lebih mudah ditemukan.

## Cara memperbarui

1. Klik kanan Momo lama atau ikon Momo di dekat jam Windows, lalu pilih **Keluar**.
2. Unduh **Maskot-win-x64.zip** pada bagian **Assets**. Jangan pilih Source code.
3. Klik kanan ZIP → **Extract All / Ekstrak Semua**.
4. Buka folder hasil ekstraksi dan klik dua kali **Maskot.exe**.

Tidak perlu alat coding, .NET tambahan, atau menghapus data aplikasi.

## Pengujian

44 tes core mencakup mode/drag/docking, animasi, migrasi pengaturan, timer dan data.
Workflow Windows juga menjalankan aplikasi WPF: mengukur gerak HWND, membandingkan
piksel frame jalan/idle, memeriksa opsi animasi, menguji Momo di atas jendela uji
fullscreen, panel, docking dan penghentian clock saat karakter tersembunyi.
Laporan JSON serta PNG hasil render ada di folder `windows-smoke` dalam paket.

Ini tetap **versi percobaan**. Pengujian otomatis di Windows runner bukan pengujian
manual penuh di Windows 11. Taskbar auto-hide, media nyata, DPI multi-monitor,
click-through dan fokus antar-aplikasi perlu dicoba pada perangkat pengguna.
Fullscreen eksklusif game dan secure desktop Windows tidak dapat ditimpa jendela
WPF biasa; gunakan borderless/windowed pada game jika ingin overlay terlihat.
