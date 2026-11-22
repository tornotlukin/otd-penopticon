using OpenTabletDriver.Desktop.Contracts;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.DependencyInjection;

namespace Penopticon;

[PluginName("Penopticon"), SupportedPlatform(PluginPlatform.Windows)]
public class PenopticonTool : ITool
{
    [Property("Mouse Apps"),
     DefaultPropertyValue("Illustrator, InDesign"),
     ToolTip("Apps where the pen should act as a plain mouse, as process names separated by commas " +
             "(e.g. \"Illustrator, InDesign\"). \".exe\" is optional and case does not matter.\n" +
             "While one of these apps has focus, Windows Ink output is swapped for mouse output; " +
             "every other app keeps your normal pen settings.\n" +
             "Find a process name in Task Manager > Details.")]
    public string MouseApps { get; set; } = "Illustrator, InDesign";

    [Resolved]
    public IDriverDaemon? Daemon { get; set; }

    public bool Initialize()
    {
        if (Daemon == null)
        {
            Log.Write(FocusWatcher.LogGroup, "Driver daemon unavailable; Penopticon cannot run.", LogLevel.Error);
            return false;
        }

        FocusWatcher.Attach(Daemon, AppList.Parse(MouseApps));
        return true;
    }

    public void Dispose() => FocusWatcher.Detach();
}
