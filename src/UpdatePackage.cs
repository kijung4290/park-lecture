using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace ParkLecture.UpdateSupport
{
    public class UpdateAsset
    {
        public string asset, sha256;
        public long size;
    }

    public sealed class UpdateManifest : UpdateAsset
    {
        public int schema;
        public string version;
        public UpdateAsset setup;

        internal UpdateAsset PackageFor(bool installed)
        {
            if (!installed) return this;
            if (setup == null) throw new InvalidDataException("The release has no signed Windows installer.");
            return setup;
        }
    }

    internal static class UpdatePackage
    {
        internal static readonly string[] Files = { "ParkLecture.exe", "ParkLecture.exe.config", "ParkLecture.Updater.exe", "README.md" };
        internal const string SetupName = "ParkLecture-Setup.exe";
        internal const long MaxPackageSize = 100 * 1024 * 1024;
        internal static string PublicKey
        {
            get { using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ParkLecture.UpdateKey.xml")) using (var reader = new StreamReader(stream)) return reader.ReadToEnd(); }
        }
        internal static Version ParseVersion(string text)
        {
            if (text == null || !Regex.IsMatch(text, @"^\d+\.\d+\.\d+$")) throw new InvalidDataException("Invalid update version.");
            return new Version(text);
        }
        internal static UpdateManifest Read(byte[] bytes, byte[] signature, string publicKey)
        {
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false; rsa.FromXmlString(publicKey);
                if (!rsa.VerifyData(bytes, CryptoConfig.MapNameToOID("SHA256"), signature)) throw new InvalidDataException("Update signature verification failed.");
            }
            var manifest = new JavaScriptSerializer().Deserialize<UpdateManifest>(Encoding.UTF8.GetString(bytes));
            if (manifest == null || manifest.schema != 1) throw new InvalidDataException("Unsupported update manifest.");
            ParseVersion(manifest.version);
            ValidateAsset(manifest, "ParkLecture-" + manifest.version + "-portable.zip");
            if (manifest.setup != null) ValidateAsset(manifest.setup, SetupName);
            return manifest;
        }
        private static void ValidateAsset(UpdateAsset package, string expectedName)
        {
            if (package.asset != expectedName || package.sha256 == null || !Regex.IsMatch(package.sha256, "^[a-fA-F0-9]{64}$") || package.size <= 0 || package.size > MaxPackageSize)
                throw new InvalidDataException("Invalid update package metadata.");
        }
        internal static void Verify(string path, UpdateAsset package)
        {
            using (var stream = File.OpenRead(path)) Verify(stream, package);
        }
        internal static void Verify(Stream stream, UpdateAsset package)
        {
            if (stream.Length == package.size)
                using (var sha = SHA256.Create())
                {
                    string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                    if (hash.Equals(package.sha256, StringComparison.OrdinalIgnoreCase)) return;
                }
            throw new InvalidDataException("Update download is incomplete or has been changed.");
        }
        internal static void Extract(string package, string destination, UpdateManifest manifest)
        {
            Directory.CreateDirectory(destination);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long expanded = 0;
            using (var stream = File.OpenRead(package))
            {
                Verify(stream, manifest);
                stream.Position = 0;
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (Array.IndexOf(Files, entry.FullName) < 0 || !seen.Add(entry.FullName)) throw new InvalidDataException("Unexpected or duplicate file in update archive.");
                        expanded += entry.Length;
                        if (expanded > 200 * 1024 * 1024 || entry.Length <= 0) throw new InvalidDataException("Invalid expanded package size.");
                        entry.ExtractToFile(Path.Combine(destination, entry.FullName));
                    }
                }
            }
            if (seen.Count != Files.Length) throw new InvalidDataException("Update package is missing files.");
            foreach (string file in new[] { Files[0], Files[2] })
            {
                Version actual = AssemblyName.GetAssemblyName(Path.Combine(destination, file)).Version;
                if (actual.ToString(3) != manifest.version) throw new InvalidDataException("Update executable version does not match its manifest.");
            }
        }
    }
}
