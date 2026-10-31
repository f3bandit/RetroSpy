using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace RetroSpy
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop!.Startup += (sender, args) =>
                {
                    string? source = null;
                    string? skin = null;
                    string? delay = null;
                    bool skipSetup = args.Args.Length >= 1 && args.Args[0] == "-skipSetup";

                    foreach (var item in args.Args)
                    {
                        if (item.StartsWith("-source="))
                        {
                            source = item.Substring(8);
                        }
                        else if (item.StartsWith("-skin="))
                        {
                            skin = item.Substring(6);
                        }
                        else if (item.StartsWith("-delay="))
                        {
                            delay = item.Substring(7);
                        }
                    }
                    // Phosphor edition: --phosphor <address> [--player N] [--go] opens the SuperStation (Phosphor) source on
                    // that SuperStation (Phosphor's Windows app opens RetroSpy this way); --go starts watching
                    for (int i = 0; i < args.Args.Length; i++)
                    {
                        if (args.Args[i] == "--phosphor" && i + 1 < args.Args.Length) SetupWindow.PhosphorLaunchHost = args.Args[++i];
                        else if (args.Args[i] == "--player" && i + 1 < args.Args.Length && int.TryParse(args.Args[++i], out int pl)) SetupWindow.PhosphorLaunchPlayer = pl;
                        else if (args.Args[i] == "--go") SetupWindow.PhosphorLaunchGo = true;
                    }

                    desktop.MainWindow = new SetupWindow(skipSetup, source, skin, delay);
                };
            }

            base.OnFrameworkInitializationCompleted();
        }


    }
}