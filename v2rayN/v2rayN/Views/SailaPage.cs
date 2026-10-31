using System.Windows.Controls;

namespace v2rayN.Views;

public enum SailaPage
{
    Home,
    Servers,
    Add,
    Settings
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
