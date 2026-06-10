using System.Diagnostics;
using System.Text.Json;
using AutoAI.Agent;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

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
            "select_combo_item" => SelectComboItem(args),
            "set_checkbox" => SetCheckBox(args),
            "select_radio_button" => SelectRadioButton(args),
            "select_list_item" => SelectListItem(args),
            "select_tree_item" => SelectTreeItem(args),
            "select_menu_item" => SelectMenuItem(args),
            "set_slider_value" => SetSliderValue(args),
            "select_grid_row" => SelectGridRow(args),
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
        // Pattern preference: Invoke (buttons, menu items) -> Toggle (check boxes) ->
        // ExpandCollapse (expanders) -> real mouse click for everything else.
        var invoke = element.Patterns.Invoke.PatternOrDefault;
        var toggle = element.Patterns.Toggle.PatternOrDefault;
        var expandCollapse = element.Patterns.ExpandCollapse.PatternOrDefault;
        if (invoke is not null)
        {
            invoke.Invoke();
        }
        else if (toggle is not null)
        {
            toggle.Toggle();
        }
        else if (expandCollapse is not null)
        {
            var state = expandCollapse.ExpandCollapseState.ValueOrDefault;
            if (state == ExpandCollapseState.Collapsed)
            {
                expandCollapse.Expand();
            }
            else
            {
                expandCollapse.Collapse();
            }
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
            // Composite controls (e.g. DatePicker) hold their editable part in an inner
            // Edit element; try that, then fall back to replacing the text via keyboard.
            var innerEdit = element.FindFirstDescendant(
                _session.Automation.ConditionFactory.ByControlType(ControlType.Edit));
            if (innerEdit is not null)
            {
                innerEdit.AsTextBox().Enter(text);
            }
            else
            {
                element.Focus();
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                Keyboard.Type(text);
            }
        }
        Wait.UntilInputIsProcessed();
        return Json(new { success = true });
    }

    private string SelectComboItem(JsonElement args)
    {
        var automationId = GetString(args, "automationId")
            ?? throw new ArgumentException("automationId is required.");
        var item = GetString(args, "item")
            ?? throw new ArgumentException("item is required.");

        var comboBox = Finder.Find(automationId, null, null).AsComboBox();
        comboBox.Expand();
        var selected = comboBox.Items.FirstOrDefault(i =>
            string.Equals(i.Text, item, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(i.Name, item, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            var available = comboBox.Items.Select(i => i.Text).ToArray();
            comboBox.Collapse();
            return Json(new { success = false, error = $"Item '{item}' not found.", availableItems = available });
        }
        selected.Select();
        comboBox.Collapse();
        Wait.UntilInputIsProcessed();
        return Json(new { success = true, selectedItem = selected.Text });
    }

    private string SetCheckBox(JsonElement args)
    {
        var isChecked = GetBool(args, "checked")
            ?? throw new ArgumentException("checked is required.");
        var element = Find(args);
        element.AsCheckBox().IsChecked = isChecked;
        return Json(new { success = true, isChecked });
    }

    private string SelectRadioButton(JsonElement args)
    {
        var element = Find(args);
        element.AsRadioButton().IsChecked = true;
        return Json(new { success = true, selected = element.Name });
    }

    private string SelectListItem(JsonElement args)
    {
        var automationId = GetString(args, "automationId")
            ?? throw new ArgumentException("automationId is required.");
        var itemText = GetString(args, "item")
            ?? throw new ArgumentException("item is required.");

        var listBox = Finder.Find(automationId, null, null).AsListBox();
        var item = listBox.Items.FirstOrDefault(i =>
            string.Equals(i.Text, itemText, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(i.Name, itemText, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return Json(new
            {
                success = false,
                error = $"List item '{itemText}' not found.",
                availableItems = listBox.Items.Select(i => i.Text).ToArray(),
            });
        }
        item.Select();
        return Json(new { success = true, selectedItem = item.Text });
    }

    private string SelectTreeItem(JsonElement args)
    {
        var automationId = GetString(args, "automationId")
            ?? throw new ArgumentException("automationId is required.");
        var path = GetString(args, "path")
            ?? throw new ArgumentException("path is required.");

        var tree = Finder.Find(automationId, null, null).AsTree();
        var parts = path.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var candidates = tree.Items;
        TreeItem? current = null;
        foreach (var part in parts)
        {
            current = candidates.FirstOrDefault(i =>
                string.Equals(i.Name, part, StringComparison.OrdinalIgnoreCase));
            if (current is null)
            {
                return Json(new
                {
                    success = false,
                    error = $"Tree item '{part}' not found in path '{path}'.",
                    availableItems = candidates.Select(i => i.Name).ToArray(),
                });
            }
            current.Expand();
            candidates = current.Items;
        }
        current!.Select();
        return Json(new { success = true, selectedItem = current.Name, path });
    }

    private string SelectMenuItem(JsonElement args)
    {
        var path = GetString(args, "path")
            ?? throw new ArgumentException("path is required.");

        var window = _session.GetMainWindow();
        var menuElement = window.FindFirstDescendant(
            _session.Automation.ConditionFactory.ByControlType(ControlType.Menu))
            ?? throw new ElementNotFoundException("controlType='Menu' (the window has no menu bar)");

        var parts = path.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var candidates = menuElement.AsMenu().Items;
        MenuItem? current = null;
        foreach (var part in parts)
        {
            current = candidates.FirstOrDefault(i =>
                string.Equals(i.Name, part, StringComparison.OrdinalIgnoreCase));
            if (current is null)
            {
                return Json(new
                {
                    success = false,
                    error = $"Menu item '{part}' not found in path '{path}'.",
                    availableItems = candidates.Select(i => i.Name).ToArray(),
                });
            }
            candidates = current.Items;
        }
        current!.Invoke();
        Wait.UntilInputIsProcessed();
        return Json(new { success = true, invoked = path });
    }

    private string SetSliderValue(JsonElement args)
    {
        var automationId = GetString(args, "automationId")
            ?? throw new ArgumentException("automationId is required.");
        var value = GetDouble(args, "value")
            ?? throw new ArgumentException("value is required.");

        var slider = Finder.Find(automationId, null, null).AsSlider();
        slider.Value = value;
        return Json(new { success = true, value = slider.Value });
    }

    private string SelectGridRow(JsonElement args)
    {
        var automationId = GetString(args, "automationId")
            ?? throw new ArgumentException("automationId is required.");
        var rowIndex = GetInt(args, "rowIndex")
            ?? throw new ArgumentException("rowIndex is required.");

        var grid = Finder.Find(automationId, null, null).AsGrid();
        var rows = grid.Rows;
        if (rowIndex < 0 || rowIndex >= rows.Length)
        {
            return Json(new
            {
                success = false,
                error = $"rowIndex {rowIndex} is out of range (grid has {rows.Length} rows).",
            });
        }
        var row = rows[rowIndex];
        row.Select();
        return Json(new { success = true, rowIndex, cells = row.Cells.Select(GetCellText).ToArray() });
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

        var toggleState = UiTreeSerializer.TryGetToggleState(element);
        return Json(new
        {
            exists = true,
            automationId = element.AutomationId,
            name = element.Name,
            controlType = element.ControlType.ToString(),
            isEnabled = element.IsEnabled,
            isOffscreen = element.IsOffscreen,
            text = UiTreeSerializer.TryGetValue(element),
            isChecked = toggleState is null ? (bool?)null : toggleState == ToggleState.On,
            rangeValue = UiTreeSerializer.TryGetRangeValue(element),
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

    private static double? GetDouble(JsonElement args, string property)
        => args.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;

    private static bool? GetBool(JsonElement args, string property)
        => args.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
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
