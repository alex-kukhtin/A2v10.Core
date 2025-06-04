﻿// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

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
        if (src.EndsWith("y"))
            return src + "ies";
        else if (src.EndsWith("s"))
            return src + "es";
        return src + "s";
    }
}
