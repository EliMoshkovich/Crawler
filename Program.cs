using HtmlAgilityPack;

namespace BrokenLinkChecker;

/// <summary>
/// Command-line broken-link checker: fetches a page, tests every link on it,
/// and writes the broken ones to a CSV file.
/// </summary>
internal static class Program
{
    private const int MaxConcurrentRequests = 8;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private static async Task<int> Main(string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            Console.Error.WriteLine("Usage: broken-link-checker <url> [output-csv-path]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Fetches the given page, tests every link on it, and writes the");
            Console.Error.WriteLine("broken ones to a CSV file (default: output.csv in the current directory).");
            return 2;
        }

        if (!Uri.TryCreate(args[0], UriKind.Absolute, out Uri? baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            Console.Error.WriteLine($"Error: '{args[0]}' is not a valid http/https URL.");
            return 2;
        }

        string outputPath = args.Length == 2 ? args[1] : "output.csv";

        using var client = new HttpClient { Timeout = RequestTimeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; broken-link-checker/1.0)");

        var doc = new HtmlDocument();
        try
        {
            doc.LoadHtml(await client.GetStringAsync(baseUri));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Console.Error.WriteLine($"Error: could not load {baseUri}: {ex.Message}");
            return 1;
        }

        IReadOnlyList<string> links = CollectLinks(doc, baseUri);
        Console.WriteLine($"Found {links.Count} unique links on {baseUri}");

        IReadOnlyList<LinkResult> results = await CheckLinksAsync(client, links);
        LinkResult[] broken = results.Where(r => r.IsBroken).ToArray();

        try
        {
            WriteCsv(outputPath, broken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Error: could not write {outputPath}: {ex.Message}");
            return 1;
        }

        Console.WriteLine($"Done: {broken.Length} broken link(s) written to {outputPath}");
        return 0;
    }

    /// <summary>
    /// Collects the unique absolute http(s) links on the page. Relative links are
    /// resolved against the page URL; non-web schemes (mailto:, javascript:,
    /// tel:, ...) are skipped, and #fragments are dropped so that same-page
    /// anchors are only tested once.
    /// </summary>
    private static IReadOnlyList<string> CollectLinks(HtmlDocument doc, Uri baseUri)
    {
        var links = new List<string>();
        var seen = new HashSet<string>();

        foreach (HtmlNode anchor in doc.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
        {
            // DeEntitize: HtmlAgilityPack leaves entities such as &amp; encoded
            // in attribute values.
            string href = HtmlEntity.DeEntitize(anchor.GetAttributeValue("href", string.Empty));
            if (string.IsNullOrWhiteSpace(href))
                continue;
            if (!Uri.TryCreate(baseUri, href, out Uri? absolute))
                continue;
            if (absolute.Scheme != Uri.UriSchemeHttp && absolute.Scheme != Uri.UriSchemeHttps)
                continue;

            string url = absolute.GetLeftPart(UriPartial.Query);
            if (seen.Add(url))
                links.Add(url);
        }

        return links;
    }

    /// <summary>
    /// Checks all links concurrently (bounded by <see cref="MaxConcurrentRequests"/>),
    /// printing one result line per link. Results keep the input order.
    /// </summary>
    private static async Task<IReadOnlyList<LinkResult>> CheckLinksAsync(HttpClient client, IReadOnlyList<string> links)
    {
        var results = new LinkResult[links.Count];
        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentRequests };

        await Parallel.ForEachAsync(Enumerable.Range(0, links.Count), options, async (i, _) =>
        {
            LinkResult result = await CheckLinkAsync(client, links[i]);
            results[i] = result;
            Console.WriteLine(result.IsBroken
                ? $"BROKEN  {result.Url}  ({result.Status})"
                : $"OK      {result.Url}");
        });

        return results;
    }

    private static async Task<LinkResult> CheckLinkAsync(HttpClient client, string url)
    {
        try
        {
            // GET rather than HEAD (some servers reject HEAD), but only the
            // response headers are read -- the body is never downloaded.
            using HttpResponseMessage response =
                await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            return new LinkResult(url, ((int)response.StatusCode).ToString(), !response.IsSuccessStatusCode);
        }
        catch (TaskCanceledException)
        {
            return new LinkResult(url, "timed out", IsBroken: true);
        }
        catch (HttpRequestException)
        {
            return new LinkResult(url, "no response", IsBroken: true);
        }
    }

    private static void WriteCsv(string path, IEnumerable<LinkResult> brokenLinks)
    {
        IEnumerable<string> lines = brokenLinks
            .Select(r => $"{CsvField(r.Url)},{CsvField(r.Status)}")
            .Prepend("url,status");
        File.WriteAllLines(path, lines);
    }

    /// <summary>Quotes a CSV field if it contains a comma, quote, or line break.</summary>
    private static string CsvField(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private sealed record LinkResult(string Url, string Status, bool IsBroken);
}
