using Rote;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Rote.Models;

/// <summary>
/// Plain data object holding all persistent application state.
/// </summary>
public class AppState
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("isExpanded")]
    public bool IsExpanded { get; set; } = true;

    [JsonPropertyName("isTopmost")]
    public bool IsTopmost { get; set; } = true;

    [JsonPropertyName("windowX")]
    public double WindowX { get; set; }

    [JsonPropertyName("windowY")]
    public double WindowY { get; set; }

    /// <summary>
    /// Returns sane defaults for a first-time launch. Window coordinates use
    /// <see cref="AppConstants.DefaultPositionSentinel"/> (-1) to signal
    /// "position on open".
    /// </summary>
    public static AppState Default()
    {
        return new AppState
        {
            Content = string.Empty,
            IsExpanded = true,
            IsTopmost = true,
            WindowX = AppConstants.DefaultPositionSentinel,
            WindowY = AppConstants.DefaultPositionSentinel,
        };
    }
}

/// <summary>
/// Compile-time JSON serialization context (source generation).
///
/// Replaces reflection-based serialization so the app stays trim-/AOT-safe
/// under <c>TrimMode=partial</c> (review F4), and removes the need for
/// case-insensitive matching that masked property-name typos (review F18).
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppState))]
internal partial class AppStateJsonContext : JsonSerializerContext
{
}
