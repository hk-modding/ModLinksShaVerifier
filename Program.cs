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
        private static readonly HttpClient _Client = new();

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

            if (args.Length != 2)
            {
                await Console.Error.WriteLineAsync("Usage: ModlinksShaVerifier [CURRENT_FILE] [INCOMING_FILE]");
                return 1;
            }

            string currentPath = args[0];
            
            if (!File.Exists(currentPath))
            {
                await Console.Error.WriteLineAsync($"Unable to access current XML file {currentPath}! Does it exist?");
                return 1;
            }

            var incomingPath = args[1];
            
            if (!File.Exists(incomingPath))
            {
                await Console.Error.WriteLineAsync($"Unable to access incoming XML file {incomingPath}! Does it exist?");
                return 1;
            }
            
            string currentContents = await File.ReadAllTextAsync(currentPath);
            var currentDocument = new XmlDocument();
            currentDocument.LoadXml(currentContents);
            var modLinksNode = currentDocument.DocumentElement;

            if (modLinksNode == null)
            {
                await Console.Error.WriteLineAsync("Could not get document element of current XML file!");
                return 1;
            }

            var currentManifests = modLinksNode.ChildNodes;
            
            var incomingReader = XmlReader.Create(incomingPath, new XmlReaderSettings {Async = true});

            var serializer = new XmlSerializer(typeof(Manifest));

            List<Task<bool>> checks = new();

            while (await incomingReader.ReadAsync())
            {
                if (incomingReader.NodeType != XmlNodeType.Element)
                    continue;

                if (incomingReader.Name != "Manifest")
                    continue;

                var incomingManifest = (Manifest?) serializer.Deserialize(incomingReader) ?? throw new InvalidDataException();

                bool matching = false;
                for (int i = 0; i < currentManifests.Count; i++)
                {
                    var xmlNode = currentManifests[i] ?? throw new InvalidDataException();
                    var stream = new MemoryStream();
                    var writer = new StreamWriter(stream);
                    await writer.WriteAsync(xmlNode.OuterXml);
                    await writer.FlushAsync();

                    stream.Position = 0;
                    
                    var currentManifest = (Manifest?) serializer.Deserialize(stream) ?? throw new InvalidDataException();
                    
                    if (currentManifest.Name == incomingManifest.Name &&
                        currentManifest.Description == incomingManifest.Description &&
                        currentManifest.Dependencies == null && incomingManifest.Dependencies == null || currentManifest.Dependencies != null && currentManifest.Dependencies.SequenceEqual(incomingManifest.Dependencies) &&
                        currentManifest.Links.Equals(incomingManifest.Links))
                    {
                        matching = true;
                        break;
                    }
                }

                if (matching)
                    continue;

                checks.Add(CheckSingleSha(incomingManifest));
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
