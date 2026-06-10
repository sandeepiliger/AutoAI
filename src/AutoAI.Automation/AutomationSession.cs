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
    /// Windows to search for elements, in interaction-priority order: open modal dialogs
    /// (including message boxes) first — they're separate top-level windows, not
    /// descendants of the main window — then the main window itself.
    /// </summary>
    public IReadOnlyList<Window> GetSearchRoots()
    {
        var mainWindow = GetMainWindow();
        var roots = new List<Window>();
        try
        {
            roots.AddRange(mainWindow.ModalWindows);
        }
        catch
        {
            // Modal window enumeration can fail transiently while a dialog opens/closes.
        }
        roots.Add(mainWindow);
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
    }

    private void DetachCurrentApp()
    {
        App?.Dispose();
        App = null;
    }

    public void Dispose()
    {
        App?.Dispose();
        Automation.Dispose();
    }
}
