using System.Text.Json;

namespace Maskot.Core;

public sealed class StateStore(string directory)
{
    public string FilePath { get; } = Path.Combine(directory, "state.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private string? _lastSavedJson;
    private (long Length, DateTime Modified)? _lastSavedStamp;
    public string? RecoveryNotice { get; private set; }

    public AppState Load()
    {
        RecoveryNotice = null;
        _lastSavedJson = null; _lastSavedStamp = null;
        if (!File.Exists(FilePath) && !File.Exists(FilePath + ".bak")) return new();
        try
        {
            var before = Stamp();
            var state = Read(FilePath, out var json);
            // Only exact canonical state can skip its first autosave. Migrations still get persisted.
            if (before == Stamp() && json == JsonSerializer.Serialize(state, Options))
            { _lastSavedJson = json; _lastSavedStamp = before; }
            return state;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            RecoveryNotice = "Data utama tidak dapat dibaca. Salinan cadangan dipulihkan jika tersedia.";
            try { return Read(FilePath + ".bak"); }
            catch (Exception b) when (b is JsonException or IOException or UnauthorizedAccessException) { return new(); }
        }
    }

    private (long Length, DateTime Modified)? Stamp()
    {
        var file = new FileInfo(FilePath);
        return file.Exists ? (file.Length, file.LastWriteTimeUtc) : null;
    }
    private static AppState Read(string path) => Read(path, out _);
    private static AppState Read(string path, out string json)
    {
        json = File.ReadAllText(path);
        var state = JsonSerializer.Deserialize<AppState>(json, Options)
                    ?? throw new JsonException("Empty state");
        if (state.SchemaVersion != 1) throw new JsonException("Unsupported schema version");
        state.Normalize();
        return state;
    }

    public void Save(AppState state)
    {
        var json = JsonSerializer.Serialize(state, Options);
        // Preserve the previous distinct snapshot as backup, even during periodic autosaves.
        if (json == _lastSavedJson && _lastSavedStamp is not null && _lastSavedStamp == Stamp()) return;
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, leaveOpen: true)) { writer.Write(json); writer.Flush(); }
                stream.Flush(flushToDisk: true);
            }
            var replaceWithBackup = false;
            if (File.Exists(FilePath))
            {
                // Never replace a good backup with a corrupted primary file.
                try { Read(FilePath); replaceWithBackup = true; }
                catch (JsonException)
                {
                    File.Copy(FilePath, FilePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff") + "-" + Guid.NewGuid().ToString("N"));
                }
            }
            if (replaceWithBackup) File.Replace(temp, FilePath, FilePath + ".bak");
            else File.Move(temp, FilePath, true);
            _lastSavedJson = json; _lastSavedStamp = Stamp();
        }
        finally
        {
            // A failed save must remain retryable and must not hide the original exception.
            try { File.Delete(temp); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }
}
