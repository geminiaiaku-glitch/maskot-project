# Komponen dan sumber eksternal

- Momo (`src/Maskot.Windows/Assets/Momo.xaml`) dan ikon tray dibuat khusus untuk
  proyek ini sebagai aset sementara; tidak memakai karakter pihak ketiga.
- .NET runtime, WPF, Windows Forms dan Windows SDK projections didistribusikan
  Microsoft melalui SDK/NuGet resmi. Periksa `LICENSE.txt` dan
  `THIRD-PARTY-NOTICES.txt` pada distribusi runtime .NET dan metadata paket untuk
  ketentuan komponennya. Lisensi: https://github.com/dotnet/runtime/blob/main/LICENSE.TXT
  dan https://github.com/dotnet/wpf/blob/main/LICENSE.TXT.
  Windows SDK .NET targeting pack menyertakan projections/runtime WinRT dengan
  ketentuan Microsoft di https://aka.ms/WinSDKLicenseURL; metadata paketnya juga
  disertakan di folder notices pada paket portable.
- xUnit dan Microsoft.NET.Test.Sdk hanya untuk pengujian, tidak dimasukkan ke
  aplikasi akhir. Metadata dan hash paket ada di packages.lock.json.
- LRCLIB (`https://lrclib.net`) adalah layanan lirik eksternal opsional. Aplikasi
  mengakses API publiknya, tidak menyalin kode layanannya. Lirik tetap tunduk pada
  hak cipta pemegangnya; lirik tidak dibundel dalam aplikasi dan cache hanya di RAM.
- Nusa/GPT Pet hanya referensi perilaku dari spesifikasi; tidak ada kode/asetnya
  yang dimasukkan.

Repositori ini belum menetapkan lisensi untuk keseluruhan kode aplikasi.
