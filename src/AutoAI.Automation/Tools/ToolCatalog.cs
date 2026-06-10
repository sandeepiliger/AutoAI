using AutoAI.Agent;

namespace AutoAI.Automation.Tools;

/// <summary>
/// The tool definitions (names, descriptions, JSON parameter schemas) supplied to the
/// model on every request. Descriptions are written for the model, not for humans.
/// </summary>
public static class ToolCatalog
{
    public static IReadOnlyList<ToolSpec> Tools { get; } =
    [
        new ToolSpec(
            "launch_app",
            "Launch the application under test and attach to its main window. " +
            "If 'path' is omitted, the configured default application path is used.",
            """
            {"type":"object","properties":{
              "path":{"type":"string","description":"Optional absolute path to the executable. Omit to use the configured app."}
            },"required":[]}
            """),

        new ToolSpec(
            "attach_app",
            "Attach to an already running application by process name or process id.",
            """
            {"type":"object","properties":{
              "processName":{"type":"string","description":"Process name, e.g. 'AutoAI.SampleApp'."},
              "processId":{"type":"integer","description":"Process id."}
            },"required":[]}
            """),

        new ToolSpec(
            "close_app",
            "Close the application under test and end the automation session.",
            """{"type":"object","properties":{},"required":[]}"""),

        new ToolSpec(
            "get_ui_tree",
            "Return the visible UI automation tree of the main window as JSON " +
            "(controlType, automationId, name, value, checked, selected, children). Open modal " +
            "dialogs and message boxes are included under 'modalWindows'. Call this to discover " +
            "which elements exist and their automation IDs before interacting.",
            """
            {"type":"object","properties":{
              "maxDepth":{"type":"integer","description":"Maximum tree depth, default 12."}
            },"required":[]}
            """),

        new ToolSpec(
            "click_element",
            "Click an element (button, menu item, expander, nav item, dialog button, etc.). " +
            "Provide at least one of automationId, name or controlType. Elements in open modal " +
            "dialogs and message boxes are found too (e.g. name='Yes' on a confirmation box). " +
            "Uses the UIA Invoke/Toggle/ExpandCollapse pattern when available, otherwise a real " +
            "mouse click.",
            """
            {"type":"object","properties":{
              "automationId":{"type":"string","description":"AutomationId of the element (preferred)."},
              "name":{"type":"string","description":"Visible name/label of the element."},
              "controlType":{"type":"string","description":"UIA control type, e.g. Button, TabItem."}
            },"required":[]}
            """),

        new ToolSpec(
            "set_text",
            "Set the text of a text box identified by automationId, replacing its current content.",
            """
            {"type":"object","properties":{
              "automationId":{"type":"string","description":"AutomationId of the text box."},
              "text":{"type":"string","description":"The text to enter."}
            },"required":["automationId","text"]}
            """),

        new ToolSpec(
            "select_tab",
            "Select a tab item by automationId or name (UIA SelectionItem pattern).",
            """
            {"type":"object","properties":{
              "automationId":{"type":"string","description":"AutomationId of the tab item."},
              "name":{"type":"string","description":"Visible header text of the tab item."}
            },"required":[]}
            """),

        new ToolSpec(
            "read_grid",
            "Read a data grid: column headers, cell values of up to maxRows rows, and the exact " +
            "total row count. Use this to verify data that was loaded into a grid.",
            """
            {"type":"object","properties":{
              "automationId":{"type":"string","description":"AutomationId of the grid."},
              "maxRows":{"type":"integer","description":"Maximum number of rows to return, default 50."}
            },"required":["automationId"]}
            """),

        new ToolSpec(
            "verify_grid_row_count",
            "Assert that a grid contains exactly the expected number of data rows. " +
            "Returns passed=true/false with expected and actual counts.",
            """
            {"type":"object","properties":{
              "automationId":{"type":"string","description":"AutomationId of the grid."},
              "expectedCount":{"type":"integer","description":"The expected number of rows."}
            },"required":["automationId","expectedCount"]}
            """),

        new ToolSpec(
            "get_element_state",
            "Check whether an element exists right now and return its state " +
            "(enabled, offscreen, text). exists=false is a normal result, not an error. " +
            "Useful for verifying labels, statuses and button states.",
            """
            {"type":"object","properties":{
              "automationId":{"type":"string","description":"AutomationId of the element (preferred)."},
              "name":{"type":"string","description":"Visible name of the element."}
            },"required":[]}
            """),

        new ToolSpec(
            "wait_for_element",
            "Wait until an element appears, up to timeoutMs. Use after actions that trigger " +
            "loading or navigation, before verifying results.",
            """
            {"type":"object","properties":{
              "automationId":{"type":"string","description":"AutomationId of the element (preferred)."},
              "name":{"type":"string","description":"Visible name of the element."},
              "timeoutMs":{"type":"integer","description":"Maximum wait in milliseconds, default 5000."}
            },"required":[]}
            """),

        new ToolSpec(
            "select_combo_item",
            "Select an item in a ComboBox (dropdown) by its visible text.",
            """
            {"type":"object","properties":{
              "automationId":{"type":"string","description":"AutomationId of the ComboBox."},
              "item":{"type":"string","description":"Visible text of the item to select."}
            },"required":["automationId","item"]}
            """),

        new ToolSpec(
            "set_checkbox",
            "Check or uncheck a CheckBox or ToggleButton (UIA Toggle pattern).",
            """
            {"type":"object","properties":{
              "automationId":{"type":"string","description":"AutomationId of the check box (preferred)."},
              "name":{"type":"string","description":"Visible label of the check box."},
              "checked":{"type":"boolean","description":"true to check, false to uncheck."}
            },"required":["checked"]}
            """),

        new ToolSpec(
            "select_radio_button",
            "Select a RadioButton.",
            """
            {"type":"object","properties":{
              "automationId":{"type":"string","description":"AutomationId of the radio button (preferred)."},
              "name":{"type":"string","description":"Visible label of the radio button."}
            },"required":[]}
            """),

        new ToolSpec(
            "select_list_item",
            "Select an item in a ListBox or ListView by its visible text.",
            """
            {"type":"object","properties":{
              "automationId":{"type":"string","description":"AutomationId of the list."},
              "item":{"type":"string","description":"Visible text of the item to select."}
            },"required":["automationId","item"]}
            """),

        new ToolSpec(
            "select_tree_item",
            "Expand a TreeView along a path of item names and select the final item. " +
            "Example path: 'Sales/Q1 Sales'.",
            """
            {"type":"object","properties":{
              "automationId":{"type":"string","description":"AutomationId of the tree view."},
              "path":{"type":"string","description":"Item names from root to target, separated by '/'."}
            },"required":["automationId","path"]}
            """),

        new ToolSpec(
            "select_menu_item",
            "Open the window's menu bar and invoke a menu item along a path of menu names. " +
            "Example path: 'File/New Order' or 'Tools/Settings...'.",
            """
            {"type":"object","properties":{
              "path":{"type":"string","description":"Menu names from the top-level menu to the item, separated by '/'."}
            },"required":["path"]}
            """),

        new ToolSpec(
            "set_slider_value",
            "Set the value of a Slider (UIA RangeValue pattern).",
            """
            {"type":"object","properties":{
              "automationId":{"type":"string","description":"AutomationId of the slider."},
              "value":{"type":"number","description":"The value to set (clamped to the slider's range)."}
            },"required":["automationId","value"]}
            """),

        new ToolSpec(
            "select_grid_row",
            "Select a row of a data grid by zero-based index (e.g. before clicking a " +
            "'Delete Selected' style button). Returns the selected row's cell values.",
            """
            {"type":"object","properties":{
              "automationId":{"type":"string","description":"AutomationId of the grid."},
              "rowIndex":{"type":"integer","description":"Zero-based row index."}
            },"required":["automationId","rowIndex"]}
            """),

        new ToolSpec(
            "double_click_element",
            "Double-click an element (e.g. a grid row to open its detail view). " +
            "Provide at least one of automationId, name or controlType.",
            """
            {"type":"object","properties":{
              "automationId":{"type":"string","description":"AutomationId of the element (preferred)."},
              "name":{"type":"string","description":"Visible name of the element."},
              "controlType":{"type":"string","description":"UIA control type, e.g. DataItem."}
            },"required":[]}
            """),

        new ToolSpec(
            "right_click_element",
            "Right-click an element to open its context menu. The opened menu appears under " +
            "'popup' in get_ui_tree and its items can be clicked with click_element by name.",
            """
            {"type":"object","properties":{
              "automationId":{"type":"string","description":"AutomationId of the element (preferred)."},
              "name":{"type":"string","description":"Visible name of the element."},
              "controlType":{"type":"string","description":"UIA control type."}
            },"required":[]}
            """),

        new ToolSpec(
            "send_keys",
            "Send a keyboard shortcut or special key to the application, e.g. 'CTRL+S', " +
            "'ALT+F4', 'ENTER', 'TAB', 'ESCAPE', 'F5', 'CTRL+SHIFT+P', 'DELETE', 'DOWN'. " +
            "Optionally focus an element first. For typing text into a field use set_text instead.",
            """
            {"type":"object","properties":{
              "keys":{"type":"string","description":"Keys joined with '+', e.g. 'CTRL+S' or a single key like 'ENTER'."},
              "automationId":{"type":"string","description":"Optional element to focus before sending the keys."}
            },"required":["keys"]}
            """),

        new ToolSpec(
            "focus_element",
            "Set keyboard focus to an element.",
            """
            {"type":"object","properties":{
              "automationId":{"type":"string","description":"AutomationId of the element (preferred)."},
              "name":{"type":"string","description":"Visible name of the element."}
            },"required":[]}
            """),

        new ToolSpec(
            "scroll_element",
            "Scroll a scrollable container (grid, list, panel) in a direction. Uses the UIA " +
            "Scroll pattern when available, otherwise the mouse wheel over the element.",
            """
            {"type":"object","properties":{
              "automationId":{"type":"string","description":"AutomationId of the scrollable element."},
              "direction":{"type":"string","enum":["up","down","left","right"],"description":"Scroll direction."},
              "amount":{"type":"integer","description":"Number of scroll increments, default 3."}
            },"required":["automationId","direction"]}
            """),

        new ToolSpec(
            "wait_for_element_state",
            "Wait until an element reaches a state: 'enabled', 'disabled', 'visible', 'hidden', " +
            "'text_equals' or 'text_contains' (the last two compare against 'text'). Use this to " +
            "wait for loads to finish, buttons to re-enable, or status text to change.",
            """
            {"type":"object","properties":{
              "automationId":{"type":"string","description":"AutomationId of the element (preferred)."},
              "name":{"type":"string","description":"Visible name of the element."},
              "condition":{"type":"string","enum":["enabled","disabled","visible","hidden","text_equals","text_contains"],"description":"The state to wait for."},
              "text":{"type":"string","description":"Expected text for text_equals / text_contains."},
              "timeoutMs":{"type":"integer","description":"Maximum wait in milliseconds, default 10000."}
            },"required":["condition"]}
            """),

        new ToolSpec(
            "verify_element_text",
            "Assert the text of an element (label, status, text box). Returns passed=true/false " +
            "with expected and actual text.",
            """
            {"type":"object","properties":{
              "automationId":{"type":"string","description":"AutomationId of the element (preferred)."},
              "name":{"type":"string","description":"Visible name of the element."},
              "expectedText":{"type":"string","description":"The expected text."},
              "comparison":{"type":"string","enum":["equals","contains"],"description":"Comparison mode, default 'equals'. Case-insensitive."}
            },"required":["expectedText"]}
            """),

        new ToolSpec(
            "list_windows",
            "List all top-level windows of the application under test (useful when the app " +
            "opens additional non-modal windows).",
            """{"type":"object","properties":{},"required":[]}"""),

        new ToolSpec(
            "switch_to_window",
            "Make another top-level window of the application the active target for all tools " +
            "(matched by title substring). Switch back by passing the main window's title.",
            """
            {"type":"object","properties":{
              "title":{"type":"string","description":"Part of the window title to switch to (case-insensitive)."}
            },"required":["title"]}
            """),

        new ToolSpec(
            "take_screenshot",
            "Save a PNG screenshot of the main window (or the full screen) and return the file path.",
            """
            {"type":"object","properties":{
              "fullScreen":{"type":"boolean","description":"true for the whole screen, false (default) for the app's main window."}
            },"required":[]}
            """),
    ];
}
