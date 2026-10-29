using System.Collections.ObjectModel;
using GensokyoLauncher.Models;
using GensokyoLauncher.Services;

namespace GensokyoLauncher.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IGameDiscoveryService _discovery;
    private readonly IGameLauncher _launcher;
    private readonly IThcrapBootstrapService _bootstrap;
    private readonly IThpracBootstrapService _thpracBootstrap;
    private readonly INekoProjectBootstrapService _nekoProject;
    private readonly IThcrapRunConfigurationService _configurations;
    private readonly IThcrapGameLauncher _thcrapLauncher;
    private readonly IThcrapRepositoryService _repositories;
    private readonly IThcrapPatchSynchronizer _patches;
    private AppSettings _settings = new();
    private bool _initialized;
    private int _selectedPage;
    private bool _isScanning;
    private bool _isThcrapBusy;
    private bool _isThpracBusy;
    private string _scanStatus = "Choose the folder containing your Touhou games.";
    private string _thcrapStatus = "Not installed";
    private string _nekoProjectStatus = "Not installed";
    private string _thpracStatus = "Not installed";
    private Task<BootstrapResult>? _thpracSetupTask;
    private string? _globalError;

    public MainWindowViewModel(
        ISettingsService settingsService,
        IGameDiscoveryService discovery,
        IGameLauncher launcher,
        IThcrapBootstrapService bootstrap,
        IThpracBootstrapService thpracBootstrap,
        INekoProjectBootstrapService nekoProject,
        IThcrapRunConfigurationService configurations,
        IThcrapGameLauncher thcrapLauncher,
        IThcrapRepositoryService repositories,
        IThcrapPatchSynchronizer patches)
    {
        _settingsService = settingsService;
        _discovery = discovery;
        _launcher = launcher;
        _bootstrap = bootstrap;
        _thpracBootstrap = thpracBootstrap;
        _nekoProject = nekoProject;
        _configurations = configurations;
        _thcrapLauncher = thcrapLauncher;
        _repositories = repositories;
        _patches = patches;
        ShowGamesCommand = new RelayCommand(() => SelectPage(0));
        ShowSettingsCommand = new RelayCommand(() => SelectPage(1));
        ShowAboutCommand = new RelayCommand(() => SelectPage(2));
        RescanCommand = new AsyncCommand(ScanAsync, () => HasGamesRoot && !IsScanning);
        RetryThcrapCommand = new AsyncCommand(BootstrapThcrapAsync, () => HasGamesRoot && !IsThcrapBusy);
        RetryThpracCommand = new AsyncCommand(RetryThpracAsync, () => HasGamesRoot && !IsThpracBusy);
    }

    public ObservableCollection<GameItemViewModel> Games { get; } = [];
    public ObservableCollection<GameGroupViewModel> GameGroups { get; } = [];
    public RelayCommand ShowGamesCommand { get; }
    public RelayCommand ShowSettingsCommand { get; }
    public RelayCommand ShowAboutCommand { get; }
    public AsyncCommand RescanCommand { get; }
    public AsyncCommand RetryThcrapCommand { get; }
    public AsyncCommand RetryThpracCommand { get; }
    public int SelectedPage => _selectedPage;
    public bool IsGamesSelected => _selectedPage == 0;
    public bool IsSettingsSelected => _selectedPage == 1;
    public bool IsAboutSelected => _selectedPage == 2;
    public string AppVersion => $"Version {typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString(3) ?? "unknown"}";
    public bool IsScanning { get => _isScanning; private set { if (SetProperty(ref _isScanning, value)) RescanCommand.RaiseCanExecuteChanged(); } }
    public bool IsThcrapBusy { get => _isThcrapBusy; private set { if (SetProperty(ref _isThcrapBusy, value)) RetryThcrapCommand.RaiseCanExecuteChanged(); } }
    public bool IsThpracBusy { get => _isThpracBusy; private set { if (SetProperty(ref _isThpracBusy, value)) RetryThpracCommand.RaiseCanExecuteChanged(); } }
    public string ScanStatus { get => _scanStatus; private set => SetProperty(ref _scanStatus, value); }
    public string ThcrapStatus { get => _thcrapStatus; private set => SetProperty(ref _thcrapStatus, value); }
    public string NekoProjectStatus { get => _nekoProjectStatus; private set => SetProperty(ref _nekoProjectStatus, value); }
    public string ThpracStatus { get => _thpracStatus; private set => SetProperty(ref _thpracStatus, value); }
    public string? GlobalError { get => _globalError; private set { if (SetProperty(ref _globalError, value)) OnPropertyChanged(nameof(HasGlobalError)); } }
    public bool HasGlobalError => !string.IsNullOrWhiteSpace(GlobalError);
    public string GamesRoot => _settings.GamesRoot ?? "No folder selected";
    public bool HasGamesRoot => !string.IsNullOrWhiteSpace(_settings.GamesRoot) && Directory.Exists(_settings.GamesRoot);
    public bool HasGames => Games.Count > 0;
    public bool ShowEmptyState => !IsScanning && !HasGames;
    public int SelectedViewMode
    {
        get => _settings.GameViewMode;
        set
        {
            var normalized = value == 1 ? 1 : 0;
            if (_settings.GameViewMode == normalized) return;
            _settings.GameViewMode = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCardView));
            OnPropertyChanged(nameof(IsHorizontalCardView));
            _ = _settingsService.SaveAsync(_settings);
        }
    }
    public bool IsCardView => SelectedViewMode == 0;
    public bool IsHorizontalCardView => SelectedViewMode == 1;
    public int SelectedBackdropMode
    {
        get => _settings.AppearanceBackdropMode;
        set
        {
            var normalized = value is >= 0 and <= 2 ? value : 0;
            if (_settings.AppearanceBackdropMode == normalized) return;
            _settings.AppearanceBackdropMode = normalized;
            OnPropertyChanged();
            _ = _settingsService.SaveAsync(_settings);
        }
    }
    public int SelectedTabAnimationMode
    {
        get => _settings.TabAnimationMode;
        set
        {
            var normalized = value is >= 0 and <= 2 ? value : 0;
            if (_settings.TabAnimationMode == normalized) return;
            _settings.TabAnimationMode = normalized;
            OnPropertyChanged();
            _ = _settingsService.SaveAsync(_settings);
        }
    }
    public int SelectedGroupingMode
    {
        get => _settings.GameGroupingMode;
        set
        {
            var normalized = value == 1 ? 1 : 0;
            if (_settings.GameGroupingMode == normalized) return;
            _settings.GameGroupingMode = normalized;
            OnPropertyChanged();
            ApplyGrouping();
            _ = _settingsService.SaveAsync(_settings);
        }
    }
    public string WineCommand
    {
        get => _settings.WineCommand;
        set
        {
            if (_settings.WineCommand == value) return;
            _settings.WineCommand = value;
            OnPropertyChanged();
        }
    }
    public IReadOnlyList<LaunchProfile> LaunchProfiles => _settings.LaunchProfiles;
    public LaunchProfile GlobalDefaultProfile => _settings.ResolveProfile();
    public LaunchProfile PreferredProfile(string gameId) => _settings.ResolveProfile(gameId);
    public Func<DiscoveredGame, Task<ProfileSelectionResult?>>? SelectProfileAsync { get; set; }
    public Func<DiscoveredGame, Task<string?>>? SelectPc98DiskAsync { get; set; }
    public Func<DiscoveredGame, Task<string?>>? ShowGamePropertiesAsync { get; set; }
    public Func<string, ILaunchProgressSession>? BeginLaunchProgress { get; set; }

    public async Task SaveProfilesAsync(List<LaunchProfile> profiles, string? defaultId)
    {
        _settings.LaunchProfiles = profiles;
        _settings.GlobalDefaultProfileId = defaultId;
        _settings.NormalizeProfiles();
        OnPropertyChanged(nameof(LaunchProfiles));
        OnPropertyChanged(nameof(GlobalDefaultProfile));
        await _settingsService.SaveAsync(_settings);
    }

    public async Task<IReadOnlyList<OfficialLanguage>> GetOfficialLanguagesAsync()
    {
        try { return await _repositories.GetOfficialLanguagesAsync(); }
        catch (Exception exception)
        {
            ThcrapStatus = $"Could not load official languages: {exception.Message}";
            return _settings.LaunchProfiles
                .Where(profile => profile.LanguagePatchId is not null)
                .Select(profile => profile.LanguagePatchId!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(id => new OfficialLanguage(id, id))
                .ToArray();
        }
    }

    public async Task<bool> InitializeAsync()
    {
        if (_initialized) return !HasGamesRoot;
        _initialized = true;
        _settings = await _settingsService.LoadAsync();
        _settings.NormalizeProfiles();
        await _settingsService.SaveAsync(_settings);
        OnSettingsChanged();
        if (!HasGamesRoot) return true;
        await ScanAsync();
        _ = BootstrapThcrapAsync();
        _ = BootstrapThpracAsync();
        _ = BootstrapNekoProjectAsync();
        return false;
    }

    public async Task SetGamesRootAsync(string path)
    {
        _settings.GamesRoot = Path.GetFullPath(path);
        GlobalError = null;
        OnSettingsChanged();
        await _settingsService.SaveAsync(_settings);
        await ScanAsync();
        _ = BootstrapThcrapAsync();
        _ = BootstrapThpracAsync();
        _ = BootstrapNekoProjectAsync();
    }

    public async Task SaveSettingsAsync()
    {
        _settings.WineCommand = string.IsNullOrWhiteSpace(_settings.WineCommand) ? "wine" : _settings.WineCommand.Trim();
        OnPropertyChanged(nameof(WineCommand));
        await _settingsService.SaveAsync(_settings);
    }

    private async Task ScanAsync()
    {
        if (!HasGamesRoot) return;
        IsScanning = true;
        GlobalError = null;
        ScanStatus = "Scanning for Touhou games…";
        OnPropertyChanged(nameof(ShowEmptyState));
        try
        {
            var discovered = await _discovery.DiscoverAsync(_settings.GamesRoot!);
            Games.Clear();
            foreach (var discoveredGame in discovered)
            {
                var game = _settings.GameExecutableOverrides.TryGetValue(discoveredGame.Definition.Id, out var executable)
                           && File.Exists(executable)
                    ? discoveredGame with { ExecutablePath = Path.GetFullPath(executable) }
                    : discoveredGame;
                GameItemViewModel? item = null;
                item = new GameItemViewModel(game, async () =>
                {
                    item!.Error = null;
                    item.Status = null;
                    ILaunchProgressSession? launchProgress = null;
                    try
                    {
                        if (game.Definition.Category == GameCategory.Pc98)
                        {
                            var diskImages = game.AvailableDiskImages?.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                                ?? [game.ExecutablePath];
                            var diskImage = diskImages.Length > 1 && SelectPc98DiskAsync is not null
                                ? await SelectPc98DiskAsync(game)
                                : game.ExecutablePath;
                            if (diskImage is null) return;
                            var emulator = _nekoProject.FindEmulator() ?? throw new InvalidOperationException("Neko Project II is not ready yet. Check Settings and retry.");
                            var launchArgument = _nekoProject.PrepareLaunchArgument(emulator, diskImage);
                            await _launcher.LaunchAsync(emulator, _settings.WineCommand, launchArgument,
                                workingDirectory: Path.GetDirectoryName(diskImage));
                        }
                        else
                        {
                            var selection = SelectProfileAsync is null ? null : await SelectProfileAsync(game);
                            if (selection is null) return;
                            launchProgress = BeginLaunchProgress?.Invoke("Preparing " + selection.Profile.Name + "…");
                            if (selection.RememberForGame)
                            {
                                _settings.GameProfileAssignments[game.Definition.Id] = selection.Profile.Id;
                                await _settingsService.SaveAsync(_settings);
                            }
                            if (selection.Profile.UseThprac)
                                await EnsureThpracReadyAsync();
                            IReadOnlyList<string> archives = [];
                            if (selection.Profile.LanguagePatchId is { } languagePatchId)
                            {
                                var syncProgress = new Progress<ThcrapSyncProgress>(progress =>
                                {
                                    ThcrapStatus = progress.Message;
                                    launchProgress?.Report(progress.Message);
                                });
                                var synchronized = await _patches.SyncForGameAsync(
                                    "thpatch", languagePatchId, game.Definition.Id, syncProgress);
                                archives = synchronized.ArchivePaths;
                            }
                            ThcrapStatus = "Preparing " + selection.Profile.Name + "…";
                            launchProgress?.Report(ThcrapStatus);
                            var config = await _configurations.GenerateAsync(selection.Profile, game.Definition.Id, archives);
                            var progress = new Progress<string>(message =>
                            {
                                if (message.Contains("thprac", StringComparison.OrdinalIgnoreCase)) ThpracStatus = message;
                                else ThcrapStatus = message;
                                launchProgress?.Report(message);
                            });
                            await _thcrapLauncher.LaunchAsync(game.ExecutablePath, config, selection.Profile, _settings.WineCommand, progress);
                            ThcrapStatus = "THCRAP is ready.";
                        }
                    }
                    catch (Exception exception) { item.Error = exception.Message; }
                    finally
                    {
                        item.Status = null;
                        launchProgress?.Dispose();
                    }
                }, () => ChangeExecutableFromPropertiesAsync(game));
                Games.Add(item);
            }
            ApplyGrouping();
            ScanStatus = Games.Count == 0 ? "No recognized Touhou games were found." : $"Found {Games.Count} game{(Games.Count == 1 ? "" : "s")}.";
            OnPropertyChanged(nameof(HasGames));
        }
        catch (Exception exception)
        {
            GlobalError = $"Could not scan the games folder: {exception.Message}";
            ScanStatus = "Scan failed.";
        }
        finally
        {
            IsScanning = false;
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    private void SelectPage(int page)
    {
        if (_selectedPage == page) return;
        _selectedPage = page;
        OnPropertyChanged(nameof(SelectedPage));
        OnPropertyChanged(nameof(IsGamesSelected));
        OnPropertyChanged(nameof(IsSettingsSelected));
        OnPropertyChanged(nameof(IsAboutSelected));
    }

    private async Task ChangeExecutableFromPropertiesAsync(DiscoveredGame game)
    {
        if (ShowGamePropertiesAsync is null) return;
        var executable = await ShowGamePropertiesAsync(game);
        if (executable is null) return;
        if (!File.Exists(executable) || !string.Equals(Path.GetExtension(executable), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new FileNotFoundException("Choose an existing Windows executable.", executable);
        _settings.GameExecutableOverrides[game.Definition.Id] = Path.GetFullPath(executable);
        await _settingsService.SaveAsync(_settings);
        await ScanAsync();
    }

    private void ApplyGrouping()
    {
        GameGroups.Clear();
        if (Games.Count == 0) return;
        var items = Games.OrderBy(item => item.CatalogId, StringComparer.OrdinalIgnoreCase).ToArray();
        if (SelectedGroupingMode == 1)
        {
            AddGroup("Mainline", items.Where(item => item.Category == GameCategory.Mainline));
            AddGroup("PC-98 era", items.Where(item => item.Category == GameCategory.Pc98));
            AddGroup("Off-mainline fighting games", items.Where(item => item.Category == GameCategory.OffMainline));
        }
        else GameGroups.Add(new GameGroupViewModel("", false, items));
    }

    private void AddGroup(string title, IEnumerable<GameItemViewModel> games)
    {
        var items = games.ToArray();
        if (items.Length > 0) GameGroups.Add(new GameGroupViewModel(title, true, items));
    }

    private async Task BootstrapNekoProjectAsync()
    {
        try
        {
            var progress = new Progress<BootstrapProgress>(item => NekoProjectStatus = item.Message);
            var result = await _nekoProject.EnsureInstalledAsync(_settings.NekoProjectVersion, progress);
            _settings.NekoProjectVersion = result.Version;
            NekoProjectStatus = $"Neko Project II {result.Version} is ready.";
            await _settingsService.SaveAsync(_settings);
        }
        catch (Exception exception) { NekoProjectStatus = $"Neko Project II setup failed: {exception.Message}"; }
    }

    private async Task BootstrapThcrapAsync()
    {
        if (!HasGamesRoot || IsThcrapBusy) return;
        IsThcrapBusy = true;
        try
        {
            var progress = new Progress<BootstrapProgress>(item => ThcrapStatus = item.Message);
            var result = await _bootstrap.EnsureInstalledAsync(_settings.ThcrapVersion, progress);
            _settings.ThcrapVersion = result.Version;
            await _settingsService.SaveAsync(_settings);
        }
        catch (Exception exception)
        {
            ThcrapStatus = $"THCRAP setup failed: {exception.Message}";
        }
        finally { IsThcrapBusy = false; }
    }

    private async Task BootstrapThpracAsync()
    {
        if (!HasGamesRoot || IsThpracBusy) return;
        IsThpracBusy = true;
        try { await EnsureThpracReadyAsync(); }
        catch (Exception exception)
        {
            _thpracSetupTask = null;
            ThpracStatus = $"thprac setup failed: {exception.Message}";
        }
        finally { IsThpracBusy = false; }
    }

    private async Task RetryThpracAsync()
    {
        _thpracSetupTask = null;
        await BootstrapThpracAsync();
    }

    private Task<BootstrapResult> EnsureThpracReadyAsync() =>
        _thpracSetupTask ??= InstallThpracAsync();

    private async Task<BootstrapResult> InstallThpracAsync()
    {
        var progress = new Progress<BootstrapProgress>(item => ThpracStatus = item.Message);
        var result = await _thpracBootstrap.EnsureInstalledAsync(_settings.ThpracVersion, progress);
        _settings.ThpracVersion = result.Version;
        ThpracStatus = $"thprac {result.Version} is ready.";
        await _settingsService.SaveAsync(_settings);
        return result;
    }

    private void OnSettingsChanged()
    {
        OnPropertyChanged(nameof(GamesRoot));
        OnPropertyChanged(nameof(HasGamesRoot));
        OnPropertyChanged(nameof(WineCommand));
        OnPropertyChanged(nameof(SelectedViewMode));
        OnPropertyChanged(nameof(IsCardView));
        OnPropertyChanged(nameof(IsHorizontalCardView));
        OnPropertyChanged(nameof(SelectedGroupingMode));
        OnPropertyChanged(nameof(SelectedBackdropMode));
        OnPropertyChanged(nameof(SelectedTabAnimationMode));
        RescanCommand.RaiseCanExecuteChanged();
        RetryThcrapCommand.RaiseCanExecuteChanged();
        RetryThpracCommand.RaiseCanExecuteChanged();
    }
}

public sealed record ProfileSelectionResult(LaunchProfile Profile, bool RememberForGame);

public interface ILaunchProgressSession : IProgress<string>, IDisposable
{
}
