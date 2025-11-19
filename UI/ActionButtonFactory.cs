using System.Drawing;
using System.Windows.Forms;

namespace GlobalTextHelper.UI;

internal static class ActionButtonFactory
{
    public static Button CreatePrimaryActionButton(string text)
    {
        var button = CreateBaseActionButton(text);
        button.BackColor = Theme.PrimaryColor;
        button.ForeColor = Theme.PrimaryTextColor;
        button.Font = Theme.ButtonFont;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Theme.PrimaryHoverColor;
        button.FlatAppearance.MouseDownBackColor = Theme.PrimaryPressedColor;
        return button;
    }

    public static Button CreateSecondaryActionButton(string text)
    {
        var button = CreateBaseActionButton(text);
        button.BackColor = Theme.SecondaryColor;
        button.ForeColor = Theme.SecondaryTextColor;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Theme.SecondaryHoverColor;
        button.FlatAppearance.MouseDownBackColor = Theme.SecondaryPressedColor;
        return button;
    }

    public static Button CreateBackNavigationButton(string text)
    {
        var button = CreateBaseActionButton(text);
        button.Padding = new Padding(Theme.PaddingSmall, 6, Theme.PaddingSmall, 6);
        button.Font = Theme.SmallFont;
        button.BackColor = Color.Transparent;
        button.ForeColor = Theme.TextMutedColor;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Theme.SecondaryHoverColor;
        button.FlatAppearance.MouseDownBackColor = Theme.SecondaryPressedColor;
        return button;
    }

    private static Button CreateBaseActionButton(string text)
    {
        var button = new Button
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0),
            Padding = new Padding(Theme.PaddingMedium, 10, Theme.PaddingMedium, 10),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            Text = text,
            TabStop = false,
            Font = Theme.ButtonFont
        };

        button.FlatAppearance.BorderSize = 0;
        return button;
    }
}
