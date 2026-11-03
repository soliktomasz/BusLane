namespace BusLane.Styling;

using Avalonia.Media;
using Avalonia.Styling;

/// <summary>
/// Designs BusLane offers and the theme variants that carry their palettes.
/// </summary>
public static class AppThemes
{
    /// <summary>
    /// Name of the default BusLane design, as persisted in preferences and shown in Settings.
    /// </summary>
    public const string ClassicDesign = "Classic";

    /// <summary>
    /// Name of the Zed-inspired design: Zed palettes, Lilex type, dense 4px chrome.
    /// </summary>
    public const string ZedDesign = "Zed";

    /// <summary>
    /// Element layer loaded over AppStyles.axaml while the Zed design is selected.
    /// </summary>
    public const string ZedStylesUri = "avares://BusLane/Styles/Designs/Zed.axaml";

    /// <summary>
    /// Fluent palette accents for the Zed design. Fluent derives its control accents (toggles,
    /// checkboxes, progress) from FluentTheme.Palettes, not from resources a variant overrides,
    /// so App swaps these into the palettes while Zed is active.
    /// </summary>
    public static Color ZedDarkAccent { get; } = Color.Parse("#74ADE8");

    /// <inheritdoc cref="ZedDarkAccent"/>
    public static Color ZedLightAccent { get; } = Color.Parse("#5C78E2");

    /// <summary>
    /// Dark palette of the Zed design, from the Zed editor's One Dark theme.
    /// Inherits <see cref="ThemeVariant.Dark"/> so every Fluent and control resource BusLane
    /// does not override still resolves while this variant is active.
    /// </summary>
    public static ThemeVariant ZedDark { get; } = new("ZedDark", ThemeVariant.Dark);

    /// <summary>
    /// Light palette of the Zed design, from the Zed editor's One Light theme.
    /// </summary>
    public static ThemeVariant ZedLight { get; } = new("ZedLight", ThemeVariant.Light);

    /// <summary>
    /// Resolves a persisted theme and design to the variant they select.
    /// </summary>
    /// <param name="systemIsDark">
    /// Whether the OS is in dark mode. Only consulted for "System" under the Zed design,
    /// because a custom variant cannot follow the platform on its own the way Default does.
    /// </param>
    public static ThemeVariant Resolve(string? themeName, string? designName, bool systemIsDark)
    {
        var variant = themeName switch
        {
            "Dark" => ThemeVariant.Dark,
            "Light" => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };

        if (designName != ZedDesign)
            return variant;

        var dark = variant == ThemeVariant.Default ? systemIsDark : variant == ThemeVariant.Dark;
        return dark ? ZedDark : ZedLight;
    }

    /// <summary>
    /// Gets whether the variant, or any variant it inherits from, is dark.
    /// </summary>
    public static bool IsDark(ThemeVariant? variant)
    {
        while (variant is not null)
        {
            if (variant == ThemeVariant.Dark)
                return true;

            if (variant == ThemeVariant.Light)
                return false;

            variant = variant.InheritVariant;
        }

        return false;
    }
}
