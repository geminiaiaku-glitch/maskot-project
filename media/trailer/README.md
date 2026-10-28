# Trailer Momo

Trailer Indonesia 32 detik, 1920×1080, 30 fps, H.264/AAC. Musik instrumental dibuat
oleh generator ini; tidak memakai lagu komersial atau rekaman mikrofon.

Visual menggunakan aset vektor Momo dari proyek dan screenshot WPF asli dari
paket `v0.4.0-preview.2` yang diverifikasi SHA256. Gerakan, sorotan kursor, latar,
transisi dan tulisan promosi disusun untuk trailer; ini presentasi produk,
bukan rekaman layar tanpa penyuntingan. Data lagu/catatan pada UI adalah fixture
orisinal proyek. Tidak memakai video atau aset Nusa.

Alur: perkenalan → teman desktop → notch hover → musik/lirik → fokus → ruang →
penutup. Caption disimpan juga dalam SRT. Poster PNG dan MP4 dihasilkan ke
`artifacts/trailer/`, bukan dimasukkan sebagai binary dalam Git.

```bash
python3 -m pip install -r media/trailer/requirements.txt
python3 scripts/make-trailer.py --assets /path/to/windows-smoke-pngs
```

Jika `--assets` tidak diberikan, script mengunduh paket rilis yang dipin dan
memverifikasi checksum sebelum mengambil PNG. Diperlukan FFmpeg dan font Open Sans
(fallback DejaVu Sans). Workflow trailer mengunggah MP4/poster/SRT ke rilis
aplikasi yang menjadi sumbernya, tanpa membuat versi aplikasi baru.
