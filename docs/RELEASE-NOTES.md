**Maskot / Momo 0.3 — karaoke dan lirik** untuk Windows x64.

## Fitur baru

- Notch menampilkan baris lirik aktif dan baris berikutnya, mengikuti posisi lagu.
- Dalam mode Momo, karakter memegang mikrofon, menggerakkan mulut dan bergoyang.
  Lirik muncul di balon teks dekat Momo yang tidak menghalangi klik ke aplikasi lain.
- Saat lirik tidak tersedia, Momo memiringkan kepala, memeriksa mic dan menampilkan
  tanda tanya. Loading, instrumental, offline dan jeda ditangani terpisah.
- Lirik terus mengikuti lagu meskipun panel tertutup atau mode Momo/notch berganti.
- Seek maju/mundur langsung memilih baris yang sesuai. Posisi media tanpa durasi
  dan playback rate juga ditangani.

## Cara memperbarui dan mengaktifkan

1. Klik kanan Momo lama → **Keluar**.
2. Unduh **Maskot-win-x64.zip**, pilih **Extract All / Ekstrak Semua**, lalu jalankan
   **Maskot.exe** dari folder baru. Data catatan/tugas tetap tersimpan.
3. Klik kanan Momo → **Lirik & karaoke** → **Aktifkan lirik online**.
   Bisa juga melalui Setelan → Lirik online melalui LRCLIB.
4. Putar lagu pada pemutar yang menyediakan sesi media Windows. Pilih sumber
   melalui panel Musik jika beberapa pemutar aktif.

Lirik bersifat opt-in. Judul, artis dan durasi dikirim lewat HTTPS ke LRCLIB.
Tidak memerlukan akun/API key. Mikrofon adalah aksesori animasi: tidak merekam
atau mengakses perangkat mikrofon dan tidak menambahkan suara/vokal.

## Batas dan pengujian

Sinkronisasi mengikuti timestamp yang disediakan sumber, bukan pengenalan suara.
Jika hanya ada plain lyrics, teks diberi label **tanpa sinkronisasi**; waktu baris
tidak ditebak. Tidak semua lagu tersedia atau memiliki metadata yang cocok.
Kegagalan layanan lirik tidak mematikan kontrol musik. Tombol **Cari lagi** ada
pada panel Lirik.

Tes core dan regresi WPF Windows memeriksa timeline, cancellation/stale response,
cache/HTTP, pergantian mode, mic/mulut, ekspresi bingung, pause, balon pasif dan opt-in.
PNG serta JSON hasil run ada dalam folder windows-smoke, dengan lirik contoh
orisinal dan media uji. Hasil probe HTTPS LRCLIB dicatat terpisah tanpa teks lagu.
Integrasi dengan pemutar dan lagu pengguna nyata tetap perlu dicoba di perangkat
pengguna; ini versi percobaan.
