using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Xml;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ModlinksShaVerifier
{
    internal static class Program
    {
        private static readonly HttpClient _Client = new()
        {
            Timeout = TimeSpan.FromMinutes(30)
        };

        private static string ShaToString(byte[] hash)
            => BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

        private static async Task<bool> CheckLink(Manifest m, Link link)
        {
            using var sha = SHA256.Create();

            Stream stream;

            try
            {
                stream = await _Client.GetStreamAsync(link.URL);
            }
            catch (HttpRequestException e)
            {
                WriteError("Check", $"Request failed for {m.Name} - {link.URL}! {e.StatusCode}");
                return false;
            }

            string shasum = ShaToString(await sha.ComputeHashAsync(stream));

            if (shasum == link.SHA256.ToLowerInvariant())
                return true;

            WriteError("Check", $"Hash mismatch of {m.Name} in link {link.URL}. Expected value from modlinks: {link.SHA256}, Actual value: {shasum}");

            return false;
        }

        private static async Task<bool> CheckSingleSha(Manifest m)
        {
            Console.WriteLine($"Checking '{m.Name}'");

            var res = await Task.WhenAll(m.Links.AsEnumerable().Select(x => CheckLink(m, x)));

            return res.All(x => x);
        }

        internal static async Task<int> Main(string[] args)
        {
            var sw = new Stopwatch();
            sw.Start();

            if (args.Length != 1)
            {
                await Console.Error.WriteLineAsync("Usage: ModlinksShaVerifier [FILE]");
                return 1;
            }

            var path = args[0];

            if (!File.Exists(path))
            {
                await Console.Error.WriteLineAsync($"Unable to access {path}! Does it exist?");
                return 1;
            }

            var reader = XmlReader.Create(path, new XmlReaderSettings {Async = true});

            var serializer = new XmlSerializer(typeof(Manifest));
            
            List<Task<bool>> checks = new();

            while (await reader.ReadAsync())
            {
                if (reader.NodeType != XmlNodeType.Element)
                    continue;

                if (reader.Name != "Manifest")
                    continue;

                var manifest = (Manifest?) serializer.Deserialize(reader) ?? throw new InvalidDataException();

                checks.Add(CheckSingleSha(manifest));
            }

            var res = await Task.WhenAll(checks);

            sw.Stop();

            Console.WriteLine($"Completed in {sw.ElapsedMilliseconds}ms.");

            // If they're not all correct, error.
            return !res.All(x => x) ? 1 : 0;
        }

        private static void WriteError(string title, string message) =>
            Console.WriteLine($"::error title={title}::{message}");
    }
}
