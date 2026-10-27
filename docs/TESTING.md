# Laporan validasi · 27 Oktober 2026

Lingkungan: Debian 13 x86-64 di cloud, SDK resmi .NET 10.0.100 (SHA512 diverifikasi
terhadap metadata rilis Microsoft), tanpa desktop Windows/Wine.

## Sudah dijalankan

- Restore dependensi dari NuGet resmi, lalu restore ulang dengan locked mode.
- `bash scripts/bootstrap-cloud.sh`: menggunakan SDK terpasang dan restore terkunci;
  jalur instalasi SDK sebelumnya dijalankan dengan verifikasi SHA512 resmi.
- `bash scripts/dev.sh check`: restore, tes core, cross-build WPF Windows.
- `bash scripts/dev.sh test`: **32 lulus, 0 gagal, 0 dilewati**. Runner xUnit melalui
  VSTest, hasil TRX pada `artifacts/test-results/core.trx`.
- `bash scripts/dev.sh publish`: aplikasi **Windows x64 self-contained** berhasil
  dipublikasikan. Build WPF/XAML/C#/WinRT: **0 peringatan, 0 error**.
- `bash -n scripts/dev.sh scripts/bootstrap-cloud.sh`: sintaks shell valid.
- `python3 scripts/package.py`: ZIP portable dibuat, CRC arsip diverifikasi,
  lisensi runtime disertakan, checksum SHA256 executable/ZIP ditulis. Identitas
  binary diperiksa sebagai PE32+ Windows GUI x86-64; ini bukan eksekusi UI.
- `scripts/build-windows.ps1` juga dijalankan melalui PowerShell 7.5.4 di Linux:
  restore terkunci, 32 tes, publish, pengemasan, dan checksum berhasil.
- Paket di branch `downloads` telah diunduh ulang dari URL GitHub publik:
  HTTP 200 `application/zip`, SHA256 sesuai, CRC ZIP valid, `Maskot.exe` tersedia.

Workflow Windows pertama gagal dengan NU1004 karena `latestPatch` memilih SDK
patch lain yang meminta ILLink 10.0.13, sedangkan lockfile memakai 10.0.0.
`global.json` kini mengunci SDK 10.0.100 secara tepat. [Workflow Windows setelah
perbaikan](https://github.com/geminiaiaku-glitch/maskot-project/actions/runs/40436742889)
berhasil: restore, tes core, publish dan pengemasan PowerShell selesai, menghasilkan
artefak aplikasi serta hasil tes. Ini memverifikasi build pada Windows runner,
belum pengujian interaksi desktop. Hasil TRX juga dimasukkan dalam paket rilis.

Tes mencakup klik/jitter, offset drag, tidak memicu klik setelah drag, mode diam,
docking hanya saat release, luar target, hysteresis, undock tanpa langsung redock,
redock setelah keluar target, Escape pada ketiga mode, monitor negatif, DPI,
panel/proximity pause, bounce, penyesuaian taskbar pada model geometri, monitor
hilang, posisi panel, timer pause/resume/deadline/sleep, LRC, normalisasi, persistensi
lintas restart, backup JSON rusak, dan shelf yang tidak menghapus file asli.

Tes taskbar memakai input geometri yang diberikan ke core. Tes itu **tidak**
membuktikan deteksi taskbar Windows bekerja. Tes media lokal tidak dijalankan.

## Belum dijalankan

- Eksekusi Maskot.exe dan pemeriksaan visual WPF pada Windows 11 maupun 10.
- Transparansi/click-through, fokus/input native, tray, autostart, Explorer
  drag-and-drop, posisi taskbar yang sebenarnya dan auto-hide.
- GSMTC dengan Spotify/browser, kontrol yang ditolak pemutar, artwork/timeline,
  LRCLIB online/offline menggunakan aplikasi yang berjalan.
- Multi-monitor/DPI dan sleep/resume perangkat Windows nyata; CPU/RAM UI.

Semua skenario langsung dicatat pada [WINDOWS-ACCEPTANCE.md](WINDOWS-ACCEPTANCE.md).
Build berhasil berarti compiler menerima kode dan paket Windows terbentuk;
belum berarti seluruh kriteria penerimaan desktop lulus.

## Keterbatasan implementasi

- Taskbar dideteksi dari visibilitas/rect aktual jendela shell dan perpotongan
  monitor setiap detik. Ini bukan API resmi untuk transisi auto-hide. Jika shell
  memakai animasi/rect berbeda, margin bawah menjadi fallback; hasil nyata masih
  harus dicek. Tidak ada perubahan konfigurasi taskbar pengguna.
- Fullscreen memakai ukuran foreground window; beberapa game/shell khusus dapat
  memerlukan penyesuaian setelah diuji.
- Fase Pomodoro berikutnya disiapkan dalam keadaan jeda; tidak mengejar banyak
  siklus yang terlewat saat PC tidur. Perubahan manual jam sistem mengikuti UTC.
- Cover/durasi/seek bergantung pada data dan izin sesi media Windows.
- Aset Momo sementara, animasi transform vektor; belum ada sprite final.
- Build portable belum ditandatangani. Belum ada installer, update otomatis,
  build ARM64, atau fitur tambahan baterai/volume/pengingat minum.

Tidak ada bug aplikasi yang dikonfirmasi lewat eksekusi Windows di lingkungan ini.
Keterbatasan di atas dan pengujian manual yang belum dijalankan tetap terbuka.
