// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Reflection;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace A2v10.Metadata;


public class JsonEmptyStringEnumConverter : StringEnumConverter
{
    public override Object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (String.IsNullOrEmpty(reader.Value?.ToString()))
            return null;
        return base.ReadJson(reader, objectType, existingValue, serializer);
    }
}

public static class JsonSettings
{
    public static JsonSerializerSettings IgnoreNull => new JsonSerializerSettings()
    {   
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        Converters = [
            new JsonEmptyStringEnumConverter(),
        ],
        ContractResolver = new FormMetadataContractResolver()
    };

    public static JsonSerializerSettings WithNull => new JsonSerializerSettings()
    {
        NullValueHandling = NullValueHandling.Include,
        Converters = [
            new JsonEmptyStringEnumConverter()
        ]
    };

    public static JsonSerializerSettings Default => new JsonSerializerSettings()
    {
        Converters = [
            new JsonEmptyStringEnumConverter()
        ]
    };
}
