# Crawler

A small command-line broken-link checker. Give it a URL and it:

1. Downloads the page and extracts every `<a href>` link (relative links are
   resolved against the page URL; non-web schemes such as `mailto:` and
   `javascript:` are skipped).
2. Requests each unique link (following redirects, with a 10-second timeout).
3. Writes every broken link — non-success status code or no response — to a
   CSV file, one URL per line.

Note: it checks the links on the single page you give it; it does not follow
links to crawl the whole site.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or newer)

Runs on Windows, macOS, and Linux.

## Build

```
dotnet build
```

## Run

```
dotnet run -- <url> [output-csv-path]
```

Examples:

```
dotnet run -- https://example.com/
dotnet run -- https://example.com/ broken-links.csv
```

If no output path is given, results are written to `output.csv` in the
current directory. Progress for each tested link is printed to the console.

## Dependencies

- [HtmlAgilityPack](https://www.nuget.org/packages/HtmlAgilityPack) — HTML parsing (restored automatically by `dotnet build`)
