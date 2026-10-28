// Targets Windows' built-in .NET Framework 4.8. No runtime download or execution-policy changes.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class Installer
{
    internal static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
    internal static readonly string Target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Momo");
    [STAThread] private static int Main(string[] args)
    {
        try
        {
            Verify();
            if (args.Length == 1 && args[0] == "--trust-publisher")
            {
                using (var cert = new X509Certificate2(Path.Combine(Root, "Momo-Preview.cer")))
                using (var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine))
                { store.Open(OpenFlags.ReadWrite); store.Add(cert); }
                return 0;
            }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm()); return 0;
        }
        catch (Exception e) { MessageBox.Show(e.Message, "Pemasangan Momo", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
    }
    internal static void Verify()
    {
        Check("Momo-Preview.cer", InstallerData.CertificateHash);
        Check("Momo-Identity.msix", InstallerData.PackageHash);
        Check(Path.Combine("Application", "Maskot.exe"), InstallerData.ApplicationHash);
    }
    private static void Check(string file, string expected)
    {
        using (var stream = File.OpenRead(Path.Combine(Root, file)))
        using (var sha = SHA256.Create())
        {
            var actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            if (actual != expected) throw new InvalidDataException("File pemasangan rusak atau berubah: " + file + ". Unduh dan ekstrak seluruh paket lagi.");
        }
    }
    internal static string PowerShell(string command)
    {
        var info = new ProcessStartInfo("powershell.exe", "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop';" + command)));
        info.UseShellExecute = false; info.CreateNoWindow = true; info.RedirectStandardOutput = true; info.RedirectStandardError = true;
        using (var p = Process.Start(info))
        {
            var stdout = p.StandardOutput.ReadToEnd(); var stderr = p.StandardError.ReadToEnd(); p.WaitForExit();
            if (p.ExitCode != 0) throw new InvalidOperationException("Windows belum dapat memasang Momo. " + stderr);
            return stdout.Trim();
        }
    }
    internal static string Quote(string value) { return "'" + value.Replace("'", "''") + "'"; }
    internal static string Install()
    {
        Verify();
        if (Process.GetProcessesByName("Maskot").Length != 0) throw new InvalidOperationException("Momo masih berjalan. Klik kanan Momo → Keluar, lalu tekan Pasang lagi.");
        var trusted = false;
        using (var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine))
        { store.Open(OpenFlags.ReadOnly); trusted = store.Certificates.Find(X509FindType.FindByThumbprint, InstallerData.Thumbprint, false).Count > 0; }
        if (!trusted)
        {
            var elevated = new ProcessStartInfo(Path.Combine(Root, "Pasang-Momo.exe"), "--trust-publisher"); elevated.UseShellExecute = true; elevated.Verb = "runas";
            using (var child = Process.Start(elevated)) { child.WaitForExit(); if (child.ExitCode != 0) throw new InvalidOperationException("Sertifikat Momo belum dipasang. Tidak ada izin notifikasi yang diubah."); }
        }
        CopyDirectory(Path.Combine(Root, "Application"), Target);
        File.Copy(Path.Combine(Root, "Momo-Identity.msix"), Path.Combine(Target, "Momo-Identity.msix"), true);
        var command = "$existing=Get-AppxPackage -Name 'MomoProject.MomoNotifications'; "
            + "if($existing -and $existing.Version -eq [version]'0.5.0.0'){Write-Output $existing.PackageFamilyName} else {"
            + "Add-AppxPackage -Path " + Quote(Path.Combine(Target, "Momo-Identity.msix")) + " -ExternalLocation " + Quote(Target) + ";"
            + "(Get-AppxPackage -Name 'MomoProject.MomoNotifications').PackageFamilyName}";
        var family = PowerShell(command);
        if (family.Length == 0) throw new InvalidOperationException("Windows tidak mengembalikan identitas paket. Coba pemasangan lagi.");
        return family.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
    }
    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(source)) CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
    }
}

internal sealed class SetupForm : Form
{
    private readonly CheckBox consent = new CheckBox();
    private readonly Button action = new Button();
    private readonly Label state = new Label();
    private string family;
    internal SetupForm()
    {
        Text = "Pasang Momo · Notifikasi Windows"; ClientSize = new Size(500, 510); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(16, 20, 28); ForeColor = Color.White;
        AddText("Momo membawa kabar", 25, 25, 450, 44, 21);
        AddText("Notifikasi aplikasi muncul di dekat Momo atau notch.\nAkses notifikasi tetap meminta izinmu di Windows.", 25, 83, 450, 62, 11);
        AddText("Paket percobaan ini menggunakan sertifikat penerbit pengembangan, belum sertifikat komersial. Windows akan meminta izin administrator untuk mempercayai sertifikat Momo saja.", 25, 159, 450, 91, 10);
        AddText("Penerbit: CN=Momo Project Preview\nFingerprint: " + InstallerData.Thumbprint, 25, 259, 450, 58, 9);
        consent.Text = "Saya setuju memasang aplikasi dan sertifikat penerbit Momo."; consent.Location = new Point(25, 330); consent.Size = new Size(450, 45); consent.ForeColor = Color.White;
        consent.CheckedChanged += delegate { action.Enabled = consent.Checked; }; Controls.Add(consent);
        state.Text = "Keluar dari Momo lama sebelum memasang. Catatan dan tugas tetap memakai folder data Momo yang sama."; state.Location = new Point(25, 385); state.Size = new Size(450, 55); state.ForeColor = Color.LightSteelBlue; Controls.Add(state);
        action.Text = "Pasang Momo"; action.Location = new Point(25, 451); action.Size = new Size(450, 38); action.Enabled = false; action.Click += ClickInstall; Controls.Add(action);
    }
    private void AddText(string text, int x, int y, int w, int h, int size)
    { Controls.Add(new Label { Text = text, Location = new Point(x,y), Size = new Size(w,h), Font = new Font("Segoe UI",size) }); }
    private async void ClickInstall(object sender, EventArgs e)
    {
        if (family != null)
        {
            Process.Start("explorer.exe", "shell:AppsFolder\\" + family + "!Momo"); Close(); return;
        }
        action.Enabled = false; consent.Enabled = false; state.Text = "Memasang Momo. Perhatikan dialog izin Windows jika muncul…";
        try
        {
            family = await Task.Run(() => Installer.Install()); state.Text = "Selesai. Di Momo, buka Notifikasi Windows → Aktifkan, lalu izinkan akses melalui Windows.";
            action.Text = "Buka Momo"; action.Enabled = true;
        }
        catch (Exception ex) { state.Text = ex.Message; consent.Enabled = true; action.Enabled = consent.Checked; }
    }
}
