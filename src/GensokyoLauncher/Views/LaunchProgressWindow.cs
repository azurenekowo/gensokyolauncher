using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using GensokyoLauncher.ViewModels;

namespace GensokyoLauncher.Views;

public sealed class LaunchProgressWindow : Window, ILaunchProgressSession
{
    private readonly TextBlock _message;
    private bool _completed;

    public LaunchProgressWindow(string message, int backdropMode)
    {
        Title = "Launching game";
        Width = 460;
        Height = 154;
        MinWidth = 380;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowAppearance.ApplyBackdrop(this, backdropMode);

        _message = new TextBlock
        {
            Text = message,
            FontSize = 15,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 18,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                _message,
                new ProgressBar { IsIndeterminate = true, Height = 5 }
            }
        };
        Closing += (_, eventArgs) =>
        {
            if (!_completed) eventArgs.Cancel = true;
        };
    }

    public void Report(string value)
    {
        if (Dispatcher.UIThread.CheckAccess()) _message.Text = value;
        else Dispatcher.UIThread.Post(() => _message.Text = value);
    }

    public void Dispose()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Dispose);
            return;
        }
        if (_completed) return;
        _completed = true;
        Close();
    }
}
