// RetroSpy, Phosphor edition: finds SuperStations running Phosphor on this PC's network.
// Copyright (C) 2026 f3bandit. GPL-3.0, as the rest of RetroSpy (see LICENSE).
//
// As Phosphor for Windows finds them: GET /info on Phosphor's remote port (8767) of every address in the /24
// around each of this PC's IPv4 addresses, 64 at a time, a short connect timeout first. A couple of seconds.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RetroSpy.Phosphor
{
    public sealed record PhosphorFound(string Address, string Version);

    public static class PhosphorDiscover
    {
        public const int Port = 8767;

        public static async Task<List<PhosphorFound>> Find()
        {
            var targets = new HashSet<string>();
            var own = new HashSet<string>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var a in ni.GetIPProperties().UnicastAddresses)
                {
                    if (a.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    byte[] b = a.Address.GetAddressBytes();
                    if (b[0] == 127 || (b[0] == 169 && b[1] == 254)) continue;
                    own.Add(a.Address.ToString());
                    for (int h = 1; h < 255; h++) targets.Add(new IPAddress(new[] { b[0], b[1], b[2], (byte)h }).ToString());
                }
            }
            targets.ExceptWith(own);

            var found = new List<PhosphorFound>();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var gate = new SemaphoreSlim(64);
            await Task.WhenAll(targets.Select(async t =>
            {
                await gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    using (var tcp = new TcpClient())
                    {
                        var connect = tcp.ConnectAsync(t, Port);
                        if (await Task.WhenAny(connect, Task.Delay(350)).ConfigureAwait(false) != connect || !tcp.Connected) return;
                    }
                    string json = await http.GetStringAsync("http://" + t + ":" + Port + "/info").ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    var r = doc.RootElement;
                    bool ok = r.TryGetProperty("ok", out var o) && o.ValueKind == JsonValueKind.True;
                    string name = r.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!ok || name != "Phosphor") return;
                    string ver = r.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
                    lock (found) found.Add(new PhosphorFound(t, ver));
                }
                catch (Exception) { }
                finally { gate.Release(); }
            })).ConfigureAwait(false);
            return found.OrderBy(f => f.Address, StringComparer.Ordinal).ToList();
        }
    }
}
