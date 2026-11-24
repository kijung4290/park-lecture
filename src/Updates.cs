using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ParkLecture.UpdateSupport;

namespace ParkLecture
{
    internal sealed class AvailableUpdate
    {
        internal UpdateManifest Manifest;
        internal UpdateAsset Package;
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
            return result;
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
        internal static async Task<AvailableUpdate> Check(CancellationToken token)
        {
            string latest = ReleasesUrl + "/latest/download/";
            byte[] bytes = await Download(latest + "update.json", 16384, token, null).ConfigureAwait(false);
            byte[] signature = Convert.FromBase64String(Encoding.UTF8.GetString(await Download(latest + "update.json.sig", 4096, token, null).ConfigureAwait(false)).Trim());
            var manifest = UpdatePackage.Read(bytes, signature, UpdatePackage.PublicKey);
            if (UpdatePackage.ParseVersion(manifest.version) <= UpdatePackage.ParseVersion(CurrentVersion)) return null;
            var package = manifest.PackageFor(UpdateInstaller.IsInstalled(AppDomain.CurrentDomain.BaseDirectory));
            return new AvailableUpdate
            {
                Manifest = manifest, Package = package, ManifestBytes = bytes, Signature = signature,
                PackageUrl = ReleasesUrl + "/download/v" + manifest.version + "/" + package.asset
            };
        }
        internal static async Task<string> Prepare(AvailableUpdate update, CancellationToken token, IProgress<int> progress)
        {
            string target = AppDomain.CurrentDomain.BaseDirectory; UpdateInstaller.CheckWritable(target);
            string stage = Path.Combine(Program.DataDir, "Updates", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
            try
            {
                byte[] bytes = await Download(update.PackageUrl, update.Package.size, token, progress).ConfigureAwait(false);
                string package = Path.Combine(stage, update.Package.asset);
                File.WriteAllBytes(package, bytes);
                UpdatePackage.Verify(package, update.Package);
                token.ThrowIfCancellationRequested();
                File.WriteAllBytes(Path.Combine(stage, "update.json"), update.ManifestBytes);
                File.WriteAllText(Path.Combine(stage, "update.json.sig"), Convert.ToBase64String(update.Signature));
                File.Copy(Path.Combine(target, "ParkLecture.Updater.exe"), Path.Combine(stage, "ParkLecture.Updater.exe"));
                return stage;
            }
            catch { Discard(stage); throw; }
        }

        internal static void Discard(string stage)
        {
            try { Directory.Delete(stage, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
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
