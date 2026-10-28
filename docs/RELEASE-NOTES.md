**Momo 0.5 — notifikasi Windows lewat Momo** untuk Windows x64.

Notifikasi baru dari Pusat Notifikasi Windows dapat muncul dalam kartu di dekat
Momo atau notch. Fitur dimatikan secara default dan memerlukan izin Windows.
Ada Jeda, contoh tampilan, mask judul/isi dan riwayat sesi sementara. Kartu tidak
mengambil fokus; isi tidak ditulis ke disk/dikirim ke internet. Popup keamanan
Windows dan pop-up internal aplikasi tidak dibaca. Notifikasi asli tetap ada.

**Pilih Maskot-Notifikasi-Setup.zip untuk akses notifikasi.** Ekstrak seluruhnya,
keluar dari Momo lama dan buka Pasang-Momo.exe. Installer menjelaskan sertifikat
penerbit pengembangan dan meminta persetujuan/UAC. Sertifikat leaf hanya dipasang
ke TrustedPeople; private key tidak dibagikan. Setelah terpasang: klik kanan Momo
→ Notifikasi Windows → Aktifkan → izinkan pada Windows. Tidak ada izin yang
diaktifkan otomatis. Paket portable hanya dapat mencoba tampilan kartu.

Program berada di LocalAppData\Programs\Momo; catatan/tugas tetap di folder data
Maskot. Windows dapat menolak akses lewat kebijakan perangkat. Momo harus berjalan;
notifikasi lama/masa lock atau busy tidak diputar ulang setelah resume.

117 tes core dan build WPF lulus di Linux. Publikasi juga mensyaratkan 98 cek native
Windows, signature/identity installation dan panggilan API status akses. Pengiriman
notifikasi nyata dengan izin pengguna masih perlu dicoba pada perangkat Windows 11.
Lihat PASANG-NOTIFIKASI.txt dan TESTING.md untuk rincian serta batas pengujian.
