// RetroSpy, Phosphor edition: what a SuperStation One running Phosphor says about its controllers.
// Copyright (C) 2026 f3bandit. GPL-3.0, as the rest of RetroSpy (see LICENSE).
//
// Phosphor (github.com/f3bandit/phosphor_os) answers {"cmd":"retrospy"} on the SS1 (its phos tool, run over the
// same SSH login RetroSpy uses): the controllers by RetroSpy's number (jsN), each with the layout Phosphor draws it
// with and which of its jsN buttons and axes each control is, which one is active (Player 1's), and the drawings
// of those layouts. Nothing here is installed on the SS1.

using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace RetroSpy.Phosphor
{
    public sealed class PhosphorItem
    {
        public char T { get; init; }            // c circle, r pill, t trigger, s stick, l lever, d D-pad
        public string Id { get; init; } = "";
        public double X { get; init; }
        public double Y { get; init; }
        public double A { get; init; }
        public double B { get; init; }
        public string Label { get; init; } = "";
    }

    public sealed class PhosphorLayout
    {
        public string Key { get; init; } = "generic";
        public string Name { get; init; } = "";
        public double Width { get; init; } = 600;
        public double Height { get; init; } = 345;
        public List<PhosphorItem> Items { get; } = new();
    }

    public sealed class PhosphorBind
    {
        public int Button { get; init; } = -1;   // jsN button, or
        public int Axis { get; init; } = -1;     // jsN axis
        public int Half { get; init; }           // -1 / 1: one direction of the axis is the control
        public bool Inverted { get; init; }
    }

    public sealed class PhosphorController
    {
        public int Js { get; init; }
        public string Name { get; init; } = "";
        public int Player { get; init; }
        public string LayoutKey { get; init; } = "generic";
        public string LayoutName { get; init; } = "";
        public string VidPid { get; init; } = "";
        public Dictionary<string, PhosphorBind> Binds { get; } = new();

        public string Title => (Player > 0 ? "Player " + Player.ToString(CultureInfo.InvariantCulture) + " · " : "") + Name;
    }

    public sealed class PhosphorStatus
    {
        public bool RetroSpyInstalled { get; init; }
        public string Host { get; init; } = "";
        public int Active { get; init; } = -1;
        public List<PhosphorController> Controllers { get; } = new();
        public Dictionary<string, PhosphorLayout> Layouts { get; } = new();

        public PhosphorController? Find(int js) => Controllers.Find(c => c.Js == js);

        /// <summary>The controller to show: one player's (1-4), or 0 for the active one.</summary>
        public PhosphorController? Pick(int player)
        {
            if (player > 0)
            {
                var p = Controllers.Find(c => c.Player == player);
                if (p != null) return p;
            }
            return Find(Active) ?? (Controllers.Count > 0 ? Controllers[0] : null);
        }
    }

    public static class PhosphorClient
    {
        public const string Phos = "/media/fat/phosphor/bin/phos";

        /// <summary>Asks the SS1 over an open SSH connection; null when it isn't running Phosphor.</summary>
        public static PhosphorStatus? Query(SshClient ssh)
        {
            if (ssh == null) throw new ArgumentNullException(nameof(ssh));
            using var cmd = ssh.CreateCommand("[ -x " + Phos + " ] && " + Phos + " '{\"cmd\":\"retrospy\"}'");
            cmd.CommandTimeout = TimeSpan.FromSeconds(8);
            string text = cmd.Execute();
            return string.IsNullOrWhiteSpace(text) ? null : Parse(text);
        }

        public static PhosphorStatus? Query(string host, string user, string password)
        {
            using var ssh = new SshClient(host, user, password);
            ssh.ConnectionInfo.Timeout = TimeSpan.FromSeconds(6);
            ssh.Connect();
            try { return Query(ssh); }
            finally { ssh.Disconnect(); }
        }

        public static PhosphorStatus? Parse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object || !r.TryGetProperty("controllers", out var ctrls)) return null;
            var st = new PhosphorStatus
            {
                RetroSpyInstalled = Bool(r, "installed"),
                Host = Str(r, "host"),
                Active = Int(r, "active", -1),
            };
            foreach (var c in ctrls.EnumerateArray())
            {
                var pc = new PhosphorController
                {
                    Js = Int(c, "js", -1),
                    Name = Str(c, "name"),
                    Player = Int(c, "player", 0),
                    LayoutKey = c.TryGetProperty("phosphor", out var ph) ? Str(ph, "layout", "generic") : "generic",
                    LayoutName = ph.ValueKind == JsonValueKind.Object ? Str(ph, "layout_name") : "",
                    VidPid = ph.ValueKind == JsonValueKind.Object ? Str(ph, "vidpid") : "",
                };
                if (ph.ValueKind == JsonValueKind.Object && ph.TryGetProperty("binds", out var binds) && binds.ValueKind == JsonValueKind.Object)
                {
                    foreach (var b in binds.EnumerateObject())
                    {
                        pc.Binds[b.Name] = new PhosphorBind
                        {
                            Button = Int(b.Value, "b", -1),
                            Axis = Int(b.Value, "a", -1),
                            Half = Int(b.Value, "half", 0),
                            Inverted = Bool(b.Value, "inv"),
                        };
                    }
                }
                st.Controllers.Add(pc);
            }
            if (r.TryGetProperty("layouts", out var lays) && lays.ValueKind == JsonValueKind.Object)
            {
                foreach (var l in lays.EnumerateObject())
                {
                    var pl = new PhosphorLayout
                    {
                        Key = l.Name,
                        Name = Str(l.Value, "name"),
                        Width = Int(l.Value, "w", 600),
                        Height = Int(l.Value, "h", 345),
                    };
                    if (l.Value.TryGetProperty("items", out var items))
                    {
                        foreach (var it in items.EnumerateArray())
                        {
                            string t = Str(it, "t", "c");
                            pl.Items.Add(new PhosphorItem
                            {
                                T = t.Length > 0 ? t[0] : 'c',
                                Id = Str(it, "id"),
                                X = Int(it, "x", 0), Y = Int(it, "y", 0), A = Int(it, "a", 0), B = Int(it, "b", 0),
                                Label = Str(it, "lab"),
                            });
                        }
                    }
                    st.Layouts[l.Name] = pl;
                }
            }
            return st;
        }

        private static string Str(JsonElement e, string k, string def = "") =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? def : def;

        private static int Int(JsonElement e, string k, int def) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int i) ? i : def;

        private static bool Bool(JsonElement e, string k) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;
    }
}
