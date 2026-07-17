# Crawler

A command-line broken-link checker written in C# on .NET 8. Point it at a web
page and it reports every link on that page that no longer works.

## How it works

1. Downloads the page and extracts every `<a href>` link. Relative links are
   resolved against the page URL, HTML entities in URLs are decoded, non-web
   schemes (`mailto:`, `javascript:`, `tel:`, ...) are skipped, and duplicate
   and `#fragment` variants of the same URL are checked only once.
2. Requests each unique link — up to 8 in parallel, following redirects, with
   a 10-second timeout. Only the response headers are read; bodies are never
   downloaded.
3. Writes every broken link — HTTP error status, timeout, or no response — to
   a CSV file with `url,status` columns.

It checks the links on the single page you give it; it does not crawl the
whole site.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or newer)

Runs on Windows, macOS, and Linux.

## Usage

```
dotnet run -- <url> [output-csv-path]
```

If no output path is given, results are written to `output.csv` in the
current directory.

### Example

```
$ dotnet run -- http://www.deadlinkcity.com/
Found 47 unique links on http://www.deadlinkcity.com/
OK      http://www.deadlinkcity.com/errorlist.asp
BROKEN  http://www.deadlinkcity.com/error-page.asp?e=404  (404)
BROKEN  http://www.deadlinkcity.com/error-page.asp?e=500  (500)
BROKEN  http://www.domaindoesnot.exist/  (no response)
...
Done: 43 broken link(s) written to output.csv
```

`output.csv`:

```csv
url,status
http://www.deadlinkcity.com/error-page.asp?e=404,404
http://www.deadlinkcity.com/error-page.asp?e=500,500
http://www.domaindoesnot.exist/,no response
```

## Exit codes

| Code | Meaning                                                      |
|------|--------------------------------------------------------------|
| 0    | Run completed (whether or not broken links were found)       |
| 1    | The page could not be loaded, or the CSV could not be written|
| 2    | Invalid arguments                                            |

## Dependencies

- [HtmlAgilityPack](https://www.nuget.org/packages/HtmlAgilityPack) — HTML
  parsing (restored automatically by `dotnet build`)
