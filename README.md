# AutoAI — AI-Driven WPF UI Test Automation

Test a WPF application by chatting with an AI instead of writing classic automation code.
You type instructions like:

> Go to the Orders screen, click Load Orders and verify the grid shows 10 rows.

An **Azure OpenAI model deployment** (from your Azure AI Foundry project, connected with
just an **API key and endpoint**) decides which UI-automation tools to call, and a
**FlaUI**-based executor performs them against the live application — clicking buttons,
typing text, reading grids — then the agent reports PASSED/FAILED results back to you
in the chat.

## Architecture

```
┌─────────────────────┐         ┌──────────────────────────┐
│  AutoAI.Copilot     │  HTTPS  │  Azure OpenAI             │
│  (WPF chat window)  │◄───────►│  (your model deployment,  │
│                     │ api-key │   e.g. gpt-4o)            │
│  ┌───────────────┐  │         └──────────────────────────┘
│  │ AzureOpenAI    │ │   The model returns tool calls
│  │ AgentSession   │ │   (click_element, read_grid, ...);
│  └──────┬────────┘  │   the Copilot executes them locally
│         │           │   and sends the results back.
│  ┌──────▼────────┐  │
│  │ AutoAI.        │ │   UI Automation (UIA3)
│  │ Automation     │─┼────────────────────────┐
│  │ (FlaUI)        │ │                        ▼
│  └───────────────┘  │         ┌──────────────────────────┐
└─────────────────────┘         │  App under test           │
                                │  (AutoAI.SampleApp or     │
                                │   your own WPF app)       │
                                └──────────────────────────┘
```

| Project | What it is |
|---|---|
| `src/AutoAI.SampleApp` | An enterprise-style WPF app to test against: menu bar, status bar, five screens (Home, Orders, Customers, Order Entry, Reports), modal dialogs and message boxes, and the full common control set — see the coverage table below. Orders loads 10 rows after a simulated 1.5 s delay. |
| `src/AutoAI.Automation` | FlaUI wrapper exposing 29 automation tools covering every common WPF control type, plus context menus, keyboard shortcuts, multi-window apps, state waits and text assertions — with modal-dialog- and popup-aware element search. |
| `src/AutoAI.Agent` | Azure OpenAI client (API key + endpoint): system prompt, conversation history, tool definitions and the tool-call → execute → respond loop. |
| `src/AutoAI.Copilot` | The WPF chat window you talk to. Shows agent replies and a live log of every tool call. |

## Prerequisites

- Windows 10/11 (WPF + UI Automation are Windows-only)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An **Azure OpenAI model deployment** with a tool-capable model (e.g. gpt-4o) — created
  in the Azure AI Foundry portal or the Azure portal. No agent or tool configuration is
  needed on the Azure side; the Copilot supplies everything with each request.
- Its **endpoint and API key** — in the Azure portal under your Azure OpenAI resource →
  **Keys and Endpoint**, or in the Foundry portal on the model deployment's details page.
  No `az login`, tenant or role assignments required.

## Setup

1. **Configure the Copilot** — edit `src/AutoAI.Copilot/appsettings.json`:

   ```json
   {
     "AzureOpenAI": {
       "Endpoint": "https://<your-resource>.openai.azure.com/",
       "ApiKey": "<your-azure-openai-api-key>",
       "DeploymentName": "gpt-4o"
     },
     "SampleApp": {
       "Path": "..\\..\\..\\..\\AutoAI.SampleApp\\bin\\Debug\\net8.0-windows\\AutoAI.SampleApp.exe"
     }
   }
   ```

   `DeploymentName` is the name *you* gave the deployment, not the model name.
   Tip: keep the key out of git by putting it in `appsettings.local.json`
   (git-ignored, loaded on top of `appsettings.json`).

2. **Build everything**:

   ```powershell
   dotnet build AutoAI.sln
   ```

3. **Run the Copilot**:

   ```powershell
   dotnet run --project src/AutoAI.Copilot
   ```

## Example session

Click **Launch App** (or just ask the agent to launch it), then try:

```
You:   Go to the Orders screen, click Load Orders and verify the grid shows 10 rows.

 🔧 click_element {"automationId":"NavOrdersButton"}        → {"success":true,...}
 🔧 click_element {"automationId":"OrdersLoadButton"}       → {"success":true,...}
 🔧 wait_for_element {"automationId":"OrdersGrid", ...}     → {"found":true,...}
 🔧 verify_grid_row_count {"automationId":"OrdersGrid","expectedCount":10}
                                                            → {"passed":true,"expectedCount":10,"actualCount":10}

Agent: PASSED — I navigated to Orders, clicked Load Orders, waited for the data to load,
       and the grid contains exactly 10 rows (expected 10).
```

More things to try:

- `Go to Home, type "Sandeep" in the name box, click Greet and verify the greeting says "Hello, Sandeep!"`
- `Open Customers, search for "an" and tell me how many customers are shown.`
- `On Order Entry: create an order for Alice Johnson, 3 Laptops, high priority, express shipping, 15% discount, and verify the confirmation message.`
- `Load the orders, filter the grid to Shipped and verify it shows 6 rows.`
- `Select the first order row, click Delete Selected, confirm the Yes/No dialog with Yes, and verify 9 orders remain.`
- `In Reports, select Sales > Q1 Sales, enable detailed breakdown, generate the report and wait until it's ready.`
- `Open Tools > Settings, switch the theme to Dark and save; verify the status bar confirms it.`
- `Open Help > About and read me the dialog text, then close it.`
- `Right-click the orders grid and choose Export to CSV; verify the status text confirms the export.`
- `Select the second order row, double-click it, switch to the Order Details window, verify the customer name, then close it.`
- `Wait until the Load Orders button is enabled again, then verify the status text contains "Loaded".`
- `Take a screenshot of the app.`

