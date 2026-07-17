using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Crawler
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (args.Length < 1 || args.Length > 2)
            {
                Console.Error.WriteLine("Usage: Crawler <url> [output-csv-path]");
                Console.Error.WriteLine();
                Console.Error.WriteLine("Fetches the given page, tests every link on it, and writes the");
                Console.Error.WriteLine("broken ones to a CSV file (default: output.csv in the current directory).");
                return 2;
            }

            if (!Uri.TryCreate(args[0], UriKind.Absolute, out Uri baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            {
                Console.Error.WriteLine($"Error: '{args[0]}' is not a valid http/https URL.");
                return 2;
            }

            string outputPath = args.Length == 2 ? args[1] : "output.csv";

            using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; Crawler/1.0)");

            // Load the page and parse its HTML.
            HtmlDocument doc = new HtmlDocument();
            try
            {
                doc.LoadHtml(await client.GetStringAsync(baseUri));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                Console.Error.WriteLine($"Error: could not load {baseUri}: {ex.Message}");
                return 1;
            }

            // Collect the unique absolute http(s) links on the page.
            // Relative links are resolved against the page URL; non-web schemes
            // (mailto:, javascript:, tel:, ...) are skipped.
            var links = new List<string>();
            var seen = new HashSet<string>();
            foreach (HtmlNode anchor in doc.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
            {
                string href = anchor.GetAttributeValue("href", string.Empty);
                if (string.IsNullOrWhiteSpace(href))
                    continue;
                if (!Uri.TryCreate(baseUri, href, out Uri absolute))
                    continue;
                if (absolute.Scheme != Uri.UriSchemeHttp && absolute.Scheme != Uri.UriSchemeHttps)
                    continue;

                // Drop the #fragment so anchors on the same page are tested once.
                string url = absolute.GetLeftPart(UriPartial.Query);
                if (seen.Add(url))
                    links.Add(url);
            }

            Console.WriteLine($"Found {links.Count} unique links on {baseUri}");

            // Test each link; anything that fails or returns a non-success
            // status code is recorded as broken.
            var badLinks = new List<string>();
            foreach (string link in links)
            {
                Console.Write($"Testing: {link} ... ");
                try
                {
                    using HttpResponseMessage response = await client.GetAsync(link);
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("OK");
                    }
                    else
                    {
                        Console.WriteLine($"BROKEN ({(int)response.StatusCode})");
                        badLinks.Add(link);
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    Console.WriteLine("BROKEN (no response)");
                    badLinks.Add(link);
                }
            }

            // Write the broken links to the CSV file, one URL per line.
            try
            {
                File.WriteAllLines(outputPath, badLinks);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Error: could not write {outputPath}: {ex.Message}");
                return 1;
            }

            Console.WriteLine($"Done: {badLinks.Count} broken link(s) written to {outputPath}");
            return 0;
        }
    }
}
