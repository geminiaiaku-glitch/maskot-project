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
