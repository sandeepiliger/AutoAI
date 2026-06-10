using System.Diagnostics;
using System.Text.Json;
using AutoAI.Agent;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
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
            "double_click_element" => DoubleClickElement(args),
            "right_click_element" => RightClickElement(args),
            "send_keys" => SendKeys(args),
            "focus_element" => FocusElement(args),
            "scroll_element" => ScrollElement(args),
            "wait_for_element_state" => WaitForElementState(args),
            "verify_element_text" => VerifyElementText(args),
            "list_windows" => ListWindows(),
            "switch_to_window" => SwitchToWindow(args),
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
        return serializer.Serialize(_session.GetActiveWindow());
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

    private string DoubleClickElement(JsonElement args)
    {
        var element = Find(args);
        element.DoubleClick();
        Wait.UntilInputIsProcessed();
        return Json(new
        {
            success = true,
            doubleClicked = new { automationId = element.AutomationId, name = element.Name },
        });
    }

    private string RightClickElement(JsonElement args)
    {
        var element = Find(args);
        element.RightClick();
        Wait.UntilInputIsProcessed();
        return Json(new
        {
            success = true,
            rightClicked = new { automationId = element.AutomationId, name = element.Name },
            hint = "Call get_ui_tree to see the opened context menu under 'popup'.",
        });
    }

    private string SendKeys(JsonElement args)
    {
        var keys = GetString(args, "keys")
            ?? throw new ArgumentException("keys is required.");
        var automationId = GetString(args, "automationId");

        if (automationId is not null)
        {
            Finder.Find(automationId, null, null).Focus();
        }

        var virtualKeys = keys
            .Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseKey)
            .ToArray();
        if (virtualKeys.Length == 0)
        {
            throw new ArgumentException("keys must contain at least one key.");
        }

        if (virtualKeys.Length == 1)
        {
            Keyboard.Type(virtualKeys[0]);
        }
        else
        {
            Keyboard.TypeSimultaneously(virtualKeys);
        }
        Wait.UntilInputIsProcessed();
        return Json(new { success = true, sent = keys });
    }

    private string FocusElement(JsonElement args)
    {
        var element = Find(args);
        element.Focus();
        return Json(new { success = true, focused = new { automationId = element.AutomationId, name = element.Name } });
    }

    private string ScrollElement(JsonElement args)
    {
        var automationId = GetString(args, "automationId")
            ?? throw new ArgumentException("automationId is required.");
        var direction = (GetString(args, "direction") ?? "down").ToLowerInvariant();
        var amount = Math.Clamp(GetInt(args, "amount") ?? 3, 1, 50);
        if (direction is not ("up" or "down" or "left" or "right"))
        {
            throw new ArgumentException("direction must be one of: up, down, left, right.");
        }

        var element = Finder.Find(automationId, null, null);
        var scroll = element.Patterns.Scroll.PatternOrDefault;
        if (scroll is not null)
        {
            var horizontal = direction switch
            {
                "left" => ScrollAmount.SmallDecrement,
                "right" => ScrollAmount.SmallIncrement,
                _ => ScrollAmount.NoAmount,
            };
            var vertical = direction switch
            {
                "up" => ScrollAmount.SmallDecrement,
                "down" => ScrollAmount.SmallIncrement,
                _ => ScrollAmount.NoAmount,
            };
            for (var i = 0; i < amount; i++)
            {
                scroll.Scroll(horizontal, vertical);
            }
        }
        else
        {
            Mouse.MoveTo(element.GetClickablePoint());
            if (direction is "up" or "down")
            {
                Mouse.Scroll(direction == "up" ? amount : -amount);
            }
            else
            {
                Mouse.HorizontalScroll(direction == "right" ? amount : -amount);
            }
        }
        return Json(new { success = true, scrolled = direction, amount });
    }

    private string WaitForElementState(JsonElement args)
    {
        var (automationId, name, _) = GetCriteria(args);
        var condition = GetString(args, "condition")
            ?? throw new ArgumentException("condition is required.");
        var expectedText = GetString(args, "text");
        var timeoutMs = GetInt(args, "timeoutMs") ?? 10000;
        if (condition is "text_equals" or "text_contains" && expectedText is null)
        {
            throw new ArgumentException($"'text' is required for condition '{condition}'.");
        }

        var stopwatch = Stopwatch.StartNew();
        string? lastText = null;
        var satisfied = Retry.WhileFalse(
            () =>
            {
                var element = Finder.TryFind(automationId, name, null, TimeSpan.FromMilliseconds(100));
                if (element is null)
                {
                    return condition == "hidden";
                }
                lastText = UiTreeSerializer.TryGetValue(element) ?? element.Name;
                return condition switch
                {
                    "enabled" => element.IsEnabled,
                    "disabled" => !element.IsEnabled,
                    "visible" => !element.IsOffscreen,
                    "hidden" => element.IsOffscreen,
                    "text_equals" => string.Equals(lastText, expectedText, StringComparison.OrdinalIgnoreCase),
                    "text_contains" => lastText?.Contains(expectedText!, StringComparison.OrdinalIgnoreCase) == true,
                    _ => throw new ArgumentException($"Unknown condition '{condition}'."),
                };
            },
            timeout: TimeSpan.FromMilliseconds(timeoutMs),
            interval: TimeSpan.FromMilliseconds(250),
            throwOnTimeout: false,
            ignoreException: false).Result;

        return Json(new { satisfied, condition, elapsedMs = stopwatch.ElapsedMilliseconds, lastObservedText = lastText });
    }

    private string VerifyElementText(JsonElement args)
    {
        var (automationId, name, _) = GetCriteria(args);
        var expectedText = GetString(args, "expectedText")
            ?? throw new ArgumentException("expectedText is required.");
        var comparison = GetString(args, "comparison") ?? "equals";

        var element = Finder.Find(automationId, name, null);
        var actualText = UiTreeSerializer.TryGetValue(element) ?? element.Name ?? string.Empty;
        var passed = comparison == "contains"
            ? actualText.Contains(expectedText, StringComparison.OrdinalIgnoreCase)
            : string.Equals(actualText, expectedText, StringComparison.OrdinalIgnoreCase);

        return Json(new { passed, expectedText, actualText, comparison });
    }

    private string ListWindows()
    {
        var active = _session.GetActiveWindow();
        var windows = _session.GetAllWindows()
            .Select(w => new
            {
                title = w.Title,
                automationId = w.AutomationId,
                isActive = w.Equals(active),
            })
            .ToArray();
        return Json(new { success = true, windows });
    }

    private string SwitchToWindow(JsonElement args)
    {
        var title = GetString(args, "title")
            ?? throw new ArgumentException("title is required.");
        var window = _session.SwitchToWindow(title);
        return Json(new { success = true, activeWindow = window.Title });
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
            : Capture.Element(_session.GetActiveWindow());
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

    private static readonly Dictionary<string, VirtualKeyShort> KeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CTRL"] = VirtualKeyShort.CONTROL,
        ["CONTROL"] = VirtualKeyShort.CONTROL,
        ["ALT"] = VirtualKeyShort.ALT,
        ["SHIFT"] = VirtualKeyShort.SHIFT,
        ["WIN"] = VirtualKeyShort.LWIN,
        ["ENTER"] = VirtualKeyShort.ENTER,
        ["RETURN"] = VirtualKeyShort.RETURN,
        ["TAB"] = VirtualKeyShort.TAB,
        ["ESC"] = VirtualKeyShort.ESCAPE,
        ["ESCAPE"] = VirtualKeyShort.ESCAPE,
        ["SPACE"] = VirtualKeyShort.SPACE,
        ["BACKSPACE"] = VirtualKeyShort.BACK,
        ["DELETE"] = VirtualKeyShort.DELETE,
        ["DEL"] = VirtualKeyShort.DELETE,
        ["INSERT"] = VirtualKeyShort.INSERT,
        ["HOME"] = VirtualKeyShort.HOME,
        ["END"] = VirtualKeyShort.END,
        ["PAGEUP"] = VirtualKeyShort.PRIOR,
        ["PAGEDOWN"] = VirtualKeyShort.NEXT,
        ["UP"] = VirtualKeyShort.UP,
        ["DOWN"] = VirtualKeyShort.DOWN,
        ["LEFT"] = VirtualKeyShort.LEFT,
        ["RIGHT"] = VirtualKeyShort.RIGHT,
        ["F1"] = VirtualKeyShort.F1,
        ["F2"] = VirtualKeyShort.F2,
        ["F3"] = VirtualKeyShort.F3,
        ["F4"] = VirtualKeyShort.F4,
        ["F5"] = VirtualKeyShort.F5,
        ["F6"] = VirtualKeyShort.F6,
        ["F7"] = VirtualKeyShort.F7,
        ["F8"] = VirtualKeyShort.F8,
        ["F9"] = VirtualKeyShort.F9,
        ["F10"] = VirtualKeyShort.F10,
        ["F11"] = VirtualKeyShort.F11,
        ["F12"] = VirtualKeyShort.F12,
    };

    private static VirtualKeyShort ParseKey(string token)
    {
        if (KeyMap.TryGetValue(token, out var key))
        {
            return key;
        }
        if (token.Length == 1 && char.IsLetterOrDigit(token[0]))
        {
            return Enum.Parse<VirtualKeyShort>($"KEY_{char.ToUpperInvariant(token[0])}");
        }
        throw new ArgumentException(
            $"Unknown key '{token}'. Use modifiers CTRL/ALT/SHIFT/WIN, named keys like " +
            "ENTER, TAB, ESCAPE, DELETE, F1-F12, arrow keys, or single letters/digits.");
    }

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
