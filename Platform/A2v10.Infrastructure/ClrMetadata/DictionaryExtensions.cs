// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;

namespace A2v10.Infrastructure.ClrMetadata;

public static class DictionaryExtensions
{
    extension(IDictionary<String, Object?> source)
    {
        public T TryGetId<T>(String key)
        {
            if (source.TryGetValue(key, out var val) && val is T tVal)
                return tVal;
            return default!;
        }

        public String? TryGetString(String key)
        {
            if (source.TryGetValue(key, out var objVal))
                return objVal?.ToString();
            return null;
        }
    }

    extension(ExpandoObject source)
    {
        public T? Get<T>(String key)
        {
            var d = (IDictionary<String, Object?>)source;
            if (d.TryGetValue(key, out var val) && val is T tVal)
                return tVal;
            throw new InvalidOperationException($"Key '{key}' not found or invalid type");
        }
    }
}

