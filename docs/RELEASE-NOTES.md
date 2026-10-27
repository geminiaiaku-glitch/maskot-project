**Maskot / Momo 0.3.2 — optimasi dan pemeriksaan bersama Astra** untuk Windows x64.

- Play/pause mengikuti pemutar tanpa menunggu pembacaan ulang gambar album.
- Event metadata yang bertumpuk digabung; hasil dari pemutar lama dibatalkan/diabaikan.
- Mengurangi pembaruan panel tersembunyi, pemindaian lirik, alokasi animasi dan
  penulisan data identik. Animasi yang dimatikan benar-benar menghentikan frame clock.
- Memulihkan backup saat file utama hilang, melindungi backup dari autosave identik,
  menulis data secara atomik dan menangani entri data lama yang kosong/rusak.
- Memperbarui runtime bawaan ke .NET 10.0.14 dan memeriksa advisory dependensi.

Mic tetap hanya muncul saat musik diputar; tanpa lirik Momo memakai wajah/gestur
bingung tanpa tulisan. Lirik tersedia tetap mengikuti lagu di notch/balon.

**Cara update:** klik kanan Momo lama → Keluar, ekstrak seluruh ZIP baru,
lalu jalankan Maskot.exe dari folder baru. Tidak perlu memasang .NET sendiri.

97 tes core dan build WPF lulus di cloud. Penerbitan juga mensyaratkan 52
pemeriksaan aplikasi Windows lulus. JSON/PNG/TRX dan informasi runtime ada di paket.
Pengujian media memakai adapter uji dan lirik contoh; seluruh pemutar, multi-monitor,
DPI, autostart dan penggunaan lama masih memerlukan pemeriksaan pada perangkat nyata.
Tidak ada klaim persentase penghematan CPU/RAM tanpa benchmark yang sebanding.
