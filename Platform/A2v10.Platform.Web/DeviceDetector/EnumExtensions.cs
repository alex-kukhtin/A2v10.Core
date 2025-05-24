// Copyright © 2014-2025 Sarin Na Wangkanai, All Rights Reserved. Apache License, Version 2.0
// https://github.com/wangkanai/wangkanai

// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;

namespace A2v10.Platform.Web.DeviceDetector;

internal static class EnumExtensions
{
    public static bool ContainsLower<T>(this String value, T flags)
        where T : Enum
        => value.ContainSingle(flags) ||
           flags.GetFlags().Any(item => value.Contains(item.ToLowerString(), StringComparison.Ordinal));

    public static string ToLowerString<T>(this T value)
        where T : Enum
        => EnumValues<T>.GetNameOriginal(value).ToLowerInvariant();

    private static bool ContainSingle<T>(this string value, T flags)
        where T : Enum
        => EnumValues<T>.TryGetSingleName(flags, out var name) &&
           value.Contains(name, StringComparison.Ordinal);

    public static IReadOnlySet<T> GetFlags<T>(this T value)
        where T : Enum
        => EnumValues<T>.GetFlags(value);
}
