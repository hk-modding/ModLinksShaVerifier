using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Xml;
using System.Security.Cryptography;

namespace ModlinksShaVerifier
{
    internal class ManifestData
    {
        public string Name = "";
        public string Sha256 = "";
        public string Url = "";
    }

    internal class Program
    {
        private static bool _atLeast1Error = false;

        private static string GetShaOfBytes(ref byte[] data)
        {
            SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLower();
        }
        
        private static void CheckSingleSha(object? data)
        {
            ManifestData actualData = (data as ManifestData)!;
            print($"Checking '{actualData.Name}'");

            string downloadedSha;
            using (var client = new WebClient())
            {
                byte[] downloadBytes = client.DownloadData(actualData.Url);
                downloadedSha = GetShaOfBytes(ref downloadBytes);
            }

            if (downloadedSha != actualData.Sha256)
            {
                printError("Check", $"Hash mismatch with '{actualData.Name}'. Expected: {actualData.Sha256}, Downloaded: {downloadedSha}!");
                _atLeast1Error = true;
            }
            else
            {
                print($"Hash of '{actualData.Name}' matches!");
            }
        }
        
        internal static int Main(string[] args)
        {
            #region Start stopwatch

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            #endregion

            #region Check argument

            if (args.Length != 1)
            {
                printError("Startup", "call like `.\\ModLinksShaVerifier.exe path_to_xml_file`!");
                return 1;
            }
            if (!File.Exists(Path.GetFullPath(args[0])))
            {
                printError("Startup", "call like `.\\ModLinksShaVerifier.exe path_to_xml_file`!");
                return 1;
            }

            #endregion

            #region Load XML file

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(Path.GetFullPath(args[0]));
            XmlNamespaceManager xmlNsMgr = new XmlNamespaceManager(xmlDoc.NameTable);
            xmlNsMgr.AddNamespace("mm", "https://github.com/HollowKnight-Modding/HollowKnight.ModLinks/HollowKnight.ModManager");

            #endregion

            #region Test type of XML file

            bool isModLinks = xmlDoc.DocumentElement!.SelectNodes("/mm:ModLinks", xmlNsMgr)!.Count > 0;
            bool isApiLinks = xmlDoc.DocumentElement.SelectNodes("/mm:ApiLinks", xmlNsMgr)!.Count > 0;

            #endregion

            #region Traverse XML file

            List<ManifestData> dataToCheck = new List<ManifestData>();
            Dictionary<string, string> linksAndNameSuffixes = new Dictionary<string, string>()
            {
                { "mm:Link", "" },
                { "mm:Linux", " (Linux)" },
                { "mm:Mac", " (Mac)" },
                { "mm:Windows", " (Windows)" },
            };
            foreach (XmlNode manifestNode in xmlDoc.SelectNodes("/mm:ModLinks/mm:Manifest", xmlNsMgr)!)
            {
                foreach (var pathNamePair in linksAndNameSuffixes)
                {
                    if (manifestNode.SelectSingleNode(pathNamePair.Key, xmlNsMgr) != null)
                    {
                        XmlNode? linkNode = manifestNode.SelectSingleNode(pathNamePair.Key, xmlNsMgr);
                        ManifestData data = new ManifestData();
                        data.Name = manifestNode.SelectSingleNode("mm:Name", xmlNsMgr)!.InnerText + pathNamePair.Value;
                        data.Sha256 = linkNode!.Attributes!["SHA256"]!.InnerText.ToLower();
                        data.Url = linkNode.InnerText;
                        dataToCheck.Add(data);
                    }
                }
            }
            foreach (XmlNode manifestNode in xmlDoc.SelectNodes("/mm:ApiLinks/mm:Manifest", xmlNsMgr)!)
            {
                foreach (var pathNamePair in linksAndNameSuffixes)
                {
                    if (manifestNode.SelectSingleNode(pathNamePair.Key, xmlNsMgr) != null)
                    {
                        XmlNode? linkNode = manifestNode.SelectSingleNode(pathNamePair.Key, xmlNsMgr);
                        ManifestData data = new ManifestData();
                        data.Name = "Modding API" + pathNamePair.Value;
                        data.Sha256 = linkNode!.Attributes!["SHA256"]!.InnerText.ToLower();
                        data.Url = linkNode.InnerText;
                        dataToCheck.Add(data);
                    }
                }
            }

            #endregion

            #region Check SHA of downloads

            List<Thread> threads = new List<Thread>();
            for (int i = 0; i < dataToCheck.Count; i++)
            {
                threads.Add(new Thread(CheckSingleSha));
            }
            for (int i = 0; i < dataToCheck.Count; i++)
            {
                threads[i].Start(dataToCheck[i]);
            }
            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            #endregion

            #region Last print

            if (!_atLeast1Error)
            {
                print("No mismatches to report!");
            }

            #endregion

            #region Stop stopwatch

            stopWatch.Stop();
            print($"The entire operation took {stopWatch.Elapsed.TotalSeconds} seconds!");

            #endregion
            return _atLeast1Error ? 1 : 0;
        }

        static void printError(string title, string message)
        {
            Console.WriteLine($"::error title={title}::{message}");
        }
        static void print(string message)
        {
            Console.WriteLine(message);
        }
    }
}