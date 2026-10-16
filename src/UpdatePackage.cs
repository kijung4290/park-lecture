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
    public sealed class UpdateManifest
    {
        public int schema;
        public string version, asset, sha256;
        public long size;
    }

    internal static class UpdatePackage
    {
        internal static readonly string[] Files = { "ParkLecture.exe", "ParkLecture.exe.config", "ParkLecture.Updater.exe", "README.md" };
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
            if (manifest.asset != "ParkLecture-" + manifest.version + "-portable.zip" || manifest.sha256 == null || !Regex.IsMatch(manifest.sha256, "^[a-fA-F0-9]{64}$") || manifest.size <= 0 || manifest.size > MaxPackageSize) throw new InvalidDataException("Invalid update package metadata.");
            return manifest;
        }
        internal static string Hash(string path)
        { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        internal static void Verify(string path, UpdateManifest manifest)
        {
            if (new FileInfo(path).Length != manifest.size || !Hash(path).Equals(manifest.sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update download is incomplete or has been changed.");
        }
        internal static void Extract(string package, string destination, UpdateManifest manifest)
        {
            Verify(package, manifest); Directory.CreateDirectory(destination);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long expanded = 0;
            using (var zip = ZipFile.OpenRead(package))
            {
                foreach (var entry in zip.Entries)
                {
                    if (Array.IndexOf(Files, entry.FullName) < 0 || !seen.Add(entry.FullName)) throw new InvalidDataException("Unexpected or duplicate file in update archive.");
                    expanded += entry.Length;
                    if (expanded > 200 * 1024 * 1024 || entry.Length <= 0) throw new InvalidDataException("Invalid expanded package size.");
                    entry.ExtractToFile(Path.Combine(destination, entry.FullName));
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
