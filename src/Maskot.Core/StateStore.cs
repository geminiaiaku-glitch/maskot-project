using System.Text.Json;

namespace Maskot.Core;

public sealed class StateStore(string directory)
{
    public string FilePath { get; } = Path.Combine(directory, "state.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string? RecoveryNotice { get; private set; }

    public AppState Load()
    {
        if (!File.Exists(FilePath)) return new();
        try { return Read(FilePath); }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            RecoveryNotice = "Data utama tidak dapat dibaca. Salinan cadangan dipulihkan jika tersedia.";
            try { return Read(FilePath + ".bak"); }
            catch (Exception b) when (b is JsonException or IOException or UnauthorizedAccessException) { return new(); }
        }
    }

    private static AppState Read(string path)
    {
        var state = JsonSerializer.Deserialize<AppState>(File.ReadAllText(path), Options)
                    ?? throw new JsonException("Empty state");
        if (state.SchemaVersion != 1) throw new JsonException("Unsupported schema version");
        state.Normalize();
        return state;
    }

    public void Save(AppState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, Options));
        if (File.Exists(FilePath))
        {
            // Never replace a good backup with a corrupted primary file.
            try { Read(FilePath); File.Copy(FilePath, FilePath + ".bak", true); }
            catch (JsonException) { File.Copy(FilePath, FilePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), true); }
        }
        File.Move(temp, FilePath, true);
    }
}
