# Tampilan 0.2 dan animasi

Referensi visual: pratinjau publik [Nusa](https://rama2555.github.io/nusa/) dan
[halaman sumber situsnya](https://github.com/rama2555/nusa/blob/main/index.html).
Yang diambil sebagai arah desain: notch hitam, sudut membulat, informasi singkat,
album art di kiri, tipografi putih, seek tipis, kontrol media sederhana dan aksen
hijau kecil. Implementasi WPF, kode aplikasi, aset Momo, serta animasinya ditulis
untuk proyek ini. Tidak ada kode aplikasi, logo, atau aset Nusa yang dibundel.

Panel Musik kini ringkas dan beradaptasi saat sumber/lirik dibuka. Navigasi berada
di bawah. Status Berjalan/Diam/Notch dan aksi Jalan-jalan terlihat langsung, supaya
mode diam setelah drag tidak disalahartikan sebagai kerusakan gerakan.

Momo memiliki kaki, tangan, kepala, mata, badan dan ekor terpisah. Sampling pose
bersifat deterministik; timer UI dimulai setelah view Loaded dan berhenti saat view
tidak terlihat. Animasi berjalan pada 25 fps dan idle pada 20 fps, independen dari
controller berjalan/diam dan panel. Mematikan animasi menghentikan clock ini.

Versi sebelumnya hanya memindahkan gambar statis 1–3 px. Versi ini memakai langkah
kaki bergantian, badan memantul, ekor bergoyang, napas, kedip, dan sapaan tangan.
Mode diam tetap mempertahankan posisi jendela sambil memainkan animasi idle.

Momo tidak disembunyikan saat fullscreen secara default. Pengaturan versi lama
dimigrasikan sekali: animasi aktif dan hide fullscreen mati, tanpa mengubah posisi,
mode, catatan/tugas, shelf atau timer. Pilihan baru pengguna setelah migrasi dihormati.
Z-order topmost dipertegas tanpa aktivasi/fokus. Jendela tidak dipindah/di-resize
ulang jika koordinat fisiknya sudah sesuai, untuk menghindari invalidasi layered
window yang tidak diperlukan.

Topmost berlaku pada desktop Windows biasa dan fullscreen berjendela/borderless.
Fullscreen eksklusif game atau secure desktop bukan lapisan yang dapat ditimpa
jendela WPF biasa. Pengujian taskbar, DPI dan perilaku game tetap perlu dilakukan
langsung pada konfigurasi pengguna.

## Karaoke 0.3

Lirik dikelola oleh satu KaraokeSession di luar jendela panel. Lookup hanya dimulai
setelah opt-in; perubahan track membatalkan lookup lama dan revision guard menolak
response basi. Metadata yang sama tidak men-trigger lookup per frame. Cache di RAM
memuat maksimal 32 dokumen selama 20 menit; error koneksi bisa dicoba ulang manual.

Notch menyesuaikan ukuran menjadi maksimal 420 × 96 DIP ketika lirik tersedia. Geometri
tetap di tengah monitor dan resize ditunda selama pointer ditahan. Balon lirik
308 × 106 DIP mengikuti Momo, dibatasi work area, membalik ke bawah saat dekat
tepi atas, memakai NOACTIVATE/TRANSPARENT dan tidak menerima klik. Balon disembunyikan
ketika menyeret, panel terbuka, lagu dijeda, tidak ada sesi media, loading atau
lirik tidak tersedia. Hanya teks lirik yang dapat muncul di balon.

Pose karaoke melapisi pose berjalan/idle tanpa mengubah mode atau posisi pilihan
pengguna. Mulut bergerak saat lirik aktif; mikrofon berupa vektor orisinal, bukan
akses perangkat audio. Lirik tidak tersedia menghasilkan kepala miring, alis
asimetris, mulut berkerut dan gestur tangan/mic, tanpa tulisan atau tanda tanya.
Loading/listening tidak dinyatakan sebagai lagu tanpa lirik. Pose karaoke menjadi
None saat jeda/berhenti, sehingga mic dan ekspresi karaoke ikut hilang. Saat musik
diputar tetapi lirik online mati, pose Listening memegang mic tanpa lookup jaringan.

UI test memakai metadata dan lirik pendek buatan proyek. Probe provider hanya
menyimpan status dan jumlah baris; tidak membundel lirik lagu layanan eksternal.

## Notch interaktif 0.4

Referensi tambahan adalah video 31 detik yang diunggah pengguna pada 27 Oktober 2026.
Panel menggunakan musik 420×282DIP; lirik dua kolom 620×300/330; fokus 420×360;
ruang 420×500; setelan 420×526. Viewbox menskalakan konten saat area layar kecil.
Notch lyric pill menjadi 360×44DIP dan menampilkan satu baris aktif; idle 230×42.
Panel mengembang terpusat pada tepi atas dengan sudut bawah 26; di dekat Momo semua
sudut dibulatkan. WindowMotion berhenti setelah180 ms atau saat panel ditutup.
NotchPeek di Core menguji hover intent, leave grace, editor dan suppression setelah
close. Mode/data tetap dikelola App/Core. Tidak mengimpor kode/aset/lirik dari video.

## Notifikasi Windows 0.5

WindowsNotificationSource memakai UserNotificationListener dengan capability
uap3:userNotificationListener. Identitas eksternal MSIX dan embedded msix manifest
mengikuti panduan Microsoft; tidak mengubah runtime Win32 atau memindah data lama.
Paket identitas ditandatangani sertifikat leaf pengembangan (code-signing), hanya
sertifikat publik dibagikan. Installer net48 memeriksa SHA256 executable/MSIX/CER,
meminta persetujuan, mengimpor leaf ke LocalMachine TrustedPeople melalui UAC,
kemudian mendaftarkan identitas untuk pengguna asal. Tidak memakai ExecutionPolicy
Bypass, Developer Mode, private key publik, atau perubahan izin notifikasi otomatis.

NotificationFeed menyimpan baseline/dedup,30 recent/10 pending, kartu 8 detik dan hold
saat hover/drag/menu. Semua isi berada di RAM. Polling 1.5 detik hanya ketika akses
Allowed dan tidak sedang lock/quiet; hasil async basi tidak boleh publish setelah
disable/dispose/cabut izin. Pada resume, snapshot pertama jadi baseline tanpa replay.
Renderer380×182DIP dibatasi work area, tanpa aktivasi. Notification card mendahului
balon lirik; mic tetap mengikuti playback. History projection/cache dan teks WPF
dihapus saat panel/card disembunyikan atau akses dicabut. UIMask menyembunyikan
judul/isi di kartu dan riwayat. Tutup kartu/bersihkan history tidak menghapus toast
Windows. Tidak ada aksi otomatis pada link/isi notifikasi.

Referensi: https://learn.microsoft.com/en-us/windows/uwp/design/shell/tiles-and-notifications/notification-listener
https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps
