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
        private static readonly HttpClient _Client = new(
            new HttpClientHandler
            {
                MaxConnectionsPerServer = 16,
            }
        );

        private static async Task<bool> CheckLink(Manifest m, Link link)
        {
            using var sha = SHA256.Create();

            try
            {
                await using Stream stream = await _Client.GetStreamAsync(link.URL);

                var sw = new Stopwatch();
                sw.Start();
                
                string shasum = Convert.ToHexString(await sha.ComputeHashAsync(stream));
                
                Console.WriteLine($"Took {sw.Elapsed.ToString()} to calc sum for {m.Name}");

                if (shasum.Equals(link.SHA256, StringComparison.InvariantCultureIgnoreCase))
                    return true;

                WriteError(
                    "Check",
                    $"Hash mismatch of {m.Name} in link {link.URL}. Expected value from modlinks: {link.SHA256}, Actual value: {shasum}"
                );
            }
            catch (HttpRequestException e)
            {
                WriteError("Check", $"Request failed for {m.Name} - {link.URL}! {e.StatusCode}");
            }

            return false;
        }

        private static async Task<bool> CheckSingleSha(Manifest m)
        {
            Console.WriteLine($"Checking '{m.Name}'");

            var res = await Task.WhenAll(m.Links.AsEnumerable().Select(x => CheckLink(m, x)));

            return res.All(x => x);
        }

        private static async Task ReadManifests(string path, XmlSerializer s, Action<Manifest> f)
        {
            using var reader = XmlReader.Create(path, new XmlReaderSettings { Async = true });

            while (await reader.ReadAsync())
            {
                if (reader.NodeType is not XmlNodeType.Element)
                    continue;

                if (reader.Name is not nameof(Manifest))
                    continue;

                f(s.Deserialize(reader) as Manifest ?? throw new XmlException("Unable to deserialize manifest!"));
            }
        }

        internal static async Task<int> Main(string[] args)
        {
            var sw = new Stopwatch();
            sw.Start();

            if (args is not [var prevPath, var currPath])
            {
                await Console.Error.WriteLineAsync("Usage: ModlinksShaVerifier [CURRENT_FILE] [INCOMING_FILE]");
                return 1;
            }

            if (!File.Exists(prevPath))
            {
                await Console.Error.WriteLineAsync($"Unable to access previous XML file {prevPath}! Does it exist?");
                return 1;
            }

            if (!File.Exists(currPath))
            {
                await Console.Error.WriteLineAsync($"Unable to access new XML file {currPath}! Does it exist?");
                return 1;
            }

            var serializer = new XmlSerializer(typeof(Manifest));

            Dictionary<string, Links> prev = new();

            await ReadManifests(
                prevPath,
                serializer,
                m => prev.Add(m.Name, m.Links)
            );

            List<Task<bool>> checks = [];

            await ReadManifests(
                currPath,
                serializer,
                m =>
                {
                    // Check the manifest if it either wasn't present before *or* its contents changed.
                    if (!prev.TryGetValue(m.Name, out Links? links) || links != m.Links)
                        checks.Add(CheckSingleSha(m));
                }
            );

            var res = await Task.WhenAll(checks);

            sw.Stop();

            Console.WriteLine($"Checked {checks.Count} manifests in {sw.ElapsedMilliseconds}ms.");

            // If they're not all correct, error.
            return !res.All(x => x) ? 1 : 0;
        }

        private static void WriteError(string title, string message) =>
            Console.WriteLine($"::error title={title}::{message}");
    }
}