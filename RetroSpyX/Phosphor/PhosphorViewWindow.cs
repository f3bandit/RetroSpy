// RetroSpy, Phosphor edition: the window that shows the SuperStation's active controller as Phosphor draws it.
// Copyright (C) 2026 f3bandit. GPL-3.0, as the rest of RetroSpy (see LICENSE).

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;

namespace RetroSpy.Phosphor
{
    public enum PhosphorBackground { Dark, GreenScreen, Transparent }

    public sealed class PhosphorViewWindow : Window
    {
        private readonly PhosphorReader _reader;
        public PhosphorReader Reader => _reader;
        private readonly PhosphorPadView _pad = new();
        private readonly TextBlock _title = new() { FontSize = 13, Foreground = new SolidColorBrush(Color.Parse("#a39d92")), VerticalAlignment = VerticalAlignment.Center };
        private readonly bool _staticName;

        public PhosphorViewWindow()
        {
            throw new NotImplementedException();    // (the XAML previewer's constructor; never used)
        }

        public PhosphorViewWindow(PhosphorReader reader, PhosphorBackground background, bool staticName)
        {
            _reader = reader;
            _staticName = staticName;
            Width = 760;
            Height = 470;
            MinWidth = 300;
            MinHeight = 190;
            Icon = null;

            _pad.Margin = new Thickness(22);
            SetBackground(background);

            UpdateController(reader);
            reader.ControllerStateChanged += (s, e) => _pad.SetState(e);
            reader.StatusChanged += (s, e) => UpdateController(reader);
            reader.ControllerDisconnected += (s, e) => Close();
            Closing += (s, e) => _reader.Finish();
        }

        /// <summary>Dark (with the controller's name on a bar), green screen or transparent; also while it's open
        /// (the setup window's Background, changed while watching)</summary>
        public void SetBackground(PhosphorBackground background)
        {
            var root = new DockPanel();
            if (_pad.Parent is Panel old) old.Children.Remove(_pad);
            if (_title.Parent is Border ob) ob.Child = null;
            TransparencyLevelHint = new[] { background == PhosphorBackground.Transparent ? WindowTransparencyLevel.Transparent : WindowTransparencyLevel.None };
            if (background == PhosphorBackground.Dark)
            {
                Background = new SolidColorBrush(Color.Parse("#141416"));
                var bar = new Border
                {
                    Height = 34,
                    Padding = new Thickness(14, 0),
                    Background = new SolidColorBrush(Color.Parse("#101012")),
                    BorderBrush = new SolidColorBrush(Color.Parse("#2c2c30")),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Child = _title,
                };
                DockPanel.SetDock(bar, Dock.Top);
                root.Children.Add(bar);
            }
            else if (background == PhosphorBackground.GreenScreen) Background = new SolidColorBrush(Color.Parse("#00ff00"));
            else Background = Brushes.Transparent;
            root.Children.Add(_pad);
            Content = root;
        }

        /// <summary>The layout chosen in the setup window, changed while watching</summary>
        public void Redraw() => UpdateController(_reader);

        private void UpdateController(PhosphorReader r)
        {
            var c = r.Controller;
            _pad.SetController(r.Layout, c);
            string what = c == null ? "No controller"
                : r.LayoutChosen ? c.Title + " · " + (r.Layout?.Name ?? r.ChosenLayout) + " · chosen"
                : c.Title + (string.IsNullOrEmpty(c.LayoutName) ? "" : " · " + c.LayoutName);
            _title.Text = what;
            Title = _staticName ? "RetroSpy" : "RetroSpy · " + what;
        }
    }
}
