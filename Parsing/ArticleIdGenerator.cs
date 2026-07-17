using RSS_Reader.Models;
using System.Diagnostics.Metrics;
using System.Security.Cryptography; //for md5,...
using System.Text; // for encoding, ...

namespace RSS_Reader.Parsing;

public static class ArticleIdGenerator //makes sure the same article always gets the same id 
{

    private static readonly string[] TrackingParamPrefixes = { "utm_", "fbclid", "gclid", "mc_cid", "mc_eid", "ref" }; //stores paraemters that should be ignored as they dont change the article, only tracking, so we ignore it/remove it 

    //deterministic so the same real-world article always maps to the same Id across refreshes,
    //even though RSS/Atom/JSON Feed items don't reliably give us a stable id of their own
    public static Guid Generate(Guid feedId, string link, string title) // the job is give the same article the same id
    {
        var key = !string.IsNullOrWhiteSpace(link)
            ? $"{feedId}|{NormalizeLink(link)}" //normalise remove the tracking paramaters
            : $"{feedId}|{title}"; //if doesnt have link, use the title

        var hash = MD5.HashData(Encoding.UTF8.GetBytes(key)); //we encode it first to be able to hash it (as create a fingerprint so same inpuut ---> same output)
        return new Guid(hash); //so we convert the 16bytes hash to the guid
    }

    // Host + path + meaningful query params only, all lowercase, no trailing slash - deliberately
    // ignores scheme (http vs https) and tracking params, since none of those change what article
    // this actually is.
    private static string NormalizeLink(string link)
    {
        var trimmed = link.Trim();

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return trimmed.ToLowerInvariant(); //not a well-formed absolute URL - best effort, just normalize case/whitespace
        }

        var keptParams = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !TrackingParamPrefixes.Any(prefix => p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => p, StringComparer.Ordinal); //sort so param order differences don't matter either

        var query = keptParams.Any() ? "?" + string.Join("&", keptParams) : "";
        var path = uri.AbsolutePath.TrimEnd('/');

        return $"{uri.Host}{path}{query}".ToLowerInvariant();
    }
}
//ArticleIdGenerator Take an article's URL, clean away differences that don't matter(tracking parameters, letter case, trailing slashes, etc.), then create a deterministic GUID so the exact same real-world article always gets the exact same ID every time your RSS reader downloads it.