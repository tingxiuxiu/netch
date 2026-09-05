namespace Netch.Ui;

/// <summary>
///     Material Design 3 light color roles, aligned with the tokens published on
///     https://m3.material.io/ (primary #6442D6 / surface #FEFBFF).
/// </summary>
internal static class MdColors
{
    public static readonly Color Primary = Color.FromArgb(0x64, 0x42, 0xD6);
    public static readonly Color OnPrimary = Color.White;
    public static readonly Color PrimaryContainer = Color.FromArgb(0x9F, 0x86, 0xFF);
    public static readonly Color OnPrimaryContainer = Color.FromArgb(0x1E, 0x00, 0x60);

    public static readonly Color Secondary = Color.FromArgb(0x5D, 0x5D, 0x74);
    public static readonly Color OnSecondary = Color.White;
    public static readonly Color SecondaryContainer = Color.FromArgb(0xDC, 0xDA, 0xF5);
    public static readonly Color OnSecondaryContainer = Color.FromArgb(0x21, 0x18, 0x2B);

    public static readonly Color TertiaryContainer = Color.FromArgb(0xF1, 0xD3, 0xF9);
    public static readonly Color OnTertiaryContainer = Color.FromArgb(0x27, 0x14, 0x30);

    public static readonly Color Background = Color.FromArgb(0xFE, 0xFB, 0xFF);
    public static readonly Color OnBackground = Color.FromArgb(0x1C, 0x1B, 0x1D);
    public static readonly Color Surface = Color.FromArgb(0xFE, 0xFB, 0xFF);
    public static readonly Color OnSurface = Color.FromArgb(0x1C, 0x1B, 0x1D);
    public static readonly Color OnSurfaceVariant = Color.FromArgb(0x4D, 0x42, 0x56);
    public static readonly Color SurfaceContainerLowest = Color.White;
    public static readonly Color SurfaceContainerLow = Color.FromArgb(0xF8, 0xF1, 0xF6);
    public static readonly Color SurfaceContainer = Color.FromArgb(0xF2, 0xEC, 0xEE);
    public static readonly Color SurfaceContainerHigh = Color.FromArgb(0xEC, 0xE7, 0xE9);
    public static readonly Color SurfaceContainerHighest = Color.FromArgb(0xE6, 0xE1, 0xE3);
    public static readonly Color SurfaceVariant = Color.FromArgb(0xE8, 0xE0, 0xE8);

    public static readonly Color Outline = Color.FromArgb(0x78, 0x75, 0x79);
    public static readonly Color OutlineVariant = Color.FromArgb(0xC6, 0xC4, 0xDE);

    public static readonly Color Error = Color.FromArgb(0xFF, 0x62, 0x40);
    public static readonly Color OnError = Color.FromArgb(0x49, 0x09, 0x09);
    public static readonly Color ErrorContainer = Color.FromArgb(0xF9, 0xDE, 0xDC);

    public static readonly Color Success = Color.FromArgb(0x34, 0xBE, 0x4D);
    public static readonly Color Caution = Color.FromArgb(0xFF, 0xCE, 0x22);

    public static readonly Color StateHover = Color.FromArgb(20, OnSurface);
    public static readonly Color StatePressed = Color.FromArgb(31, OnSurface);
    public static readonly Color Selection = Color.FromArgb(31, Primary);
}
