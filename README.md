# AutoAI — AI-Driven WPF UI Test Automation

Test a WPF application by chatting with an AI instead of writing classic automation code.
You type instructions like:

> Go to the Orders screen, click Load Orders and verify the grid shows 10 rows.

An **Azure AI Foundry agent** (your existing agent) decides which UI-automation tools to
call, and a **FlaUI**-based executor performs them against the live application — clicking
buttons, typing text, reading grids — then the agent reports PASSED/FAILED results back
to you in the chat.

## Architecture

```
┌─────────────────────┐         ┌──────────────────────────┐
│  AutoAI.Copilot     │  HTTPS  │  Azure AI Foundry         │
│  (WPF chat window)  │◄───────►│  Agent Service            │
│                     │         │  (your existing agent)    │
│  ┌───────────────┐  │         └──────────────────────────┘
│  │ FoundryAgent   │ │   The agent returns tool calls
│  │ Session        │ │   (click_element, read_grid, ...);
│  └──────┬────────┘  │   the Copilot executes them locally
│         │           │   and submits the results back.
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
| `src/AutoAI.SampleApp` | A small WPF app to test against: Home / Orders / Customers screens, buttons, text boxes and DataGrids. Orders loads 10 rows after a simulated 1.5 s delay. |
| `src/AutoAI.Automation` | FlaUI wrapper exposing 12 automation tools (launch/attach, get_ui_tree, click_element, set_text, read_grid, verify_grid_row_count, wait_for_element, screenshot, …). |
| `src/AutoAI.Agent` | Azure AI Foundry client: persistent thread, per-run tool definitions, the RequiresAction → execute → SubmitToolOutputs loop. |
| `src/AutoAI.Copilot` | The WPF chat window you talk to. Shows agent replies and a live log of every tool call. |

## Prerequisites

- Windows 10/11 (WPF + UI Automation are Windows-only)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) (`az`)
- An **Azure AI Foundry project with an existing agent** using a tool-capable model
  (e.g. gpt-4o). You do **not** need to configure any tools on the agent — the Copilot
  supplies the automation tool definitions with every run.
- Your signed-in account needs the **Azure AI User** role on the Foundry project.

## Setup

1. **Sign in to Azure** (the Copilot uses `DefaultAzureCredential`):

   ```powershell
   az login            # add --tenant <tenant-id> if you have several
   ```

2. **Configure the Copilot** — edit `src/AutoAI.Copilot/appsettings.json`:

   ```json
   {
     "Foundry": {
       "ProjectEndpoint": "https://<your-resource>.services.ai.azure.com/api/projects/<your-project>",
       "AgentId": "asst_xxxxxxxxxxxxxxxx",
       "TenantId": null
     },
     "SampleApp": {
       "Path": "..\\..\\..\\..\\AutoAI.SampleApp\\bin\\Debug\\net8.0-windows\\AutoAI.SampleApp.exe"
     }
   }
   ```

   Find the endpoint and agent id in the Azure AI Foundry portal under your project's
   **Agents** page. You can also put local values in `appsettings.local.json`
   (git-ignored, loaded on top of `appsettings.json`).

3. **Build everything**:

   ```powershell
   dotnet build AutoAI.sln
   ```

4. **Run the Copilot**:

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
- `Clear the orders grid and confirm the status text says "Not loaded".`
- `Take a screenshot of the app.`

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

`launch_app`, `attach_app`, `close_app`, `get_ui_tree`, `click_element`, `set_text`,
`select_tab`, `read_grid`, `verify_grid_row_count`, `get_element_state`,
`wait_for_element`, `take_screenshot` — defined in
`src/AutoAI.Automation/Tools/ToolCatalog.cs`. Add your own tool by appending a
`ToolSpec` there and a handler in `AutomationToolExecutor.ExecuteCore`.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `401/403` on connect | `az login` again; ensure your account has the **Azure AI User** role on the Foundry project. Multi-tenant accounts: set `Foundry:TenantId`. |
| `Agent ... was not found` | Check `Foundry:AgentId` and `Foundry:ProjectEndpoint` (must be the *project* endpoint, ending in `/api/projects/<name>`). |
| `Executable not found` on Launch App | Build the sample app first (`dotnet build`), or fix `SampleApp:Path`. |
| Agent can't find elements | Make sure the right screen is visible; ask it to "get the UI tree" — and add `AutomationProperties.AutomationId` to your controls. |
| Build errors in `FoundryAgentSession` | The `Azure.AI.Agents.Persistent` SDK is young and method names occasionally shift between versions. Pin to the version in `AutoAI.Agent.csproj` (1.1.0) or adjust the calls to your version. |

## Current limitations

- Run status is polled (no streaming of partial agent text).
- One application under test at a time.
- Screenshots are saved to `%TEMP%\AutoAI` and returned as file paths; they are not
  sent to the model (use a vision-enabled flow if you need that).
