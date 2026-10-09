namespace SmartInventory.Api.Models.ViewModels;

/// <param name="Label">Small caption, e.g. "Products".</param>
/// <param name="Value">Pre-formatted headline value.</param>
/// <param name="Icon">Sprite symbol name without the "i-" prefix (see _Icons.cshtml).</param>
/// <param name="Tone">Optional: success | warning | danger. Tints the icon only.</param>
public record StatCardViewModel(string Label, string Value, string Icon, string? Tone = null);
