namespace Rote;

/// <summary>
/// Single source of truth for layout, timing and persistence constants.
/// Centralising them (ADR-004) removes magic numbers scattered across
/// MainWindow / StateStorage and prevents the inconsistencies flagged in the
/// engineering review (F5 / F6 / TD-16).
/// </summary>
internal static class AppConstants
{
    // ── Window sizing (logical pixels) ──
    public const double CollapsedSize  = 36;
    public const double ExpandedWidth  = 320;
    public const double ExpandedHeight = 360;

    // ── Timing ──
    /// <summary>Debounce window for auto-save, in milliseconds.</summary>
    public const int AutoSaveDelayMs = 300;

    // ── Interaction thresholds ──
    /// <summary>Max pointer travel (px) still treated as a click, not a drag.</summary>
    public const double ClickThreshold = 5;
    /// <summary>Margin kept from screen edges / working-area bounds (px).</summary>
    public const int PositionMargin = 40;

    // ── Persistence sentinels ──
    /// <summary>windowX/Y value meaning "not yet positioned; compute on open".</summary>
    public const double DefaultPositionSentinel = -1;

    // ── File names (relative to the per-platform data folder) ──
    public const string DataFolderName       = "Rote";
    public const string StateFileName        = "state.json";
    public const string StateFileBackupName  = "state.json.bak";
    public const string StateFileTempName    = "state.json.tmp";
    public const string LogFileName          = "rote.log";
}
