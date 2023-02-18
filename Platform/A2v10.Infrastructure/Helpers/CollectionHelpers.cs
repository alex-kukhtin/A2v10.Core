﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace A2v10.Infrastructure;
public static class CollectionHelpers
{
    public static IDictionary<TKey, TValue> Append<TKey, TValue>(this IDictionary<TKey, TValue> dst, IDictionary<TKey, TValue>? src, Boolean replaceExisiting = false)
    {
        if (src == null)
            return dst;
        foreach (var c in src)
        {
            if (!dst.ContainsKey(c.Key))
                dst.Add(c.Key, c.Value);
            if (replaceExisiting)
                dst[c.Key] = c.Value;
        }
        return dst;
    }

    public static String ToJsonObject(this IEnumerable<String> list)
    {
        return $"{{{String.Join(',', list)}}}";
    }
}

