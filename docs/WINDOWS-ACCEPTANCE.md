# Pemeriksaan langsung Windows

**Belum dijalankan di cloud Linux.** Isi hasil, konfigurasi Windows, versi pemutar,
monitor/DPI, langkah reproduksi dan screenshot setelah mengujinya. Jangan mengubah
kotak menjadi lulus berdasarkan build atau tes core saja.

Persiapan: Windows 11 x64, build portable hasil publish, dua monitor bila tersedia,
satu pemutar GSMTC (misalnya Spotify/browser), file uji yang aman. Opsi login
startup harus mati pada profil baru. Data uji dapat dicadangkan dari LocalAppData.

- [ ] Mulai Maskot.exe: hanya satu karakter, transparan, tanpa title bar dan
  taskbar button. Fokus keyboard aplikasi sebelumnya tidak berpindah.
- [ ] Klik aplikasi di belakang area transparan di sudut jendela, celah telinga,
  samping badan, dan bawah kaki: klik diterima aplikasi di belakang. Tubuh Momo
  tetap menerima klik/drag. Ulangi pada skala 100%, 150% dan 200%.
- [ ] Jalan-jalan: berbalik di ujung, idle/duduk/tidur muncul sesekali. Kursor
  mendekat menghentikan gerak; menjauh melanjutkan. Animasi tambahan dapat dimatikan.
- [ ] Klik dengan sedikit jitter membuka/menutup panel; gerakan lebih besar
  menyeret tanpa memicu klik setelah mouse dilepas.
- [ ] Lepas di tengah layar: diam di sana, tidak jatuh/berjalan. Restart memulihkan
  posisi. Perintah Jalan-jalan mengembalikan ke tepi bawah.
- [ ] Drag dekat tengah atas: penanda muncul, berubah saat eligible; tubuh tetap
  maskot sampai dilepas. Tidak berkedip di ambang target. Lepas di luar tidak dock.
- [ ] Lepas di target: transisi menjadi notch. Tarik badan notch ke bawah:
  kembali maskot diangkat, lalu diam setelah dilepas. Wiggle kecil tidak undock.
- [ ] Escape selama drag mengembalikan mode/posisi awal termasuk saat notch telah
  dilepas dan saat berpindah monitor. Tidak membuka panel setelah mouse dilepas.
- [ ] Panel terbuka menghentikan jalan; tombol/slider/kolom teks tidak menyeret
  maskot. Input fokus ketika diedit. Klik di luar/Escape menutup. Panel tidak
  keluar work area bahkan di sudut dan pada monitor beresolusi kecil.
- [ ] Musik: pilih sumber saat beberapa pemutar aktif, title/artis/cover, progres,
  play/pause, previous/next, seek mengikuti sesi nyata. Pemutar tanpa seek/durasi
  tidak menawarkan kontrol yang tidak didukung. Tutup pemutar: empty state.
- [ ] Lirik default mati; aktifkan dan periksa lagu dengan LRC, plain lyrics,
  instrumental, tidak ditemukan dan offline. Ganti lagu cepat: tidak muncul lirik
  lagu sebelumnya. Kegagalan lirik tidak menghalangi media.
- [ ] Pomodoro: mulai/jeda/lanjut/reset, atur fokus/istirahat. Tutup panel dan dock/
  undock: tetap akurat. Tidurkan PC melewati deadline: satu notifikasi selesai,
  fase berikutnya menunggu Mulai. Restart dengan timer aktif memulihkan deadline.
- [ ] Catatan dan tugas: tambah/edit/centang/hapus, keluar dan buka ulang.
- [ ] Shelf: drop file/folder dari Explorer ke tubuh Momo/notch saat panel tertutup,
  lalu ke panel. Buka file dan lokasi, drag nama file
  ke aplikasi lain. Hapus pintasan tidak menghapus file. Pindah/hapus file asli:
  item menandai file tidak ditemukan. Tidak ada Move effect yang ditawarkan.
- [ ] Taskbar selalu terlihat: kaki di atas taskbar. Auto-hide: cek saat benar-benar
  tersembunyi, reveal mouse, muncul dan hilang, fullscreen, taskbar monitor kedua.
  Uji status aktual, bukan hanya checkbox pengaturan. Tidak mengubah pengaturan
  Windows atau menutupi ikon/reveal edge. Catat konfigurasi yang membutuhkan fallback.
- [ ] Monitor negatif/mixed DPI: drag ke monitor lain, dock di monitor itu,
  ukuran mengikuti DPI, tidak ada duplikat. Lepas monitor ketika maskot ada di sana:
  posisi dipulihkan ke monitor aktif. Ubah skala monitor saat aplikasi berjalan.
- [ ] Sembunyikan saat fullscreen, lalu keluar fullscreen. Tray Tampilkan Momo
  memunculkan kembali. Aplikasi tidak mencuri fokus ketika idle.
- [ ] Login startup: aktifkan dari lokasi executable tetap, logout/login, lalu
  nonaktifkan dan pastikan entri HKCU Run hilang. Default profil baru tetap mati.
- [ ] Tray Keluar menutup semua jendela dan proses Maskot.exe sepenuhnya. Jalankan
  dua kali: instance kedua memberi tahu bahwa instance pertama sudah berjalan.
- [ ] Catat CPU/RAM selama berjalan, diam, notch, panel media, dan minimized
  fullscreen. Tidak ada render/polling sibuk tanpa batas.

Tes backup dapat dilakukan pada data uji: lakukan dua penyimpanan, tutup aplikasi,
cadangkan folder, rusak JSON utama, mulai lagi. Pastikan ada pemberitahuan pemulihan,
backup tidak ditimpa dengan JSON rusak, dan salinan `.corrupt-*` dipertahankan.
