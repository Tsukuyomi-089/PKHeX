using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace PKHeX.Avalonia;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            var args = desktop.Args;
            if (args is { Length: > 0 } && System.IO.File.Exists(args[0]))
                window.LoadFromPath(args[0]);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
