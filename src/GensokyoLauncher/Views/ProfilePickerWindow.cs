using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System.Collections.ObjectModel;
using GensokyoLauncher.Models;
using GensokyoLauncher.ViewModels;

namespace GensokyoLauncher.Views;

public sealed class ProfilePickerWindow : Window
{
    private readonly ComboBox _profiles;
    private readonly CheckBox _remember;
    public bool ManageRequested { get; private set; }

    public ProfilePickerWindow(string gameTitle, IReadOnlyList<LaunchProfile> profiles, string selectedId,
        int backdropMode)
    {
        Title = "Choose launch profile";
        Width = 520; Height = 230; MinWidth = 440; MinHeight = 220;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowAppearance.ApplyBackdrop(this, backdropMode);
        var heading = new TextBlock { Text = gameTitle, FontSize = 19, FontWeight = Avalonia.Media.FontWeight.SemiBold, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis };
        _profiles = new ComboBox
        {
            ItemsSource = profiles,
            Margin = new Thickness(0, 14, 0, 12),
            Height = 38,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MaxDropDownHeight = 320
        };
        _profiles.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<LaunchProfile>((profile, _) =>
            new TextBlock { Text = profile?.Name ?? string.Empty, VerticalAlignment = VerticalAlignment.Center });
        _profiles.SelectedItem = profiles.FirstOrDefault(p => p.Id == selectedId) ?? profiles.FirstOrDefault();
        _remember = new CheckBox { Content = "Prefer this profile for this game", Margin = new Thickness(0, 5, 0, 12) };
        var launch = DialogButton("Launch", minWidth: 92);
        launch.IsDefault = true;
        launch.Click += (_, _) => { if (_profiles.SelectedItem is LaunchProfile p) Close(new ProfileSelectionResult(p, _remember.IsChecked == true)); };
        var cancel = DialogButton("Cancel", minWidth: 92);
        cancel.IsCancel = true;
        cancel.Click += (_, _) => Close(null);
        var manage = DialogButton("Manage profiles");
        manage.Click += (_, _) => { ManageRequested = true; Close(null); };
        var buttons = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), VerticalAlignment = VerticalAlignment.Bottom, Children = { manage, AtColumn(cancel, 1), AtColumn(launch, 2) }, ColumnSpacing = 8 };
        Content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"), Margin = new Thickness(22),
            Children = { heading, At(_profiles, 1), At(_remember, 2), At(buttons, 3) }
        };
    }

    private static T At<T>(T control, int row) where T : Control { Grid.SetRow(control, row); return control; }
    private static T AtColumn<T>(T control, int column) where T : Control { Grid.SetColumn(control, column); return control; }
    private static Button DialogButton(string content, double minWidth = 0) => new()
    {
        Content = content,
        MinWidth = minWidth,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center
    };
}

public sealed class ProfileManagerWindow : Window
{
    private readonly ObservableCollection<LaunchProfile> _profiles;
    private readonly ComboBox _list;
    private readonly TextBox _name = new();
    private readonly ComboBox _language;
    private readonly ComboBox _mode = new() { ItemsSource = Enum.GetValues<LaunchDisplayMode>() };
    private readonly NumericUpDown _width = new() { Minimum = 320, Maximum = 7680, Increment = 1 };
    private readonly NumericUpDown _height = new() { Minimum = 240, Maximum = 4320, Increment = 1 };
    private readonly CheckBox _thprac = new() { Content = "Enable thprac practice tools" };
    private readonly CheckBox _default = new() { Content = "Use as global default" };
    private string? _defaultId;

