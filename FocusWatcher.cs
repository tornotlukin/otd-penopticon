using System.Diagnostics;
using OpenTabletDriver.Desktop;
using OpenTabletDriver.Desktop.Contracts;
using OpenTabletDriver.Plugin;

namespace Penopticon;

/// <summary>
/// Watches the foreground window and swaps the driver between the user's settings and a
/// mouse-mode copy of them while a listed app has focus.
///
/// The watcher is static because applying settings makes OpenTabletDriver dispose and
/// re-create every tool, including the <see cref="PenopticonTool"/> that started it. The
/// tool instances only attach and detach; the thread and switching state outlive them.
/// The thread stops once no tool has been attached for <see cref="DetachGraceMs"/>.
/// </summary>
internal static class FocusWatcher
{
    public const string LogGroup = "Penopticon";

    private const int PollMs = 50;
    private const int SettleMs = 150;
    private const int DetachGraceMs = 2000;
    private const string UxProcessPrefix = "OpenTabletDriver.UX";

    private static readonly object Gate = new();

    // Guarded by Gate.
    private static Thread? _thread;
    private static IDriverDaemon? _daemon;
    private static HashSet<string> _apps = new(StringComparer.OrdinalIgnoreCase);
    private static bool _attached;
    private static DateTime _detachedAtUtc;
    private static bool _selfApplying;
    private static bool _externalApply;

    // Watcher-thread state.
    private static Settings? _userSettings;   // restored when focus leaves a listed app
    private static Settings? _mouseSettings;  // non-null while mouse mode is active
    private static DateTime _mouseSinceUtc;
    private static bool _nothingToSwitch;     // user settings have no pen output mode

    public static void Attach(IDriverDaemon daemon, HashSet<string> apps)
    {
        lock (Gate)
        {
            if (!_apps.SetEquals(apps))
                Log.Write(LogGroup, apps.Count == 0
                    ? "No apps listed; the pen stays in its normal mode everywhere."
                    : $"Mouse mode for: {string.Join(", ", apps)}");

            _daemon = daemon;
            _apps = apps;
            _attached = true;

            // Any settings change not made by this watcher (Apply in the OTD window, a
            // preset binding, the console) re-creates tools too; flag it for review.
            if (!_selfApplying)
                _externalApply = true;

            if (_thread == null)
            {
                _thread = new Thread(Run) { IsBackground = true, Name = LogGroup };
                _thread.Start();
            }
        }
    }

    public static void Detach()
    {
        lock (Gate)
        {
            _attached = false;
            _detachedAtUtc = DateTime.UtcNow;
        }
    }

    private static void Run()
    {
        bool? pendingMouse = null;
        var pendingSinceUtc = DateTime.MinValue;

        while (true)
        {
            Thread.Sleep(PollMs);
            try
            {
                IDriverDaemon daemon;
                HashSet<string> apps;
                lock (Gate)
                {
                    if (!_attached)
                    {
                        if ((DateTime.UtcNow - _detachedAtUtc).TotalMilliseconds > DetachGraceMs)
                        {
                            Stop();
                            return;
                        }
                        continue;
                    }
                    if (_daemon == null)
                        continue;

                    daemon = _daemon;
                    apps = _apps;
                    if (_externalApply)
                    {
                        _externalApply = false;
                        AdoptExternalSettings(daemon);
                    }
                }

                var name = Win32.GetForegroundProcessName();
                if (name == null)
                    continue;

                bool wantMouse = apps.Contains(name) && !_nothingToSwitch;
                bool inMouse = _mouseSettings != null;
                if (wantMouse == inMouse)
                {
                    pendingMouse = null;
                    continue;
                }

                // Debounce so alt-tabbing past a listed app does not switch twice.
                if (pendingMouse != wantMouse)
                {
                    pendingMouse = wantMouse;
                    pendingSinceUtc = DateTime.UtcNow;
                    continue;
                }
                if ((DateTime.UtcNow - pendingSinceUtc).TotalMilliseconds < SettleMs)
                    continue;

                // Changing output mode mid-stroke could leave a button stuck down.
                if (Win32.AnyMouseButtonDown())
                    continue;

                lock (Gate)
                {
                    if (wantMouse)
                        EnterMouseMode(daemon, name);
                    else
                        ExitMouseMode(daemon, name);
                }
                pendingMouse = null;
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                Thread.Sleep(1000);
            }
        }
    }

    /// <summary>
    /// Settings were applied by someone else. If they are not our own mouse-mode settings
    /// being re-applied (the daemon does that when a tablet reconnects), they become the
    /// new user settings; mouse mode is re-entered from them if a listed app has focus.
    /// </summary>
    private static void AdoptExternalSettings(IDriverDaemon daemon)
    {
        var current = daemon.GetSettings().GetAwaiter().GetResult();
        if (_mouseSettings != null && ReferenceEquals(current, _mouseSettings))
            return;

        if (_mouseSettings != null)
            Log.Write(LogGroup, "Settings changed outside Penopticon; using them as the normal settings.");

        _mouseSettings = null;
        _userSettings = null;
        _nothingToSwitch = false;
    }

    private static void EnterMouseMode(IDriverDaemon daemon, string app)
    {
        var current = daemon.GetSettings().GetAwaiter().GetResult();
        var mouse = MouseSettingsBuilder.Build(current);
        if (mouse == null)
        {
            _nothingToSwitch = true;
            Log.Write(LogGroup, "Output mode is not Windows Ink; nothing to switch.");
            return;
        }

        if (!Apply(daemon, mouse))
        {
            Log.Write(LogGroup, "Driver rejected the mouse-mode settings; staying in the normal mode.", LogLevel.Error);
            _nothingToSwitch = true;
            return;
        }

        _userSettings = current;
        _mouseSettings = mouse;
        _mouseSinceUtc = DateTime.UtcNow;
        Log.Write(LogGroup, $"{app} focused: mouse mode");
    }

    private static void ExitMouseMode(IDriverDaemon daemon, string app)
    {
        var restore = _userSettings;
        _mouseSettings = null;
        _userSettings = null;
        if (restore == null)
            return;

        if (!Apply(daemon, restore))
            Log.Write(LogGroup, "Driver rejected the normal settings on restore.", LogLevel.Error);

        // An OTD window opened during mouse mode loaded the mouse-mode settings; make it
        // reload so a later Save in that window cannot persist them.
        if (UxStartedSince(_mouseSinceUtc))
            daemon.ForceResynchronize().GetAwaiter().GetResult();

        Log.Write(LogGroup, $"{app} focused: normal mode");
    }

    private static bool Apply(IDriverDaemon daemon, Settings settings)
    {
        _selfApplying = true;
        try
        {
            daemon.SetSettings(settings).GetAwaiter().GetResult();
        }
        finally
        {
            _selfApplying = false;
        }
        // On failure the daemon silently reverts to its last valid settings.
        return ReferenceEquals(daemon.GetSettings().GetAwaiter().GetResult(), settings);
    }

    private static bool UxStartedSince(DateTime sinceUtc)
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.ProcessName.StartsWith(UxProcessPrefix, StringComparison.OrdinalIgnoreCase)
                        && process.StartTime.ToUniversalTime() >= sinceUtc)
                        return true;
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
            }
        }
        return false;
    }

    private static void Stop()
    {
        // Settings were applied without this tool (it was disabled or removed). Whatever
        // was applied is now authoritative, so there is nothing to restore.
        _thread = null;
        _daemon = null;
        _mouseSettings = null;
        _userSettings = null;
        _nothingToSwitch = false;
        _apps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Log.Write(LogGroup, "Stopped.");
    }
}
