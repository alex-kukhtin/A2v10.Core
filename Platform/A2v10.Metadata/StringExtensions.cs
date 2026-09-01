// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal static class StringExtensions
{
    /* The missing half of the pair StringHelpers.ToKebabCase is one of: an address segment is
     * written in kebab-case, an identifier is not, so '/enum/vat-rates' names 'VatRates'.
     * ToPascalCase alone leaves the dash where it is, and a dash survives in a quoted table name
     * only to fail in the one place a name is emitted bare - a constraint.
     *
     * Only names the PLATFORM derives go through here. A declared table name is written by the
     * author as an identifier and is never converted.
     */
    public static String KebabToPascal(this String src) =>
        String.Concat(src.Split('-').Select(p => p.ToPascalCase()));

    public static String Singular(this String src)
    {
        if (src.EndsWith("ies"))
            return src[..^3] + "y";
        if (src.EndsWith("ses"))
            return src[..^2]; // remove 'es'
        if (src.EndsWith("s"))
            return src[..^1];
        return src;  
    }
    public static String Plural(this String src)
    {
        if (String.IsNullOrEmpty(src))
            return src;
        if (src.Length > 1 && src[^1] == 'y' && !"aeiouAEIOU".Contains(src[^2]))
            return src[..^1] + "ies";
        if (src[^1] is 's' or 'x' or 'z' or 'S' or 'X' or 'Z'
            || src.EndsWith("ch", StringComparison.OrdinalIgnoreCase)
            || src.EndsWith("sh", StringComparison.OrdinalIgnoreCase))
            return src + "es";
        return src + "s";
    }
}
