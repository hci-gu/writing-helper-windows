using System.Drawing;
using System.Windows.Forms;

namespace GlobalTextHelper.UI;

internal static class ActionButtonFactory
{
    public static Button CreatePrimaryActionButton(string text)
    {
        var button = CreateBaseActionButton(text);
        button.BackColor = Color.FromArgb(46, 90, 165);
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(61, 113, 203);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(39, 78, 143);
        return button;
    }

    public static Button CreateSecondaryActionButton(string text)
    {
        var button = CreateBaseActionButton(text);
        button.BackColor = Color.FromArgb(237, 240, 247);
        button.ForeColor = Color.FromArgb(46, 90, 165);
        button.FlatAppearance.BorderColor = Color.FromArgb(209, 216, 232);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(227, 233, 246);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(215, 224, 243);
        return button;
    }

    private static Button CreateBaseActionButton(string text)
    {
        var button = new Button
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0),
            Padding = new Padding(14, 7, 14, 7),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            Text = text,
            TabStop = false
        };

        button.FlatAppearance.BorderSize = 1;
        return button;
    }
}
