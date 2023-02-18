﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace A2v10.Infrastructure
{
    public static class PathHelpers
    {
        public static String AddExtension(this String This, String extension)
        {
            if (String.IsNullOrEmpty(This))
                return This;
            String ext = "." + extension;
            if (This.EndsWith(ext))
                return This;
            return This + ext;
        }

        public static String RemoveHeadSlash(this String This)
        {
            if (String.IsNullOrEmpty(This))
                return This;
            if (This.StartsWith("/"))
                return This[1..];
            return This;
        }

        public static String NormalizeSlash(this String This)
        {
            if (String.IsNullOrEmpty(This))
                return This;
            return This.Replace('\\', '/');
        }

        public static String RemoveEOL(this String This)
        {
            if (String.IsNullOrEmpty(This))
                return This;
            return This.Replace("\n", "").Replace("\r", "");
        }

        public static String CombineRelative(String path1, String path2)
        {
            var dirSep = new String(Path.DirectorySeparatorChar, 1);
            var combined = Path.GetFullPath(Path.Combine(dirSep, path1, path2));
            var root = Path.GetFullPath(dirSep);
            return combined[root.Length..];
        }
    }
}