## WPF control coverage

The sample app intentionally contains every control family an enterprise WPF system
typically has, each wired to a matching automation tool:

| Control | In the sample app | Agent tool |
|---|---|---|
| Button | everywhere | `click_element` |
| TextBox (single/multiline) | Home, Order Entry | `set_text` |
| PasswordBox | Settings dialog | `set_text` |
| ComboBox | Orders filter, Order Entry, Settings | `select_combo_item` |
| CheckBox / ToggleButton | Order Entry, Reports, Settings | `set_checkbox` |
| RadioButton (GroupBox) | Order Entry priority | `select_radio_button` |
| DatePicker | Order Entry delivery date | `set_text` (inner edit) |
| Slider | Order Entry discount | `set_slider_value` |
| DataGrid | Orders (2 grids), Customers | `read_grid`, `verify_grid_row_count`, `select_grid_row` |
| TabControl / TabItem | Orders (All / Completed) | `select_tab` |
| TreeView | Reports catalog | `select_tree_item` |
| ListBox / ListView | Reports results | `select_list_item` |
| Menu / MenuItem | File, View, Tools, Help | `select_menu_item` |
| Expander | Reports options | `click_element` (ExpandCollapse) |
| ProgressBar | Reports generation | `get_element_state` (rangeValue) |
| StatusBar | main window | `get_element_state`, `verify_element_text` |
| Modal Window / MessageBox | Settings, About, delete confirmation | found automatically by all tools; see `modalWindows` in `get_ui_tree` |
| ContextMenu (right-click) | Orders grid (View Details, Export to CSV) | `right_click_element`, then `click_element`; menu appears under `popup` in `get_ui_tree` |
| Non-modal child windows | Order Details (double-click a row) | `double_click_element`, `list_windows`, `switch_to_window` |
| Keyboard shortcuts | everywhere | `send_keys` (`CTRL+S`, `ENTER`, `F5`, …) |
| Async loads / state changes | Orders loading, report progress | `wait_for_element_state` (enabled/disabled/visible/hidden/text) |

## Pointing it at your own WPF application

1. Set `SampleApp:Path` in `appsettings.json` to your app's executable
   (or ask the agent to `attach_app` to an already running process).
2. The agent discovers your UI by calling `get_ui_tree`. It works best when your
   controls have `AutomationProperties.AutomationId` set; otherwise it falls back
   to visible names (button text, labels).
3. To explore what your app exposes to UI Automation, use
   [Accessibility Insights for Windows](https://accessibilityinsights.io/) or the
   Windows SDK's `inspect.exe`.

## The automation tools the agent can call

**Apps & windows**: `launch_app`, `attach_app`, `close_app`, `list_windows`,
`switch_to_window`
**Discovery**: `get_ui_tree`, `get_element_state`, `take_screenshot`
**Interaction**: `click_element`, `double_click_element`, `right_click_element`,
`set_text`, `send_keys`, `focus_element`, `scroll_element`
**Control-specific**: `select_combo_item`, `set_checkbox`, `select_radio_button`,
`select_list_item`, `select_tree_item`, `select_menu_item`, `set_slider_value`,
`select_tab`, `select_grid_row`
**Synchronization**: `wait_for_element`, `wait_for_element_state`
**Assertions**: `read_grid`, `verify_grid_row_count`, `verify_element_text`

All defined in `src/AutoAI.Automation/Tools/ToolCatalog.cs`. Add your own tool by
appending a `ToolSpec` there and a handler in `AutomationToolExecutor.ExecuteCore`.

Element search automatically covers **open modal dialogs, message boxes and popups**
(context menus, dropdowns) — they are separate top-level windows in UI Automation,
searched before the active window — so the agent can fill dialogs, confirm message
boxes and click context-menu items with the same tools. When the app opens non-modal
child windows, `switch_to_window` retargets every tool at that window.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `401/403` on connect | Wrong or expired API key — copy key 1 or 2 from **Keys and Endpoint** on your Azure OpenAI resource into `AzureOpenAI:ApiKey`. |
| `Deployment ... was not found` | Check `AzureOpenAI:Endpoint` (must look like `https://<resource>.openai.azure.com/`) and `AzureOpenAI:DeploymentName` (the deployment name, not the model name). |
| `Executable not found` on Launch App | Build the sample app first (`dotnet build`), or fix `SampleApp:Path`. |
| Agent can't find elements | Make sure the right screen is visible; ask it to "get the UI tree" — and add `AutomationProperties.AutomationId` to your controls. |
| Model never calls tools | Use a tool-capable chat model (gpt-4o, gpt-4o-mini, gpt-4.1, …); completions-only or embedding deployments won't work. |

## Current limitations

- Replies are not streamed (the full answer appears when the turn finishes).
- Conversation history grows over the session; restart the Copilot for a fresh context.
- One application under test at a time.
- Screenshots are saved to `%TEMP%\AutoAI` and returned as file paths; they are not
  sent to the model (use a vision-enabled flow if you need that).
