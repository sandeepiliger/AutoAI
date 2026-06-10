using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace AutoAI.Automation;

/// <summary>
/// Owns one <see cref="UIA3Automation"/> instance (expensive COM init, never per-call)
/// and the <see cref="Application"/> handle of the app under test for the lifetime
/// of a launch/attach.
/// </summary>
public sealed class AutomationSession : IDisposable
{
    public UIA3Automation Automation { get; } = new();

    public Application? App { get; private set; }

    public bool IsAttached => App is { HasExited: false };

    private Window? _activeWindow;

    public Window Launch(string path)
    {
        DetachCurrentApp();
        App = Application.Launch(path);
        return GetMainWindow();
    }

    public Window AttachByProcessName(string processName)
    {
        DetachCurrentApp();
        App = Application.Attach(processName);
        return GetMainWindow();
    }

    public Window AttachByProcessId(int processId)
    {
        DetachCurrentApp();
        App = Application.Attach(processId);
        return GetMainWindow();
    }

    public Window GetMainWindow()
    {
        if (App is null)
        {
            throw new InvalidOperationException(
                "No application is attached. Call launch_app or attach_app first.");
        }

        var window = App.GetMainWindow(Automation, TimeSpan.FromSeconds(10));
        return window ?? throw new InvalidOperationException(
            "The application's main window did not appear within 10 seconds.");
    }

    /// <summary>
    /// The window the tools currently operate on: the window chosen via
    /// switch_to_window if it is still alive, otherwise the main window.
    /// </summary>
    public Window GetActiveWindow()
    {
        if (_activeWindow is not null)
        {
            try
            {
                if (_activeWindow.IsAvailable)
                {
                    return _activeWindow;
                }
            }
            catch
            {
                // Window died; fall back to the main window.
            }
            _activeWindow = null;
        }
        return GetMainWindow();
    }

    public IReadOnlyList<Window> GetAllWindows()
    {
        if (App is null)
        {
            throw new InvalidOperationException(
                "No application is attached. Call launch_app or attach_app first.");
        }
        return App.GetAllTopLevelWindows(Automation);
    }

    public Window SwitchToWindow(string titleContains)
    {
        var window = GetAllWindows().FirstOrDefault(w =>
            (w.Title ?? string.Empty).Contains(titleContains, StringComparison.OrdinalIgnoreCase));
        if (window is null)
        {
            throw new InvalidOperationException(
                $"No top-level window with a title containing '{titleContains}' was found.");
        }
        _activeWindow = window;
        try
        {
            window.Focus();
        }
        catch
        {
            // Focusing is best-effort; the window may not accept focus right now.
        }
        return window;
    }

    /// <summary>
    /// Windows to search for elements, in interaction-priority order: the active
    /// window's popup (open context menus / dropdowns), its modal dialogs (including
    /// message boxes) — both are separate top-level windows, not descendants — then
    /// the active window itself, and finally the main window as a fallback.
    /// </summary>
    public IReadOnlyList<Window> GetSearchRoots()
    {
        var active = GetActiveWindow();
        var roots = new List<Window>();
        try
        {
            if (active.Popup is { } popup)
            {
                roots.Add(popup);
            }
        }
        catch
        {
            // No popup open.
        }
        try
        {
            roots.AddRange(active.ModalWindows);
        }
        catch
        {
            // Modal window enumeration can fail transiently while a dialog opens/closes.
        }
        roots.Add(active);

        try
        {
            var main = GetMainWindow();
            if (!main.Equals(active))
            {
                roots.Add(main);
            }
        }
        catch
        {
            // Active window is all we have.
        }
        return roots;
    }

    public void CloseApp()
    {
        if (App is not null)
        {
            App.Close();
            App.Dispose();
            App = null;
        }
        _activeWindow = null;
    }

    private void DetachCurrentApp()
    {
        App?.Dispose();
        App = null;
        _activeWindow = null;
    }

    public void Dispose()
    {
        App?.Dispose();
        Automation.Dispose();
    }
}
