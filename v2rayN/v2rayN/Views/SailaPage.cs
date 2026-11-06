using System.Windows.Controls;
using System.Windows.Media;

namespace v2rayN.Views;

public enum SailaPage
{
    Home,
    Servers,
    Add,
    Settings,
    Edit
}

public static class SailaTemplate
{
    // Named elements inside a ControlTemplate get no code-behind field; look them up on the templated control.
    public static T Part<T>(Control owner, string name) where T : class
    {
        owner.ApplyTemplate();
        return (T)owner.Template.FindName(name, owner);
    }
}

// The two edit pages shown inside the main window instead of v2rayN's server windows.
public interface ISailaEditPage
{
    event Action? Cancelled;
    void SetWide(bool wide);
}

public static class SailaEditTheme
{
    public static void Apply(FrameworkElement page)
    {
        var ui = AppManager.Instance.Config.UiItem;
        var look = ui.SailaLook ?? "Nexus";
        Brush text = look == "White" ? new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)) : Brushes.White;
        Brush muted = new SolidColorBrush(look switch
        {
            "White" => Color.FromRgb(0x47, 0x55, 0x69),
            "Glass" => Color.FromRgb(0xCB, 0xD5, 0xE1),
            _ => Color.FromRgb(0x94, 0xA3, 0xB8),
        });
        Brush card = look switch
        {
            "White" => Brushes.White,
            "Glass" => new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)),
            "Black" => new SolidColorBrush(Color.FromRgb(0x14, 0x15, 0x19)),
            _ => new LinearGradientBrush(Color.FromArgb(192, 0x16, 0x20, 0x3E), Color.FromArgb(176, 0x0A, 0x10, 0x22), new Point(0, 0), new Point(0, 1)),
        };
        Brush border = new SolidColorBrush(look switch
        {
            "White" => Color.FromRgb(0xE2, 0xE7, 0xF0),
            "Glass" => Color.FromArgb(64, 255, 255, 255),
            "Black" => Color.FromRgb(0x2A, 0x2C, 0x33),
            _ => Color.FromArgb(53, 0x82, 0xA0, 0xFF),
        });
        page.Resources["SailaEditText"] = text;
        page.Resources["SailaEditMuted"] = muted;
        page.Resources["SailaEditCardBg"] = card;
        page.Resources["SailaEditCardBorder"] = border;
        page.Resources["SailaEditAccent"] = new SolidColorBrush(SailaHomeView.ParseColor(ui.SailaAccent ?? "#7AB0FF"));
    }
}
