using System.Collections.ObjectModel;
using System.Text.Json;
using AutoAI.Agent;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoAI.Copilot.ViewModels;

public sealed partial class ChatViewModel : ObservableObject
{
    private readonly FoundryAgentSession _agent;
    private readonly IToolExecutor _executor;
    private bool _connected;

    public ChatViewModel(FoundryAgentSession agent, IToolExecutor executor)
    {
        _agent = agent;
        _executor = executor;
        Items.Add(new ChatItemViewModel(ChatItemKind.Assistant,
            "Hi! I'm your AI test copilot. Click 'Launch App' (or just ask me to), then give me test " +
            "instructions like: \"Go to the Orders screen, click Load Orders and verify the grid shows 10 rows.\""));
    }

    public ObservableCollection<ChatItemViewModel> Items { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _inputText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyCanExecuteChangedFor(nameof(LaunchAppCommand))]
    private bool _isBusy;

    public string StatusText => IsBusy ? "Working..." : "Ready";

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(StatusText));

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = InputText.Trim();
        if (text.Length == 0)
        {
            return;
        }

        InputText = string.Empty;
        Items.Add(new ChatItemViewModel(ChatItemKind.User, text));
        IsBusy = true;
        try
        {
            await EnsureConnectedAsync();

            // Progress<T> marshals reports back to the UI thread automatically.
            var toolLog = new Progress<ToolLogEntry>(entry => Items.Add(new ChatItemViewModel(
                entry.Kind == ToolLogKind.Call ? ChatItemKind.ToolCall : ChatItemKind.ToolResult,
                $"{entry.ToolName} {Truncate(entry.PayloadJson, 600)}")));

            var reply = await _agent.SendMessageAsync(text, toolLog);
            Items.Add(new ChatItemViewModel(ChatItemKind.Assistant, reply));
        }
        catch (Exception ex)
        {
            Items.Add(new ChatItemViewModel(ChatItemKind.Error, ex.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSend() => !IsBusy && !string.IsNullOrWhiteSpace(InputText);

    [RelayCommand(CanExecute = nameof(CanLaunchApp))]
    private async Task LaunchAppAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _executor.ExecuteAsync("launch_app", "{}");
            var kind = IsSuccess(result) ? ChatItemKind.ToolResult : ChatItemKind.Error;
            Items.Add(new ChatItemViewModel(kind, $"launch_app {result}"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanLaunchApp() => !IsBusy;

    private async Task EnsureConnectedAsync()
    {
        if (_connected)
        {
            return;
        }
        var status = await _agent.ConnectAsync();
        Items.Add(new ChatItemViewModel(ChatItemKind.ToolResult, status));
        _connected = true;
    }

    private static bool IsSuccess(string resultJson)
    {
        try
        {
            using var document = JsonDocument.Parse(resultJson);
            return !document.RootElement.TryGetProperty("success", out var success)
                || success.ValueKind != JsonValueKind.False;
        }
        catch
        {
            return true;
        }
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "…";
}
