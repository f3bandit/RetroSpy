// RetroSpy, Phosphor edition: the tray icon, one RetroSpy at a time, and starting with Windows.
// Copyright (C) 2026 f3bandit. GPL-3.0, as the rest of RetroSpy (see LICENSE).
//
// As Phosphor for Windows: closing the window leaves RetroSpy in the tray (a viewer that's open keeps going); the
// tray opens it again, starts or stops watching, checks for updates, turns Start with Windows on or off, or exits.
// Start with Windows: HKCU\...\Run starts it with --tray (no window, just the tray icon).
//
// One RetroSpy at a time: a second one (Phosphor for Windows' Open RetroSpy while this one is in the tray) hands its
// command line to the first through a named pipe and exits; the first shows its window and does what it asked
// (--phosphor <address> [--player N] [--go]).

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RetroSpy.Phosphor
{
    public static class PhosphorTray
    {
        private const string Name = "RetroSpyPhosphorEdition";
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValue = "RetroSpy (Phosphor edition)";

        private static Mutex? _one;
        private static bool _quitting;
        private static NativeMenuItem? _watchItem, _autoItem;

        /// <summary>False when another RetroSpy is running: it was handed this command line, and this one should exit.</summary>
        public static bool FirstInstance(string[] args)
        {
            _one = new Mutex(true, "Local\\" + Name, out bool first);
            if (first) return true;
            try
            {
                using var pipe = new NamedPipeClientStream(".", Name, PipeDirection.Out);
                pipe.Connect(3000);
                using var w = new StreamWriter(pipe);
                w.Write(string.Join("\u001f", args));
            }
            catch (Exception) { }
            return false;
        }

        /// <summary>The tray, closing to it, and listening for a second RetroSpy's command line.</summary>
        public static void Setup(Application app, IClassicDesktopStyleApplicationLifetime desktop, SetupWindow w, bool hidden)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            w.Closing += (s, e) =>
            {
                if (_quitting) return;
                e.Cancel = true;                       // (to the tray; Exit in the tray's menu ends it)
                w.Hide();
            };

            var menu = new NativeMenu();
            var open = new NativeMenuItem("Open RetroSpy");
            open.Click += (s, e) => ShowWindow(w);
            _watchItem = new NativeMenuItem("Start watching");
            _watchItem.Click += (s, e) => { ShowWindow(w); w.StartOrStop(); };
            var upd = new NativeMenuItem("Check for updates");
            upd.Click += (s, e) => { ShowWindow(w); w.CheckForUpdatesFromTray(); };
            _autoItem = new NativeMenuItem("Start with Windows") { ToggleType = NativeMenuItemToggleType.CheckBox, IsChecked = StartsWithWindows() };
            _autoItem.Click += (s, e) => { SetStartWithWindows(!StartsWithWindows()); _autoItem.IsChecked = StartsWithWindows(); };
            _autoItem.IsEnabled = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var exit = new NativeMenuItem("Exit RetroSpy");
            exit.Click += (s, e) => { _quitting = true; Environment.Exit(0); };
            menu.Items.Add(open);
            menu.Items.Add(_watchItem);
            menu.Items.Add(upd);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(_autoItem);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(exit);
            w.WatchingChanged += (s, e) => { if (_watchItem != null) _watchItem.Header = w.Watching ? "Stop watching" : "Start watching"; };

            var tray = new TrayIcon { ToolTipText = "RetroSpy (Phosphor edition)", Menu = menu, IsVisible = true };
            try { tray.Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://RetroSpy/RetroSpy.ico"))); } catch (Exception) { }
            tray.Clicked += (s, e) => ShowWindow(w);
            TrayIcon.SetIcons(app, new TrayIcons { tray });

            if (!hidden) w.Show();
            Listen(w);
        }

        public static void ShowWindow(Window w)
        {
            w.Show();
            if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
            w.Activate();
        }

        /// <summary>A second RetroSpy's command line: the window, and what it asked for.</summary>
        private static void Listen(SetupWindow w)
        {
            Task.Run(async () =>
            {
                for (;;)
                {
                    try
                    {
                        using var pipe = new NamedPipeServerStream(Name, PipeDirection.In, 1);
                        await pipe.WaitForConnectionAsync().ConfigureAwait(false);
                        using var r = new StreamReader(pipe);
                        string[] args = (await r.ReadToEndAsync().ConfigureAwait(false)).Split('\u001f', StringSplitOptions.RemoveEmptyEntries);
                        Dispatcher.UIThread.Post(() =>
                        {
                            ShowWindow(w);
                            if (ApplyLaunchArgs(args)) w.PhosphorLaunchFromArgs();
                        });
                    }
                    catch (Exception) { await Task.Delay(1000).ConfigureAwait(false); }
                }
            });
        }

        /// <summary>--phosphor <address> [--player N] [--go]: into SetupWindow's launch settings; true when there was one.</summary>
        public static bool ApplyLaunchArgs(string[] args)
        {
            bool any = false;
            SetupWindow.PhosphorLaunchGo = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--phosphor" && i + 1 < args.Length) { SetupWindow.PhosphorLaunchHost = args[++i]; any = true; }
                else if (args[i] == "--player" && i + 1 < args.Length && int.TryParse(args[++i], out int pl)) SetupWindow.PhosphorLaunchPlayer = pl;
                else if (args[i] == "--go") SetupWindow.PhosphorLaunchGo = true;
            }
            return any;
        }

        // ---------------------------------------------------------------- Start with Windows (HKCU Run)

        public static bool StartsWithWindows()
        {
            if (!OperatingSystem.IsWindows()) return false;
            try
            {
                using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey);
                return k?.GetValue(RunValue) is string;
            }
            catch (Exception) { return false; }
        }

        public static void SetStartWithWindows(bool on)
        {
            if (!OperatingSystem.IsWindows()) return;
            try
            {
                using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey);
                if (on) k.SetValue(RunValue, "\"" + (Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "RetroSpy.exe")) + "\" --tray");
                else k.DeleteValue(RunValue, false);
            }
            catch (Exception) { }
            if (_autoItem != null) _autoItem.IsChecked = StartsWithWindows();
        }
    }
}
