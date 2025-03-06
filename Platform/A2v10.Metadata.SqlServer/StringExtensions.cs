﻿// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata.SqlServer;

internal static class StringExtensions
{
    public static String Singular(this String src)
    {
        if (src.EndsWith("ies"))
            return src[..^3] + "y";
        if (src.EndsWith("s"))
            return src[..^1];
        return src;  
    }
    public static String Plural(this String src)
    {
        if (src.EndsWith("y"))
            return src + "ies";
        return src + "s";
    }

    public static String SchemaToDirectory(this String schema)
    {
        return schema switch
        {
            "cat" => "catalog",
            "doc" => "document",
            _ => schema
        };
    }
}
