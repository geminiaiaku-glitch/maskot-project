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
