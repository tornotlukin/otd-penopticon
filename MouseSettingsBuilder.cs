using Newtonsoft.Json;
using OpenTabletDriver.Desktop;
using OpenTabletDriver.Desktop.Binding;
using OpenTabletDriver.Desktop.Output;
using OpenTabletDriver.Desktop.Profiles;
using OpenTabletDriver.Desktop.Reflection;

namespace Penopticon;

/// <summary>
/// Derives a mouse-mode copy of the driver settings: Windows Ink output modes become the
/// built-in Absolute / Relative modes, and pen-channel bindings become mouse buttons.
/// Everything else (areas, filters, tools, keyboard bindings) is kept as-is.
/// </summary>
internal static class MouseSettingsBuilder
{
    private const string WinInkAbsoluteMode = "VoiDPlugins.OutputMode.WinInkAbsoluteMode";
    private const string WinInkRelativeMode = "VoiDPlugins.OutputMode.WinInkRelativeMode";
    private const string WinInkButtonHandler = "VoiDPlugins.OutputMode.WindowsInkButtonHandler";
    private const string AdaptiveBinding = "OpenTabletDriver.Desktop.Binding.AdaptiveBinding";
    private const string ButtonaliciousClick = "Buttonalicious.MouseClickBinding";

    public static bool IsPenOutputMode(string? path) =>
        path is WinInkAbsoluteMode or WinInkRelativeMode;

    public static bool UsesPenOutputMode(Settings settings) =>
        settings.Profiles.Any(p => IsPenOutputMode(p.OutputMode?.Path));

    /// <summary>
    /// Returns a converted deep copy, or null when no profile uses a pen output mode
    /// (the settings already deliver mouse input and there is nothing to switch).
    /// </summary>
    public static Settings? Build(Settings source)
    {
        if (!UsesPenOutputMode(source))
            return null;

        var copy = Clone(source);
        foreach (var profile in copy.Profiles)
        {
            var path = profile.OutputMode?.Path;
            if (!IsPenOutputMode(path))
                continue;

            profile.OutputMode = new PluginSettingStore(
                path == WinInkRelativeMode ? typeof(RelativeMode) : typeof(AbsoluteMode));
            ConvertBindings(profile.BindingSettings);
        }
        return copy;
    }

    private static Settings Clone(Settings source) =>
        JsonConvert.DeserializeObject<Settings>(JsonConvert.SerializeObject(source))
        ?? throw new InvalidOperationException("Failed to copy driver settings");

    private static void ConvertBindings(BindingSettings? b)
    {
        if (b == null)
            return;

        b.TipButton = Convert(b.TipButton);
        b.EraserButton = Convert(b.EraserButton);
        b.MouseScrollUp = Convert(b.MouseScrollUp);
        b.MouseScrollDown = Convert(b.MouseScrollDown);
        ConvertAll(b.PenButtons);
        ConvertAll(b.AuxButtons);
        ConvertAll(b.MouseButtons);

        foreach (var wheel in b.WheelBindings ?? Enumerable.Empty<WheelBindingSettings>())
        {
            wheel.ClockwiseRotation = Convert(wheel.ClockwiseRotation);
            wheel.CounterClockwiseRotation = Convert(wheel.CounterClockwiseRotation);
            ConvertAll(wheel.WheelButtons);
        }
    }

    private static void ConvertAll(PluginSettingStoreCollection? stores)
    {
        if (stores == null)
            return;
        for (int i = 0; i < stores.Count; i++)
            stores[i] = Convert(stores[i])!;
    }

    private static PluginSettingStore? Convert(PluginSettingStore? store)
    {
        switch (store?.Path)
        {
            case WinInkButtonHandler:
            {
                var button = GetString(store, "Button");
                bool isContact = button == "Pen Tip" || (button?.StartsWith("Eraser", StringComparison.Ordinal) ?? false);
                return MouseButton(isContact ? "Left" : "Right");
            }
            case AdaptiveBinding:
                return MouseButton(GetString(store, "Binding") switch
                {
                    "Button 1" => "Left",
                    "Button 3" => "Middle",
                    _ => "Right"
                });
            case ButtonaliciousClick when GetString(store, "Button") == "Pen Tap":
            {
                // Pen Tap synthesizes pen-channel input, which is exactly what mouse mode
                // must avoid. Keep the binding and its timing, but click the left button.
                var copy = JsonConvert.DeserializeObject<PluginSettingStore>(JsonConvert.SerializeObject(store))!;
                copy["Button"].SetValue("Left");
                return copy;
            }
            default:
                return store;
        }
    }

    private static PluginSettingStore MouseButton(string button)
    {
        var store = new PluginSettingStore(typeof(MouseBinding));
        store["Button"].SetValue(button);
        return store;
    }

    private static string? GetString(PluginSettingStore store, string property)
    {
        var setting = store.Settings?.FirstOrDefault(s => s.Property == property);
        return setting is { HasValue: true } ? setting.GetValue<string>() : null;
    }
}
