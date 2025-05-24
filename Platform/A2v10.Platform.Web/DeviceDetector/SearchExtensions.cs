// Copyright © 2014-2025 Sarin Na Wangkanai, All Rights Reserved. Apache License, Version 2.0
// https://github.com/wangkanai/wangkanai

// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Platform.Web.DeviceDetector;

public interface IPrefixTrie
{
    bool ContainsWithAnyIn(ReadOnlySpan<char> source);
    bool StartsWithAnyIn(ReadOnlySpan<char> source);
    bool IsEnd { get; }
}

internal static class SearchExtensions
{
    public static bool SearchStartsWith(this string search, IPrefixTrie tree)
        => search.AsSpan().SearchStartsWith(tree);
    private static bool SearchStartsWith(this ReadOnlySpan<char> search, IPrefixTrie tree)
        => tree.StartsWithAnyIn(search);
    public static bool SearchContains(this string search, IPrefixTrie tree)
        => search.AsSpan().SearchContains(tree);

    private static bool SearchContains(this ReadOnlySpan<char> search, IPrefixTrie tree)
        => tree.ContainsWithAnyIn(search);

    public static KmpPrefixTrie BuildSearchTrie(this string[] keywords)
        => new(keywords);

    public static KmpPrefixTrie BuildSearchTrie(this IEnumerable<string> keywords)
        => new(keywords.Distinct().ToArray());
}
