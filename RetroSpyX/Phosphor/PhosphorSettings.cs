// RetroSpy, Phosphor edition: the SuperStation (Phosphor) source's own settings, kept next to RetroSpy's.
// Copyright (C) 2026 f3bandit. GPL-3.0, as the rest of RetroSpy (see LICENSE).

using System;
using System.IO;
using System.Text.Json;

namespace RetroSpy.Phosphor
{
    public static class PhosphorSettings
    {
        private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RetroSpy", "phosphor.json");

        private sealed class Data { public int Player { get; set; } public int Look { get; set; } public int Background { get; set; } public string Layout { get; set; } = ""; }

        /// <summary>layout: a Phosphor layout key to always draw, or "" for Automatic (the SuperStation's choice)</summary>
        public static (int player, int look, int background, string layout) Load()
        {
            try
            {
                var d = JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath));
                if (d != null) return (Math.Clamp(d.Player, 0, 4), Math.Clamp(d.Look, 0, 1), Math.Clamp(d.Background, 0, 2), d.Layout ?? "");
            }
            catch (Exception) { }
            return (0, 0, 0, "");
        }

        public static void Save(int player, int look, int background, string layout)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(new Data { Player = player, Look = look, Background = background, Layout = layout ?? "" }));
            }
            catch (Exception) { }
        }
    }
}
