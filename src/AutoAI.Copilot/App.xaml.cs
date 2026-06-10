using System.Windows;
using AutoAI.Agent;
using AutoAI.Automation.Tools;
using AutoAI.Copilot.ViewModels;
using Microsoft.Extensions.Configuration;

namespace AutoAI.Copilot;

public partial class App : Application
{
    private AutomationToolExecutor? _executor;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .Build();

        var options = configuration.Get<CopilotOptions>() ?? new CopilotOptions();

        _executor = new AutomationToolExecutor(options.SampleApp.Path);
        var agentSession = new FoundryAgentSession(options.Foundry, _executor);
        var viewModel = new ChatViewModel(agentSession, _executor);

        var window = new MainWindow { DataContext = viewModel };
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _executor?.Dispose();
        base.OnExit(e);
    }
}
