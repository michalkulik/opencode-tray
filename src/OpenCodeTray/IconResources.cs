using System.Drawing;
using System.Reflection;

namespace OpenCodeTray;

/// <summary>Loads the embedded tray icons.</summary>
public static class IconResources
{
    private static Icon? _normal;
    private static Icon? _alert;

    public static Icon Normal => _normal ??= Load("OpenCodeTray.icon.ico");
    public static Icon Alert => _alert ??= Load("OpenCodeTray.icon-alert.ico");

    private static Icon Load(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Brak zasobu ikony: {resourceName}");
        return new Icon(stream, SystemInformation.SmallIconSize);
    }
}
