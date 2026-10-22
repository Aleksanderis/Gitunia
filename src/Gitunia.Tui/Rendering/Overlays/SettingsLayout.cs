namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// The size of the settings overlay on a screen: its width, how many rows the lists show, whether the categories
/// collapse into the list (under 80 columns) and whether the screen is too small for any layout (under 40x12).
/// </summary>
/// <param name="Width">Outer width of the box.</param>
/// <param name="BodyRows">Rows of the lists.</param>
/// <param name="CategoryWidth">Inner columns of the categories column, 0 when collapsed.</param>
/// <param name="IsCollapsed">One list with category headings instead of two columns.</param>
/// <param name="IsTooSmall">Only a "terminal too small" line fits.</param>
public sealed record SettingsLayout(int Width, int BodyRows, int CategoryWidth, bool IsCollapsed, bool IsTooSmall)
{
    /// <summary>Under this many columns the categories collapse into the settings list.</summary>
    public const int CollapseBelowWidth = 80;

    /// <summary>Under this many columns no layout fits.</summary>
    public const int MinimumScreenWidth = 40;

    /// <summary>Under this many rows no layout fits.</summary>
    public const int MinimumScreenHeight = 12;

    /// <summary>Rows around the lists: title, filter line, rule, two help lines, hint line and bottom edge.</summary>
    public const int ChromeRows = 7;

    private const int MaximumWidth = 100;
    private const int MaximumBodyRows = 16;
    private const int CategoriesInnerWidth = 16;
    private const int ScreenMargin = 2;
    private const int SmallestUsableRows = 4;

    /// <summary>The first body row, below the title and the filter line.</summary>
    public const int BodyStart = 2;

    /// <summary>The settings column's width in inner columns (everything but the categories and the divider).</summary>
    public int SettingsWidth => Width - 2 - (IsCollapsed ? 0 : CategoryWidth + 1);

    /// <summary>The frame line of the rule above the help lines.</summary>
    public int RuleLine => BodyStart + BodyRows;

    /// <summary>The frame line of the second help line (the answers of the removal question).</summary>
    public int SecondHelpLine => RuleLine + 2;

    /// <summary>The layout for a screen, or null when not even a title line fits.</summary>
    /// <param name="screenWidth">Terminal columns.</param>
    /// <param name="screenHeight">Terminal rows.</param>
    public static SettingsLayout? For(int screenWidth, int screenHeight)
    {
        if (screenWidth - ScreenMargin < OverlayBox.MinimumWidth || screenHeight - ScreenMargin < SmallestUsableRows)
        {
            return null;
        }

        var width = Math.Min(MaximumWidth, screenWidth - ScreenMargin);
        var tooSmall = screenWidth < MinimumScreenWidth || screenHeight < MinimumScreenHeight;
        var collapsed = screenWidth < CollapseBelowWidth;
        var body = Math.Clamp(screenHeight - ScreenMargin - ChromeRows, 1, MaximumBodyRows);

        return new SettingsLayout(width, body, collapsed ? 0 : CategoriesInnerWidth, collapsed, tooSmall);
    }
}
