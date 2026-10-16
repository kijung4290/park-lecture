using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using ParkLecture.UpdateSupport;

namespace ParkLecture
{
    internal sealed class AvailableUpdate
    {
        internal UpdateManifest Manifest;
        internal byte[] ManifestBytes, Signature;
        internal string PackageUrl;
    }
    internal static class Updates
    {
        internal const string Repository = "kijung4290/park-lecture";
        internal const string ReleasesUrl = "https://github.com/" + Repository + "/releases";
        internal static string CurrentVersion { get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(3); } }
        private static readonly HttpClient client = CreateClient();
        private static HttpClient CreateClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var result = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            result.DefaultRequestHeaders.UserAgent.ParseAdd("ParkLecture/" + CurrentVersion);
            result.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json"); return result;
        }
        private static async Task<byte[]> Download(string url, long limit, CancellationToken token, IProgress<int> progress)
        {
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                long length = response.Content.Headers.ContentLength ?? 0;
                if (length > limit) throw new InvalidDataException("Update response is too large.");
                using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false)) using (var output = new MemoryStream())
                {
                    var buffer = new byte[65536]; int count;
                    while ((count = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                    {
                        if (output.Length + count > limit) throw new InvalidDataException("Update response is too large.");
                        output.Write(buffer, 0, count);
                        if (progress != null && length > 0) progress.Report((int)(output.Length * 100 / length));
                    }
                    return output.ToArray();
                }
            }
        }
        internal static async Task<AvailableUpdate> Check(CancellationToken token, string installedVersion = null)
        {
            byte[] releaseBytes;
            try { releaseBytes = await Download("https://api.github.com/repos/" + Repository + "/releases/latest", 1024 * 1024, token, null).ConfigureAwait(false); }
            catch (HttpRequestException) { throw; }
            var release = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(System.Text.Encoding.UTF8.GetString(releaseBytes));
            if ((bool)release["draft"] || (bool)release["prerelease"]) return null;
            string tag = (string)release["tag_name"];
            if (tag == null || !tag.StartsWith("v", StringComparison.Ordinal)) throw new InvalidDataException("Invalid release tag.");
            Version version = UpdatePackage.ParseVersion(tag.Substring(1));
            if (version <= UpdatePackage.ParseVersion(installedVersion ?? CurrentVersion)) return null;
            var urls = new Dictionary<string, string>();
            foreach (Dictionary<string, object> asset in (IEnumerable)release["assets"])
            {
                string name = (string)asset["name"], url = (string)asset["browser_download_url"];
                string expected = "https://github.com/" + Repository + "/releases/download/" + tag + "/" + name;
                if (url == expected) urls[name] = url;
            }
            if (!urls.ContainsKey("update.json") || !urls.ContainsKey("update.json.sig")) throw new InvalidDataException("Release has no signed update manifest.");
            byte[] bytes = await Download(urls["update.json"], 16384, token, null).ConfigureAwait(false);
            byte[] signature = Convert.FromBase64String(System.Text.Encoding.UTF8.GetString(await Download(urls["update.json.sig"], 4096, token, null).ConfigureAwait(false)).Trim());
            var manifest = UpdatePackage.Read(bytes, signature, UpdatePackage.PublicKey);
            if (manifest.version != version.ToString(3) || !urls.ContainsKey(manifest.asset)) throw new InvalidDataException("Release and signed package do not match.");
            return new AvailableUpdate { Manifest = manifest, ManifestBytes = bytes, Signature = signature, PackageUrl = urls[manifest.asset] };
        }
        internal static async Task<string> Prepare(AvailableUpdate update, CancellationToken token, IProgress<int> progress)
        {
            string target = AppDomain.CurrentDomain.BaseDirectory; UpdateInstaller.CheckWritable(target);
            string stage = Path.Combine(Program.DataDir, "Updates", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
            byte[] bytes = await Download(update.PackageUrl, update.Manifest.size, token, progress).ConfigureAwait(false);
            string package = Path.Combine(stage, update.Manifest.asset); File.WriteAllBytes(package, bytes);
            UpdatePackage.Verify(package, update.Manifest);
            File.WriteAllBytes(Path.Combine(stage, "update.json"), update.ManifestBytes);
            File.WriteAllText(Path.Combine(stage, "update.json.sig"), Convert.ToBase64String(update.Signature));
            File.Copy(Path.Combine(target, "ParkLecture.Updater.exe"), Path.Combine(stage, "ParkLecture.Updater.exe"));
            return stage;
        }
        internal static void Apply(string stage)
        {
            using (var parent = Process.GetCurrentProcess())
            {
                string args = MainForm.Quote(stage) + " " + MainForm.Quote(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)) + " " + parent.Id + " " + parent.StartTime.ToUniversalTime().Ticks;
                Process.Start(new ProcessStartInfo(Path.Combine(stage, "ParkLecture.Updater.exe"), args) { UseShellExecute = false, WorkingDirectory = stage, CreateNoWindow = true });
            }
        }
    }
}
