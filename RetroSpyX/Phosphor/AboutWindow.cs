// RetroSpy, Phosphor edition: About and credits.
// Copyright (C) 2026 f3bandit. GPL-3.0, as the rest of RetroSpy (see LICENSE).

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace RetroSpy
{
    public sealed class AboutWindow : Window
    {
        public AboutWindow()
        {
            Title = "About RetroSpy · Phosphor edition";
            Width = 640;
            Height = 600;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var muted = new SolidColorBrush(Color.Parse("#a39d92"));
            var accent = new SolidColorBrush(Color.Parse("#f0b54a"));
            var col = new StackPanel { Spacing = 12, Margin = new Thickness(28, 24) };

            col.Children.Add(new TextBlock { Text = "RETROSPY", FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"), FontSize = 18, LetterSpacing = 5, Foreground = accent });
            col.Children.Add(new TextBlock
            {
                Text = "Phosphor edition · " + (Assembly.GetEntryAssembly()?.GetName()?.Version?.ToString() ?? ""),
                Foreground = muted, FontSize = 13,
            });

            void Para(string head, string body)
            {
                col.Children.Add(new TextBlock { Text = head, FontWeight = FontWeight.SemiBold, FontSize = 15, Margin = new Thickness(0, 6, 0, 0) });
                col.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, FontSize = 13.5, Foreground = new SolidColorBrush(Color.Parse("#c9c4bb")) });
            }

            Para("RetroSpy", "By zoggins and the RetroSpy contributors (retro-spy.com, github.com/retrospy/RetroSpy). Everything RetroSpy does, its sources, readers, firmware and its MiSTer program, is their work.");
            Para("NintendoSpy", "RetroSpy is a fork of NintendoSpy by jaburns (github.com/jaburns/NintendoSpy).");
            Para("Skins", "By their authors: " + SkinAuthors() + ".");
            Para("Thanks", "Brian Khuu and the Arduino Game Boy Printer Emulator contributors, for decoding the Game Boy Printer protocol.");
            Para("Phosphor edition", "By f3bandit (github.com/f3bandit/RetroSpy): the SuperStation (Phosphor) source, which shows the controller a SuperStation One running Phosphor says is active, drawn as Phosphor's controller test draws it (f3bandit's design), and Phosphor's look.");
            Para("License", "GPL-3.0, as RetroSpy. The source of this edition is at github.com/f3bandit/RetroSpy; Phosphor itself is a separate program.");

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 10, 0, 0) };
            var rs = new Avalonia.Controls.Button { Content = "RetroSpy's own About page" };
            rs.Click += (s, e) => SetupWindow.OpenRetroSpyAbout();
            var src = new Avalonia.Controls.Button { Content = "This edition's source" };
            src.Click += (s, e) => Open("https://github.com/f3bandit/RetroSpy");
            var close = new Avalonia.Controls.Button { Content = "Close", Classes = { "primary" } };
            close.Click += (s, e) => Close();
            buttons.Children.Add(close);
            buttons.Children.Add(rs);
            buttons.Children.Add(src);
            col.Children.Add(buttons);

            Content = new ScrollViewer { Content = col };
        }

        /// <summary>Every skin author, from the skins RetroSpy has (each skin.xml's author).</summary>
        private static string SkinAuthors()
        {
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string dir = Path.Combine(AppContext.BaseDirectory, "skins");
                foreach (var f in Directory.EnumerateFiles(dir, "skin.xml", SearchOption.AllDirectories))
                {
                    var m = Regex.Match(File.ReadAllText(f), "author=\"([^\"]+)\"");
                    if (!m.Success) continue;
                    foreach (var n in Regex.Split(m.Groups[1].Value, @"\s*(?:,|/|&| and )\s*"))
                        if (n.Trim().Length > 0) names.Add(n.Trim());
                }
            }
            catch (Exception) { }
            return names.Count > 0 ? string.Join(", ", names) : "their authors (each skin's skin.xml)";
        }

        private static void Open(string url)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) Process.Start("open", url);
                else Process.Start("xdg-open", url);
            }
            catch (Exception) { }
        }
    }
}
