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
            "take_screenshot",
            "Save a PNG screenshot of the main window (or the full screen) and return the file path.",
            """
            {"type":"object","properties":{
              "fullScreen":{"type":"boolean","description":"true for the whole screen, false (default) for the app's main window."}
            },"required":[]}
            """),
    ];
}
