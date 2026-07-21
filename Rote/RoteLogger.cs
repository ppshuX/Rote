using System.Diagnostics;
using System.IO;
using Rote.Services;

namespace Rote;

/// <summary>
/// Lightweight, fail-safe logger.
///
/// Persistence failures used to be swallowed by a bare <c>Debug.WriteLine</c>
/// (review F13 / TD-6), so a user could silently lose their notes with no
/// trace. This logger still writes to the debugger, but also appends to
/// <c>rote.log</c> in the data folder so failures are observable on disk.
/// Logging itself can never crash the app.
/// </summary>
internal static class RoteLogger
{
    private static readonly object Lock = new();
    private static string? _logPath;

    private static string LogPath
    {
        get
        {
            if (_logPath is null)
                _logPath = Path.Combine(StateStorage.GetDataFolder(), AppConstants.LogFileName);
            return _logPath;
        }
    }

    public static void Log(string message)
    {
        var line = $"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Rote] {message}";
        Debug.WriteLine(line);
        try
        {
            Directory.CreateDirectory(StateStorage.GetDataFolder());
            lock (Lock)
            {
                File.AppendAllText(LogPath, line + System.Environment.NewLine);
            }
        }
        catch
        {
            // Never let logging crash the app.
        }
    }
}