    public ProfileManagerWindow(IEnumerable<LaunchProfile> profiles, string? defaultId,
        IReadOnlyList<OfficialLanguage> officialLanguages, int backdropMode)
    {
        Title = "Manage launch profiles"; Width = 760; Height = 540; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowAppearance.ApplyBackdrop(this, backdropMode);
        _profiles = new ObservableCollection<LaunchProfile>(profiles.Select(Clone)); _defaultId = defaultId;
        var languageOptions = new List<LanguageOption> { new(null, "Original Japanese") };
        languageOptions.AddRange(officialLanguages.Select(language =>
            new LanguageOption(language.PatchId, $"{language.Name} ({language.PatchId})")));
        foreach (var patchId in _profiles.Select(profile => profile.LanguagePatchId)
                     .Where(id => id is not null).Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase))
            if (!languageOptions.Any(option => string.Equals(option.PatchId, patchId, StringComparison.OrdinalIgnoreCase)))
                languageOptions.Add(new LanguageOption(patchId, patchId));
        _language = new ComboBox
        {
            ItemsSource = languageOptions,
            Height = 36,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MaxDropDownHeight = 320,
            IsTextSearchEnabled = true
        };
        _language.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<LanguageOption>((language, _) =>
            new TextBlock { Text = language?.Label ?? string.Empty, VerticalAlignment = VerticalAlignment.Center });
        _list = new ComboBox
        {
            ItemsSource = _profiles,
            Height = 36,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MaxDropDownHeight = 280
        };
        _list.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<LaunchProfile>((profile, _) =>
            new TextBlock { Text = profile?.Name ?? string.Empty, VerticalAlignment = VerticalAlignment.Center });
        _list.SelectionChanged += (_, _) => LoadSelected();
        var add = DialogButton("Add", height: 36); add.Click += (_, _) => AddProfile();
        var remove = DialogButton("Delete", height: 36); remove.Click += (_, _) => DeleteSelectedProfile();
        var presets = new ComboBox { ItemsSource = new[] { "640×480", "960×720", "1280×960", "1600×1200", "1920×1440" }, PlaceholderText = "4:3 preset" };
        presets.SelectionChanged += (_, _) => { if (presets.SelectedItem is string s) { var parts = s.Split('×'); _width.Value = decimal.Parse(parts[0]); _height.Value = decimal.Parse(parts[1]); } };
        var save = DialogButton("Save"); save.IsDefault = true; save.Click += (_, _) => { SaveSelected(); Close((_profiles.ToList(), _defaultId)); };
        var cancel = DialogButton("Cancel"); cancel.IsCancel = true; cancel.Click += (_, _) => Close();
        var editor = new StackPanel { Spacing = 8, Children = { Label("Name"), _name, Label("Language patch"), _language, Label("Display mode"), _mode, Label("Windowed client size"), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _width, new TextBlock { Text = "×", VerticalAlignment = VerticalAlignment.Center }, _height, presets } }, _thprac, _default } };
        var body = new StackPanel
        {
            Spacing = 16,
            Children =
            {
                Label("Profile"),
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                    ColumnSpacing = 8,
                    Children = { _list, AtColumn(add, 1), AtColumn(remove, 2) }
                },
                editor
            }
        };
        Content = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Margin = new Thickness(22), Children = { body, AtRow(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Children = { cancel, save } }, 1) } };
        _list.SelectedIndex = 0;
    }

    private void LoadSelected()
    {
        if (_list.SelectedItem is not LaunchProfile profile) return;
        _name.Text = profile.Name;
        _language.SelectedItem = (_language.ItemsSource as IEnumerable<LanguageOption>)?.FirstOrDefault(option =>
            string.Equals(option.PatchId, profile.LanguagePatchId, StringComparison.OrdinalIgnoreCase));
        _mode.SelectedItem = profile.DisplayMode;
        _width.Value = profile.WindowWidth;
        _height.Value = profile.WindowHeight;
        _thprac.IsChecked = profile.UseThprac;
        _default.IsChecked = profile.Id == _defaultId;
    }

    private void AddProfile()
    {
        SaveSelected();
        var profile = new LaunchProfile();
        _profiles.Add(profile);
        _list.SelectedItem = profile;
    }

    private void DeleteSelectedProfile()
    {
        if (_list.SelectedItem is not LaunchProfile profile || _profiles.Count <= 1) return;
        var index = _profiles.IndexOf(profile);
        _profiles.RemoveAt(index);
        if (_defaultId == profile.Id) _defaultId = _profiles[0].Id;
        _list.SelectedIndex = Math.Min(index, _profiles.Count - 1);
    }

    private void SaveSelected()
    {
        if (_list.SelectedItem is not LaunchProfile profile) return;
        var index = _profiles.IndexOf(profile);
        if (index < 0) return;

        profile.Name = string.IsNullOrWhiteSpace(_name.Text) ? "Launch profile" : _name.Text.Trim();
        profile.LanguagePatchId = (_language.SelectedItem as LanguageOption)?.PatchId;
        profile.DisplayMode = _mode.SelectedItem is LaunchDisplayMode mode ? mode : LaunchDisplayMode.GameDefault;
        profile.WindowWidth = (int)(_width.Value ?? 1280);
        profile.WindowHeight = (int)(_height.Value ?? 960);
        profile.UseThprac = _thprac.IsChecked == true;
        profile.Normalize();
        if (_default.IsChecked == true) _defaultId = profile.Id;

    }
    private static TextBlock Label(string text) => new() { Text = text, Opacity = .65 };
    private static T AtRow<T>(T c, int r) where T : Control { Grid.SetRow(c, r); return c; }
    private static T AtColumn<T>(T c, int col) where T : Control { Grid.SetColumn(c, col); return c; }
    private static Button DialogButton(string content, double height = double.NaN) => new()
    {
        Content = content,
        Height = height,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center
    };
    private static LaunchProfile Clone(LaunchProfile p) => new() { Id = p.Id, Name = p.Name, LanguagePatchId = p.LanguagePatchId, DisplayMode = p.DisplayMode, WindowWidth = p.WindowWidth, WindowHeight = p.WindowHeight, UseThprac = p.UseThprac };
    private sealed record LanguageOption(string? PatchId, string Label);
}

