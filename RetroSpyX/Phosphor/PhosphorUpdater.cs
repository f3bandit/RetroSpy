// RetroSpy, Phosphor edition: keeps itself up to date from its own GitHub releases.
// Copyright (C) 2026 f3bandit. GPL-3.0, as the rest of RetroSpy (see LICENSE).
//
// Only this edition's releases count (f3bandit/RetroSpy, tags phosphor-<version>, never a draft or a pre-release),
// so RetroSpy's own builds are never installed over it. Installing: the release zip is downloaded and checked against
// the release's SHA256SUMS.txt, unpacked next to this copy as <folder>.new, then a small script waits for RetroSpy to
// close, renames <folder> to <folder>.old and <folder>.new to <folder>, starts it again and removes the old one (put
// back if the rename fails). The same names Phosphor for Windows uses for the copy it bundles (RetroSpy\ next to
// Phosphor.exe), so either one can update it. Windows only: the release is the Windows build.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RetroSpy.Phosphor
{
    public sealed record PhosphorUpdate(Version Version, string Tag, string ZipUrl, string SumsUrl, string PageUrl);

    public static class PhosphorUpdater
    {
        public const string Repo = "f3bandit/RetroSpy";
        public const string Asset = "RetroSpy-Phosphor-edition-win64.zip";
        public static string ReleasesPage => "https://github.com/" + Repo + "/releases";

        private static string StateFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RetroSpy", "phosphor-update.json");
        private sealed class State { public DateTime Checked { get; set; } public string Skip { get; set; } = ""; }

        public static Version Current
        {
            get
            {
                var v = Assembly.GetEntryAssembly()?.GetName()?.Version ?? new Version(0, 0, 0);
                return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
            }
        }

        private static HttpClient Http()
        {
            var h = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            h.DefaultRequestHeaders.UserAgent.ParseAdd("RetroSpy-Phosphor-edition/" + Current);
            return h;
        }

        /// <summary>The newest release of this edition when it's newer than this copy; null when it isn't (or no answer).</summary>
        public static async Task<PhosphorUpdate?> Check()
        {
            using var h = Http();
            string json = await h.GetStringAsync("https://api.github.com/repos/" + Repo + "/releases?per_page=30").ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            PhosphorUpdate? best = null;
            foreach (var r in doc.RootElement.EnumerateArray())
            {
                if (r.TryGetProperty("draft", out var d) && d.GetBoolean()) continue;
                if (r.TryGetProperty("prerelease", out var p) && p.GetBoolean()) continue;
                string tag = r.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
                if (!tag.StartsWith("phosphor-", StringComparison.Ordinal)) continue;
                if (!Version.TryParse(tag.Substring("phosphor-".Length), out var v)) continue;
                v = new Version(v.Major, v.Minor, Math.Max(0, v.Build));
                string zip = "", sums = "";
                if (r.TryGetProperty("assets", out var assets))
                    foreach (var a in assets.EnumerateArray())
                    {
                        string n = a.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
                        string u = a.TryGetProperty("browser_download_url", out var au) ? au.GetString() ?? "" : "";
                        if (n == Asset) zip = u;
                        else if (n == "SHA256SUMS.txt") sums = u;
                    }
                if (zip.Length == 0 || sums.Length == 0) continue;
                string page = r.TryGetProperty("html_url", out var hu) ? hu.GetString() ?? ReleasesPage : ReleasesPage;
                if (best == null || v > best.Version) best = new PhosphorUpdate(v, tag, zip, sums, page);
            }
            return best != null && best.Version > Current ? best : null;
        }

        /// <summary>Time for the once-a-day check on start (and not a version the user said no to).</summary>
        public static bool Due()
        {
            var s = Load();
            return DateTime.UtcNow - s.Checked > TimeSpan.FromHours(20);
        }

        public static void Checked() { var s = Load(); s.Checked = DateTime.UtcNow; Save(s); }
        public static void Skip(Version v) { var s = Load(); s.Skip = v.ToString(); Save(s); }
        public static bool Skipped(Version v) => Load().Skip == v.ToString();

        private static State Load()
        {
            try { return JsonSerializer.Deserialize<State>(File.ReadAllText(StateFile)) ?? new State(); }
            catch (Exception) { return new State(); }
        }

        private static void Save(State s)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StateFile)!);
                File.WriteAllText(StateFile, JsonSerializer.Serialize(s));
            }
            catch (Exception) { }
        }

        /// <summary>This copy's folder, and whether it can update itself there (Windows, a folder it can write next to).</summary>
        public static bool CanInstall(out string folder, out string why)
        {
            folder = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) { why = "Updating itself works on Windows; download the new version from the releases page."; return false; }
            string? parent = Path.GetDirectoryName(folder);
            if (parent == null) { why = "RetroSpy's folder can't be updated in place."; return false; }
            try
            {
                string probe = Path.Combine(parent, ".retrospy-update-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "");
                File.Delete(probe);
            }
            catch (Exception)
            {
                why = "RetroSpy can't write next to its folder (" + parent + "): download the new version from the releases page.";
                return false;
            }
            why = "";
            return true;
        }

        /// <summary>Downloads, checks and unpacks the update next to this copy, then starts the swap script. The caller
        /// closes RetroSpy right after: the script waits for that, swaps the folders and starts the new one.</summary>
        public static async Task Install(PhosphorUpdate u, IProgress<string> progress)
        {
            if (!CanInstall(out string folder, out string why)) throw new InvalidOperationException(why);
            string parent = Path.GetDirectoryName(folder)!, name = Path.GetFileName(folder);
            string nw = Path.Combine(parent, name + ".new"), old = Path.Combine(parent, name + ".old");
            string tmp = Path.Combine(Path.GetTempPath(), "retrospy-" + u.Version + ".zip");

            using (var h = Http())
            {
                progress.Report("Downloading RetroSpy " + u.Version + "...");
                string sums = await h.GetStringAsync(u.SumsUrl).ConfigureAwait(false);
                string? want = sums.Split('\n').Select(l => l.Trim().Split(new[] { ' ', '*' }, StringSplitOptions.RemoveEmptyEntries))
                                   .Where(p => p.Length >= 2 && p[^1] == Asset).Select(p => p[0].ToLowerInvariant()).FirstOrDefault();
                if (want == null) throw new InvalidOperationException("The release has no checksum for " + Asset + ".");
                using (var src = await h.GetStreamAsync(u.ZipUrl).ConfigureAwait(false))
                using (var dst = File.Create(tmp))
                    await src.CopyToAsync(dst).ConfigureAwait(false);
                progress.Report("Checking the download...");
                string got;
                using (var f = File.OpenRead(tmp))
                    got = Convert.ToHexString(await SHA256.HashDataAsync(f).ConfigureAwait(false)).ToLowerInvariant();
                if (got != want) { File.Delete(tmp); throw new InvalidOperationException("The download didn't match the release's checksum, so it wasn't installed."); }
            }

            progress.Report("Unpacking...");
            if (Directory.Exists(nw)) Directory.Delete(nw, true);
            Directory.CreateDirectory(nw);
            string root = Path.GetFullPath(nw) + Path.DirectorySeparatorChar;
            using (var zip = ZipFile.OpenRead(tmp))
            {
                foreach (var e in zip.Entries)
                {
                    string n = e.FullName.Replace('\\', '/');
                    if (!n.StartsWith("RetroSpy/", StringComparison.Ordinal)) continue;
                    string rel = n.Substring("RetroSpy/".Length);
                    if (rel.Length == 0) continue;
                    string dest = Path.GetFullPath(Path.Combine(nw, rel));
                    if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;   // (nothing outside the folder)
                    if (n.EndsWith("/", StringComparison.Ordinal)) { Directory.CreateDirectory(dest); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    e.ExtractToFile(dest, true);
                }
            }
            File.Delete(tmp);
            if (!File.Exists(Path.Combine(nw, "RetroSpy.exe")))
            {
                Directory.Delete(nw, true);
                throw new InvalidOperationException("The download had no RetroSpy.exe, so it wasn't installed.");
            }

            // the swap, once this RetroSpy has closed (its files are in use until then)
            int pid = Environment.ProcessId;
            string script = Path.Combine(Path.GetTempPath(), "retrospy-update-" + pid.ToString(CultureInfo.InvariantCulture) + ".cmd");
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("cd /d \"" + parent + "\"");
            sb.AppendLine(":wait");
            sb.AppendLine("tasklist /FI \"PID eq " + pid + "\" 2>nul | find \"" + pid + "\" >nul && (timeout /t 1 /nobreak >nul & goto wait)");
            sb.AppendLine("if exist \"" + old + "\" rd /s /q \"" + old + "\"");
            sb.AppendLine("set n=0");
            sb.AppendLine(":move");
            sb.AppendLine("move \"" + folder + "\" \"" + old + "\" >nul 2>&1 && goto moved");
            sb.AppendLine("set /a n+=1");
            sb.AppendLine("if %n% lss 15 (timeout /t 1 /nobreak >nul & goto move)");
            sb.AppendLine("start \"\" \"" + Path.Combine(folder, "RetroSpy.exe") + "\"");   // (couldn't: the old one again)
            sb.AppendLine("goto end");
            sb.AppendLine(":moved");
            sb.AppendLine("move \"" + nw + "\" \"" + folder + "\" >nul 2>&1 || (move \"" + old + "\" \"" + folder + "\" >nul 2>&1)");
            sb.AppendLine("start \"\" \"" + Path.Combine(folder, "RetroSpy.exe") + "\"");
            sb.AppendLine("timeout /t 3 /nobreak >nul");
            sb.AppendLine("if exist \"" + Path.Combine(folder, "RetroSpy.exe") + "\" rd /s /q \"" + old + "\"");
            sb.AppendLine(":end");
            sb.AppendLine("del \"%~f0\"");
            File.WriteAllText(script, sb.ToString());
            Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + script + "\"")
            {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = parent,
            });
            progress.Report("Restarting...");
        }
    }
}
