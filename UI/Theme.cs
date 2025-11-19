using System.Drawing;

namespace GlobalTextHelper.UI;

public static class Theme
{
    // Colors
    public static readonly Color PrimaryColor = Color.FromArgb(79, 70, 229); // Indigo 600
    public static readonly Color PrimaryHoverColor = Color.FromArgb(67, 56, 202); // Indigo 700
    public static readonly Color PrimaryPressedColor = Color.FromArgb(55, 48, 163); // Indigo 800
    public static readonly Color PrimaryTextColor = Color.White;

    public static readonly Color SecondaryColor = Color.FromArgb(243, 244, 246); // Gray 100
    public static readonly Color SecondaryHoverColor = Color.FromArgb(229, 231, 235); // Gray 200
    public static readonly Color SecondaryPressedColor = Color.FromArgb(209, 213, 219); // Gray 300
    public static readonly Color SecondaryTextColor = Color.FromArgb(31, 41, 55); // Gray 800

    public static readonly Color BackgroundColor = Color.White;
    public static readonly Color SurfaceColor = Color.FromArgb(249, 250, 251); // Gray 50
    public static readonly Color BorderColor = Color.FromArgb(229, 231, 235); // Gray 200
    
    public static readonly Color TextColor = Color.FromArgb(17, 24, 39); // Gray 900
    public static readonly Color TextMutedColor = Color.FromArgb(107, 114, 128); // Gray 500

    public static readonly Color ErrorColor = Color.FromArgb(220, 38, 38); // Red 600

    // Fonts
    public static readonly Font HeaderFont = new Font("Segoe UI", 11F, FontStyle.Bold);
    public static readonly Font BodyFont = new Font("Segoe UI", 9.5F, FontStyle.Regular);
    public static readonly Font SmallFont = new Font("Segoe UI", 8.5F, FontStyle.Regular);
    public static readonly Font ButtonFont = new Font("Segoe UI", 9.5F, FontStyle.Bold);

    // Dimensions
    public const int CornerRadius = 6;
    public const int PaddingSmall = 8;
    public const int PaddingMedium = 16;
    public const int PaddingLarge = 24;
}
