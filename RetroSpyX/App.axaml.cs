using Avalonia;
using System;
using System.Linq;
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
                    // Phosphor edition: one RetroSpy at a time (a second one hands its command line to the first, which
                    // shows itself, and exits); --phosphor <address> [--player N] [--go] opens the SuperStation (Phosphor)
                    // source on that SuperStation (Phosphor's Windows app opens RetroSpy this way), --go starts watching;
                    // --tray (Start with Windows) starts in the tray, no window
                    if (!Phosphor.PhosphorTray.FirstInstance(args.Args)) { Environment.Exit(0); return; }
                    Phosphor.PhosphorTray.ApplyLaunchArgs(args.Args);
                    bool hidden = args.Args.Contains("--tray");

                    SetupWindow.StartHidden = hidden;
                    var w = new SetupWindow(skipSetup, source, skin, delay);
                    Phosphor.PhosphorTray.Setup(this, desktop, w, hidden);
                };
            }

            base.OnFrameworkInitializationCompleted();
        }


    }
}