public sealed class Pc98DiskPickerWindow : Window
{
    private readonly ComboBox _disks;

    public Pc98DiskPickerWindow(string gameTitle, IReadOnlyList<string> diskImages, string preferredPath,
        int backdropMode)
    {
        Title = "Choose PC-98 disk image";
        Width = 500;
        Height = 205;
        MinWidth = 420;
        MinHeight = 190;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowAppearance.ApplyBackdrop(this, backdropMode);

        var heading = new TextBlock { Text = gameTitle, FontSize = 19, FontWeight = Avalonia.Media.FontWeight.SemiBold };
        var prompt = new TextBlock { Text = "Choose which disk image to launch.", Opacity = .68, Margin = new Thickness(0, 4, 0, 0) };
        _disks = new ComboBox
        {
            ItemsSource = diskImages,
            Height = 38,
            Margin = new Thickness(0, 14, 0, 14),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxDropDownHeight = 280
        };
        _disks.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((path, _) =>
            new TextBlock { Text = Path.GetFileName(path), VerticalAlignment = VerticalAlignment.Center });
        _disks.SelectedItem = diskImages.FirstOrDefault(path => string.Equals(path, preferredPath, StringComparison.OrdinalIgnoreCase))
            ?? diskImages.FirstOrDefault();

        var launch = DialogButton("Launch", 84, 34);
        launch.IsDefault = true;
        launch.Click += (_, _) => Close(_disks.SelectedItem as string);
        var cancel = DialogButton("Cancel", 84, 34);
        cancel.IsCancel = true;
        cancel.Click += (_, _) => Close(null);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancel, launch }
        };

        Content = new StackPanel
        {
            Margin = new Thickness(22),
            Children = { heading, prompt, _disks, buttons }
        };
    }

    private static Button DialogButton(string content, double minWidth, double height) => new()
    {
        Content = content,
        MinWidth = minWidth,
        Height = height,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center
    };
}
