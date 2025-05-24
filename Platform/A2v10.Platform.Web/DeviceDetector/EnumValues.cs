// Copyright © 2014-2025 Sarin Na Wangkanai, All Rights Reserved. Apache License, Version 2.0
// https://github.com/wangkanai/wangkanai

// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace A2v10.Platform.Web.DeviceDetector;

internal static class EnumValues<T> where T : Enum
{
    private static readonly T[] Values = (T[])Enum.GetValues(typeof(T));

    private static readonly ConcurrentDictionary<T, EnumValueName> MultiValueCache = new();
    private static readonly ConcurrentDictionary<T, IReadOnlySet<T>> MultiValueFlagsCache = new();

    private static readonly Dictionary<T, string> PlainNames = Values.ToDictionary(value => value, value => value.ToString());
    private static readonly Dictionary<T, IReadOnlySet<T>> EnumFlags = Values.ToDictionary(v => v, v => (IReadOnlySet<T>)Values.Where(item => v.HasFlag(item)).ToHashSet());

    public static string GetNameOriginal(T value) =>
        GetNameWithFlagsCached(value, true, ValueKind.Normal);
    public static bool TryGetSingleName(T value, [MaybeNullWhen(false)] out string result)
        => PlainNames.TryGetValue(value, out result);

    public static IReadOnlySet<T> GetFlags(T value)
        => EnumFlags.TryGetValue(value, out var flags)
               ? flags
               : MultiValueFlagsCache.GetOrAdd(value, v => Values.Where(item => v.HasFlag(item)).ToHashSet());

    private enum ValueKind
    {
        Normal = 0,
        Lower = 1,
        Upper = 2,
    }

    private static string GetNameWithFlags(T value)
        => value.GetFlags().Any()
               ? string.Join(',', value.GetFlags().Select(x => x.ToString()))
               : value.ToString();

    private static string GetNameWithFlagsCached(T value, bool returnAllFlags, ValueKind kind)
    {
        var names = MultiValueCache
            .GetOrAdd(value, v => new EnumValueName
            {
                Name = v.ToString(),
                NameWithFlags = GetNameWithFlags(v),
                NameLower = v.ToString().ToLowerInvariant(),
                NameLowerWithFlags = GetNameWithFlags(v).ToLowerInvariant(),
                NameUpper = v.ToString().ToUpperInvariant(),
                NameUpperWithFlags = GetNameWithFlags(v).ToUpperInvariant()
            });

        if (returnAllFlags)
            return kind switch
            {
                ValueKind.Normal => names.NameWithFlags,
                ValueKind.Lower => names.NameLowerWithFlags,
                ValueKind.Upper => names.NameUpperWithFlags,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };

        return kind switch
        {
            ValueKind.Normal => names.Name,
            ValueKind.Lower => names.NameLower,
            ValueKind.Upper => names.NameUpper,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }
    private readonly struct EnumValueName
    {
        public required string Name { get; init; }
        public required string NameWithFlags { get; init; }
        public required string NameLower { get; init; }
        public required string NameLowerWithFlags { get; init; }
        public required string NameUpper { get; init; }
        public required string NameUpperWithFlags { get; init; }
    }
}