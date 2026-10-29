using System.ComponentModel;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using GensokyoLauncher.ViewModels;
using GensokyoLauncher.Models;

namespace GensokyoLauncher.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _appearanceViewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Opened += OnOpened;
    }

    private void OnDataContextChanged(object? sender, EventArgs eventArgs)
    {
        if (_appearanceViewModel is not null)
            _appearanceViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _appearanceViewModel = DataContext as MainWindowViewModel;
        if (_appearanceViewModel is not null)
            _appearanceViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyAppearance();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(MainWindowViewModel.SelectedBackdropMode)
            or nameof(MainWindowViewModel.SelectedTabAnimationMode))
            ApplyAppearance();
    }

    private void ApplyAppearance()
    {
        if (_appearanceViewModel is null) return;

        WindowAppearance.ApplyBackdrop(this, _appearanceViewModel.SelectedBackdropMode);

        MainCarousel.PageTransition = _appearanceViewModel.SelectedTabAnimationMode switch
        {
            1 => CreateTabTransition(PageSlide.SlideAxis.Horizontal),
            2 => null,
            _ => CreateTabTransition(PageSlide.SlideAxis.Vertical)
        };
    }

    private static IPageTransition CreateTabTransition(PageSlide.SlideAxis axis)
    {
        var duration = TimeSpan.FromMilliseconds(220);
        var transition = new CompositePageTransition();
        transition.PageTransitions.Add(new CrossFade(duration));
        transition.PageTransitions.Add(new PageSlide(duration, axis));
        return transition;
    }

    private async void OnOpened(object? sender, EventArgs eventArgs)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectProfileAsync = game => SelectProfileAsync(viewModel, game);
            viewModel.SelectPc98DiskAsync = SelectPc98DiskAsync;
            viewModel.ShowGamePropertiesAsync = ShowGamePropertiesAsync;
            viewModel.BeginLaunchProgress = BeginLaunchProgress;
            if (await viewModel.InitializeAsync()) await ChooseFolderAsync();
        }
    }

    private async Task<string?> ShowGamePropertiesAsync(DiscoveredGame game)
    {
        var dialog = new GamePropertiesWindow(game, CurrentBackdropMode);
        return await dialog.ShowDialog<string?>(this);
    }

    private ILaunchProgressSession BeginLaunchProgress(string message)
    {
        var dialog = new LaunchProgressWindow(message, CurrentBackdropMode);
        _ = dialog.ShowDialog(this);
        return dialog;
    }

    private async Task<string?> SelectPc98DiskAsync(Models.DiscoveredGame game)
    {
        var images = game.AvailableDiskImages ?? [game.ExecutablePath];
        if (images.Count <= 1) return images.FirstOrDefault();
        var dialog = new Pc98DiskPickerWindow(game.Definition.Title, images, game.ExecutablePath, CurrentBackdropMode);
        return await dialog.ShowDialog<string?>(this);
    }

    private async void ChooseFolder_OnClick(object? sender, RoutedEventArgs eventArgs) => await ChooseFolderAsync();

    private async Task ChooseFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose the folder containing your Touhou games",
            AllowMultiple = false
        });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path && DataContext is MainWindowViewModel viewModel)
            await viewModel.SetGamesRootAsync(path);
    }

    private async void SaveSettings_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainWindowViewModel viewModel)
            await viewModel.SaveSettingsAsync();
    }

    private async Task<ProfileSelectionResult?> SelectProfileAsync(MainWindowViewModel viewModel, Models.DiscoveredGame game)
    {
        while (true)
        {
            var preferred = viewModel.PreferredProfile(game.Definition.Id).Id;
            var dialog = new ProfilePickerWindow(game.Definition.Title, viewModel.LaunchProfiles, preferred,
                CurrentBackdropMode);
            var result = await dialog.ShowDialog<ProfileSelectionResult?>(this);
            if (!dialog.ManageRequested) return result;
            await ManageProfilesAsync(viewModel);
        }
    }

    private async void ManageProfiles_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;
        await ManageProfilesAsync(viewModel);
    }

    private async Task ManageProfilesAsync(MainWindowViewModel viewModel)
    {
        var languages = await viewModel.GetOfficialLanguagesAsync();
        var dialog = new ProfileManagerWindow(viewModel.LaunchProfiles, viewModel.GlobalDefaultProfile.Id, languages,
            CurrentBackdropMode);
        var result = await dialog.ShowDialog<(List<LaunchProfile> Profiles, string? DefaultId)?>(this);
        if (result is { } value) await viewModel.SaveProfilesAsync(value.Profiles, value.DefaultId);
    }

    private int CurrentBackdropMode => _appearanceViewModel?.SelectedBackdropMode ?? 0;
}
