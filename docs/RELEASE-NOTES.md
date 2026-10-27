**Maskot / Momo 0.3.1 — mic mengikuti musik, ekspresi bingung tanpa tulisan** untuk Windows x64.

## Perbaikan

- Mic hanya terlihat saat musik sedang diputar. Pause, stop atau sesi media hilang
  menyembunyikan mic dan mengembalikan wajah Momo ke keadaan biasa.
- Saat musik dilanjutkan, mic dan pose bernyanyi/bingung kembali mengikuti lagu.
- Jika lirik tidak tersedia, instrumental atau koneksi gagal, Momo memiringkan
  kepala, mengubah alis/mulut dan menggerakkan tangan/mic. Tidak ada pesan tulisan,
  tanda tanya atau balon status pada Momo. Notch tetap ringkas tanpa pesan fallback.
- Balon teks hanya muncul untuk lirik yang tersedia. Loading tidak membuka balon.
- Saat musik diputar dengan lirik online mati, Momo tetap memegang mic tanpa
  mengirim permintaan lirik ke jaringan.
- Lirik tersinkron, seek/rewind, perubahan mode dan balon pasif tetap tersedia.

## Cara memperbarui

1. Klik kanan Momo lama → **Keluar**.
2. Unduh **Maskot-win-x64.zip** → **Extract All / Ekstrak Semua** → jalankan
   **Maskot.exe** dari folder baru. Catatan/tugas/pengaturan tetap tersimpan.
3. Untuk menampilkan lirik: klik kanan Momo → **Lirik & karaoke** →
   **Aktifkan lirik online**, lalu putar lagu.

Lirik online bersifat opt-in dan mengirim judul, artis serta durasi lewat HTTPS ke
LRCLIB. Mic adalah aksesori animasi; tidak mengakses/rekam perangkat mikrofon atau
menambahkan vokal. Suara tetap dari pemutar musik. Tidak semua lagu memiliki lirik
tersinkron; plain lyrics tetap diberi label tanpa sinkronisasi.

## Validasi

87 tes core lulus di Linux dan cross-build WPF berhasil. Penerbitan ZIP juga
mensyaratkan semua regresi WPF pada Windows lulus, termasuk pause/resume, mic,
bernyanyi, wajah/gestur bingung tanpa teks, notch, opt-in dan layering fullscreen.
Laporan JSON, PNG dengan lirik uji orisinal, dan TRX ada dalam paket.
Integrasi dengan pemutar/lagu pengguna nyata tetap perlu dicoba di perangkat pengguna.
