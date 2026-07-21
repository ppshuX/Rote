using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Rote;
using Rote.Models;

namespace Rote.Services;

/// <summary>
/// Outcome of a <see cref="Save"/> call. Lets callers detect — instead of
/// silently ignore — a failed persistence (review F13 / TD-6).
/// </summary>
public enum SaveResult
{
    /// <summary>State was written successfully.</summary>
    Success,
    /// <summary>An exception occurred; the previous good file is untouched.</summary>
    Failed,
}

/// <summary>
/// Handles loading and saving <see cref="AppState"/> as a JSON file.
///
/// Reliability guarantees (review F1 / TD-1):
///  - Saves are <b>atomic</b>: the JSON is written to a temp file in the same
///    folder, then renamed over the target. A rename on the same volume is
///    atomic, so a crash / power loss / disk-full mid-write cannot leave a
///    truncated <c>state.json</c>.
///  - Before overwriting, the current good file is copied to <c>state.json.bak</c>.
///  - On load failure, the <c>.bak</c> is recovered before falling back to
///    <see cref="AppState.Default"/>, so a corrupt write no longer silently
///    wipes the user's notes.
///
/// Serialization uses a compile-time <see cref="AppStateJsonContext"/> (source
/// generation) so it is trim-/AOT-safe with <c>TrimMode=partial</c> (F4).
/// </summary>
public class StateStorage
{
    // ── Path resolution ────────────────────────────────────────────

    /// <summary>
    /// Returns the platform-specific data directory.
    /// Windows: %LocalAppData%\Rote
    /// macOS:   ~/Library/Application Support/Rote
    /// Other:   %AppData%\Rote
    /// </summary>
    public static string GetDataFolder()
    {
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, AppConstants.DataFolderName);
        }

        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", AppConstants.DataFolderName);
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, AppConstants.DataFolderName);
    }

    private static string StateFilePath(string folder)  => Path.Combine(folder, AppConstants.StateFileName);
    private static string BackupFilePath(string folder) => Path.Combine(folder, AppConstants.StateFileBackupName);
    private static string TempFilePath(string folder)   => Path.Combine(folder, AppConstants.StateFileTempName);

    // ── Load ───────────────────────────────────────────────────────

    /// <summary>
    /// Load state from disk. Returns defaults when the file is missing or
    /// corrupt, after attempting recovery from the <c>.bak</c> backup.
    /// An optional <paramref name="dataFolder"/> enables tests against a temp dir.
    /// </summary>
    public static AppState Load(string? dataFolder = null)
    {
        var folder = dataFolder ?? GetDataFolder();
        var path = StateFilePath(folder);

        if (!File.Exists(path))
            return AppState.Default();

        try
        {
            var json = File.ReadAllText(path);
            var state = JsonSerializer.Deserialize(json, AppStateJsonContext.Default.AppState);
            if (state is null)
                return AppState.Default();

            // A clean load means we have a known-good file; back it up so a
            // future corrupt write can be recovered.
            TryBackup(path, BackupFilePath(folder));
            return state;
        }
        catch (Exception ex)
        {
            RoteLogger.Log($"Failed to load state: {ex}");

            // Attempt recovery from the last known-good backup before giving up.
            try
            {
                var backup = BackupFilePath(folder);
                if (File.Exists(backup))
                {
                    var json = File.ReadAllText(backup);
                    var state = JsonSerializer.Deserialize(json, AppStateJsonContext.Default.AppState);
                    if (state is not null)
                    {
                        RoteLogger.Log("Recovered state from .bak backup.");
                        return state;
                    }
                }
            }
            catch (Exception backupEx)
            {
                RoteLogger.Log($"Backup recovery also failed: {backupEx}");
            }

            return AppState.Default();
        }
    }

    // ── Save ───────────────────────────────────────────────────────

    /// <summary>
    /// Save state to disk atomically. Creates the data folder if needed.
    /// Returns <see cref="SaveResult.Failed"/> (and leaves the previous good
    /// file intact) if anything goes wrong instead of throwing or swallowing.
    /// An optional <paramref name="dataFolder"/> enables tests against a temp dir.
    /// </summary>
    public static SaveResult Save(AppState state, string? dataFolder = null)
    {
        var folder = dataFolder ?? GetDataFolder();

        try
        {
            Directory.CreateDirectory(folder);

            var json = JsonSerializer.Serialize(state, AppStateJsonContext.Default.AppState);
            var statePath = StateFilePath(folder);
            var tempPath = TempFilePath(folder);

            // Back up the current good file before overwriting it.
            if (File.Exists(statePath))
                File.Copy(statePath, BackupFilePath(folder), overwrite: true);

            // Atomic write: temp file then rename over the target (same volume).
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, statePath, overwrite: true);

            return SaveResult.Success;
        }
        catch (Exception ex)
        {
            RoteLogger.Log($"Failed to save state: {ex}");
            return SaveResult.Failed;
        }
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static void TryBackup(string sourcePath, string backupPath)
    {
        try
        {
            if (File.Exists(sourcePath))
                File.Copy(sourcePath, backupPath, overwrite: true);
        }
        catch
        {
            // Best-effort only.
        }
    }
}
