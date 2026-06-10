using System.Diagnostics;
using System.Text.Json;
using AutoAI.Agent;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Input;

namespace AutoAI.Automation.Tools;

/// <summary>
/// Executes the agent's tool calls with FlaUI. All UIA work runs on threadpool (MTA)
/// threads — never call this from the copilot's UI thread synchronously. Calls are
/// serialized so a batch of tool calls from one agent turn executes in order.
/// </summary>
public sealed class AutomationToolExecutor(string defaultAppPath) : IToolExecutor, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly AutomationSession _session = new();
    private ElementFinder? _finder;

    public IReadOnlyList<ToolSpec> Tools => ToolCatalog.Tools;

    public async Task<string> ExecuteAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() => ExecuteCore(toolName, argumentsJson), cancellationToken);
        }
        catch (Exception ex)
        {
            return Error(ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    private string ExecuteCore(string toolName, string argumentsJson)
    {
        using var document = JsonDocument.Parse(
            string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
        var args = document.RootElement;

        return toolName switch
        {
            "launch_app" => LaunchApp(args),
            "attach_app" => AttachApp(args),
            "close_app" => CloseApp(),
            "get_ui_tree" => GetUiTree(args),
            "click_element" => ClickElement(args),
            "set_text" => SetText(args),
            "select_tab" => SelectTab(args),
            "read_grid" => ReadGrid(args),
            "verify_grid_row_count" => VerifyGridRowCount(args),
            "get_element_state" => GetElementState(args),
            "wait_for_element" => WaitForElement(args),
            "take_screenshot" => TakeScreenshot(args),
            _ => Json(new { success = false, error = $"Unknown tool '{toolName}'." }),
        };
    }

    private string LaunchApp(JsonElement args)
    {
        var path = GetString(args, "path") ?? defaultAppPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return Json(new { success = false, error = "No path given and no default app path configured (SampleApp:Path)." });
        }

        var fullPath = Path.GetFullPath(path, AppContext.BaseDirectory);
        if (!File.Exists(fullPath))
        {
            return Json(new { success = false, error = $"Executable not found: {fullPath}. Build the app under test first." });
        }

        var window = _session.Launch(fullPath);
        _finder = new ElementFinder(_session);
        return Json(new { success = true, processId = _session.App!.ProcessId, mainWindowTitle = window.Title });
    }

    private string AttachApp(JsonElement args)
    {
        var processName = GetString(args, "processName");
        var processId = GetInt(args, "processId");

        Window window;
        if (processId is not null)
        {
            window = _session.AttachByProcessId(processId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(processName))
        {
            window = _session.AttachByProcessName(processName);
        }
        else
        {
            return Json(new { success = false, error = "Provide processName or processId." });
        }

        _finder = new ElementFinder(_session);
        return Json(new { success = true, processId = _session.App!.ProcessId, mainWindowTitle = window.Title });
    }

    private string CloseApp()
    {
        _session.CloseApp();
        _finder = null;
        return Json(new { success = true });
    }

    private string GetUiTree(JsonElement args)
    {
        var maxDepth = GetInt(args, "maxDepth") ?? 12;
        var serializer = new UiTreeSerializer(maxDepth);
        return serializer.Serialize(_session.GetMainWindow());
    }

    private string ClickElement(JsonElement args)
    {
        var element = Find(args);
        var invoke = element.Patterns.Invoke.PatternOrDefault;
        if (invoke is not null)
        {
            invoke.Invoke();
        }
        else
        {
            element.Click();
        }
        Wait.UntilInputIsProcessed();
        return Json(new
        {
            success = true,
            clicked = new { automationId = element.AutomationId, name = element.Name },
        });
    }

    private string SetText(JsonElement args)
    {
        var automationId = GetString(args, "automationId")
            ?? throw new ArgumentException("automationId is required.");
        var text = GetString(args, "text")
            ?? throw new ArgumentException("text is required.");

        var element = Finder.Find(automationId, null, null);
        try
        {
            element.AsTextBox().Text = text;
        }
        catch
        {
            // Fallback for controls without the Value pattern: type it.
            element.Focus();
            Keyboard.Type(text);
        }
        return Json(new { success = true });
    }

    private string SelectTab(JsonElement args)
    {
        var element = Find(args);
        var selectionItem = element.Patterns.SelectionItem.PatternOrDefault;
        if (selectionItem is not null)
        {
            selectionItem.Select();
        }
        else
        {
            element.AsTabItem().Select();
        }
        return Json(new { success = true, selectedTab = element.Name });
    }

    private string ReadGrid(JsonElement args)
    {
        var automationId = GetString(args, "automationId")
            ?? throw new ArgumentException("automationId is required.");
        var maxRows = GetInt(args, "maxRows") ?? 50;

        var grid = Finder.Find(automationId, null, null).AsGrid();
        var columns = grid.Header?.Columns.Select(c => c.Name).ToArray() ?? [];
        var rowCount = grid.RowCount;
        var rows = grid.Rows
            .Take(maxRows)
            .Select(row => row.Cells.Select(GetCellText).ToArray())
            .ToArray();

        return Json(new { success = true, columns, rows, rowCount });
    }

    private string VerifyGridRowCount(JsonElement args)
    {
        var automationId = GetString(args, "automationId")
            ?? throw new ArgumentException("automationId is required.");
        var expectedCount = GetInt(args, "expectedCount")
            ?? throw new ArgumentException("expectedCount is required.");

        var grid = Finder.Find(automationId, null, null).AsGrid();
        var actualCount = grid.RowCount;
        return Json(new { passed = actualCount == expectedCount, expectedCount, actualCount });
    }

    private string GetElementState(JsonElement args)
    {
        var (automationId, name, _) = GetCriteria(args);
        var element = Finder.TryFind(automationId, name, null, TimeSpan.FromSeconds(1));
        if (element is null)
        {
            return Json(new { exists = false, criteria = ElementFinder.Describe(automationId, name, null) });
        }

        return Json(new
        {
            exists = true,
            automationId = element.AutomationId,
            name = element.Name,
            controlType = element.ControlType.ToString(),
            isEnabled = element.IsEnabled,
            isOffscreen = element.IsOffscreen,
            text = UiTreeSerializer.TryGetValue(element),
            boundingRectangle = element.BoundingRectangle.ToString(),
        });
    }

    private string WaitForElement(JsonElement args)
    {
        var (automationId, name, _) = GetCriteria(args);
        var timeoutMs = GetInt(args, "timeoutMs") ?? 5000;

        var stopwatch = Stopwatch.StartNew();
        var element = Finder.TryFind(automationId, name, null, TimeSpan.FromMilliseconds(timeoutMs));
        return Json(new { found = element is not null, elapsedMs = stopwatch.ElapsedMilliseconds });
    }

    private string TakeScreenshot(JsonElement args)
    {
        var fullScreen = args.TryGetProperty("fullScreen", out var p) && p.ValueKind == JsonValueKind.True;

        var directory = Path.Combine(Path.GetTempPath(), "AutoAI");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"shot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");

        using var image = fullScreen
            ? Capture.Screen()
            : Capture.Element(_session.GetMainWindow());
        image.ToFile(path);

        return Json(new { success = true, path });
    }

    private ElementFinder Finder => _finder
        ?? throw new InvalidOperationException("No application is attached. Call launch_app or attach_app first.");

    private AutomationElement Find(JsonElement args)
    {
        var (automationId, name, controlType) = GetCriteria(args);
        return Finder.Find(automationId, name, controlType);
    }

    private static (string? AutomationId, string? Name, string? ControlType) GetCriteria(JsonElement args)
        => (GetString(args, "automationId"), GetString(args, "name"), GetString(args, "controlType"));

    private static string GetCellText(GridCell cell)
        => UiTreeSerializer.TryGetValue(cell) ?? cell.Name ?? string.Empty;

    private static string? GetString(JsonElement args, string property)
        => args.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInt(JsonElement args, string property)
        => args.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;

    private static string Json(object payload) => JsonSerializer.Serialize(payload);

    private static string Error(Exception ex)
        => JsonSerializer.Serialize(new { success = false, error = $"{ex.GetType().Name}: {ex.Message}" });

    public void Dispose()
    {
        _session.Dispose();
        _gate.Dispose();
    }
}
