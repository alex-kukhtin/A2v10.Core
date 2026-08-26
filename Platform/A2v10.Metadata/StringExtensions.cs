// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

internal static class StringExtensions
{
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
