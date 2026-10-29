using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GensokyoLauncher.Services;
using GensokyoLauncher.ViewModels;
using GensokyoLauncher.Views;

namespace GensokyoLauncher;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var paths = new AppPaths();
            var settings = new JsonSettingsService(paths);
            var discovery = new GameDiscoveryService();
            var launcher = new ProcessGameLauncher();
            var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            var releaseClient = new GitHubThcrapReleaseClient(httpClient);
            var bootstrap = new ThcrapBootstrapService(releaseClient, paths);
            var thpracReleaseClient = new GitHubThpracReleaseClient(httpClient);
            var thpracBootstrap = new ThpracBootstrapService(thpracReleaseClient, paths);
            var nekoProject = new NekoProjectBootstrapService(httpClient, paths);
            var configurations = new ThcrapRunConfigurationService(paths);
            var thcrapLauncher = new ThcrapGameLauncher(paths, new WindowsGameWindowManager(),
                new ThpracGameAttacher(paths), new Th06VpatchService());
            var repositories = new ThcrapRepositoryService(httpClient, paths);
            var viewModel = new MainWindowViewModel(settings, discovery, launcher, bootstrap, thpracBootstrap, nekoProject, configurations,
                thcrapLauncher, repositories, repositories);
            desktop.MainWindow = new MainWindow { DataContext = viewModel };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
