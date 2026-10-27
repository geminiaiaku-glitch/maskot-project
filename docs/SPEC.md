# Maskot / Momo

Aplikasi desktop Windows 11 dengan satu maskot chibi transparan. Maskot berjalan di
tepi bawah, berhenti saat kursor mendekat, dapat ditempatkan diam, dan berubah
menjadi notch setelah dilepas di target tengah atas monitor aktif. Menarik badan
notch turun mengembalikan maskot. Escape membatalkan perpindahan.

Panel berisi sesi media Windows, lirik opsional, Pomodoro, catatan, tugas, dan file
shelf. Data fitur tidak dimiliki jendela sehingga pergantian bentuk tidak meresetnya.
Pengaturan dan data disimpan lokal; tidak ada akun, backend, AI, API key, telemetry,
atau perubahan pengaturan taskbar. Login startup nonaktif secara default.

## Pilihan teknis

- C#/.NET 10 LTS + WPF: jendela layered transparan, antarmuka native, integrasi
  Win32/WinRT tanpa Chromium atau server lokal.
- `Maskot.Core`: geometri physical pixel, state machine input/mode, timer berbasis
  deadline UTC, parser LRC, penyimpanan JSON atomik; dapat dites di Linux.
- `Maskot.Windows`: WPF, monitor/taskbar Win32, GSMTC media Windows, system tray,
  HKCU Run opsional, LRCLIB opt-in.
- `Assets/Momo.xaml`: aset vektor orisinal sementara yang dapat diganti; animasi
  dipisahkan dari controller dan 20 fps saat idle / 25 fps saat berjalan. Controller berjalan 25 Hz saat
  berjalan/drag dan 5 Hz ketika diam. Deteksi monitor/taskbar/fullscreen 1 Hz.

## Tahap implementasi

1. Geometri, state machine, drag/cancel, hysteresis target docking, mode diam.
2. Jendela transparan pasif, Momo, animasi, notch, penanda docking, panel fokusable.
3. Timer, penyimpanan, catatan/tugas, file shelf tanpa pemindahan file.
4. Media WinRT, kapabilitas kontrol, sumber sesi, artwork, lirik opt-in.
5. Monitor/DPI, posisi taskbar aktual, fullscreen, tray dan login startup opsional.
6. Tes core, cross-build Windows portable, workflow CI Windows, checklist manual.

Pengujian Windows langsung adalah syarat untuk menyatakan perilaku taskbar,
click-through transparan, media, drag-and-drop, fokus, DPI dan animasi bekerja.
Cross-build dan tes core tidak menggantikan pengujian tersebut.

## Pembaruan 0.2

Gerakan berjalan berfungsi pada laporan pengguna, tetapi animasi versi pertama
tidak terlihat. Pisahkan animasi tubuh dari gerakan jendela: langkah kaki, ekor,
napas, kedip dan sapaan tetap aktif saat maskot diam. Maskot harus tetap di lapisan
teratas saat aplikasi lain fullscreen; sembunyikan saat fullscreen menjadi opt-in,
dan nilai default lama dimigrasikan. Panel mengikuti arah visual Nusa tanpa memakai
kode/aset aplikasinya. Tambahkan tes WPF nyata pada Windows sebelum paket diterbitkan.
