// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace A2v10.Metadata;

public static class JsonSettings
{
    public static JsonSerializerSettings IgnoreNull => new JsonSerializerSettings()
    {   
        NullValueHandling = NullValueHandling.Ignore
    };

    public static JsonSerializerSettings WithNull => new JsonSerializerSettings()
    {
        NullValueHandling = NullValueHandling.Include,
        Converters = [
            new StringEnumConverter()
        ]
    };
}
