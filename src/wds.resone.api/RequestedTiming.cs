using System.Text.RegularExpressions;

namespace Wds.Resone.Api;

/// <summary>Project timing wins; preserve note tokens and rhythmic durations.</summary>
public static class RequestedTiming
{
    public static string Apply(string notation, int tempo, string meter)
    {
        // Numeric tempo is accepted only as the first header token. Do not
        // reinterpret numeric pitch mistakes elsewhere as tempo directives.
        notation = Regex.Replace(notation, @"\A\s*[0-9]+(?:\.[0-9]+)?(?=\s|[|]|$)", "");
        // Standalone directives only: never touch chord contents, gate values,
        // onset fractions such as @+1/8q, or phrase/section markers.
        notation = Regex.Replace(notation,
            @"(?<![^\s|])(?:tempo=[^\s|]+|[0-9]+/[0-9]+)(?=[\s|]|$)", "");
        return $"tempo={tempo} {meter}\n" + notation.Trim();
    }
}
