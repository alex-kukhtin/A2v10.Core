// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using A2v10.Services;

namespace A2v10.Metadata;


public class JsonEmptyStringEnumConverter : StringEnumConverter
{
    public override Object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (String.IsNullOrEmpty(reader.Value?.ToString()))
        {
            if (!objectType.IsEnum)
                throw new ArgumentException("Expected enum", nameof(objectType));
            return Enum.ToObject(objectType, 0);
        }
        return base.ReadJson(reader, objectType, existingValue, serializer);
    }
}

public static class JsonSettings
{
    public static JsonSerializerSettings IgnoreNull => new()
    {   
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        Converters = [
            new JsonEmptyStringEnumConverter(),
        ],
        ContractResolver = new FormMetadataContractResolver()
    };

    public static JsonSerializerSettings CamelCaseSerializerSettings => new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        ContractResolver = new DefaultContractResolver()
        {
            NamingStrategy = new CamelCaseNamingStrategy()
        }
    };

    public static JsonSerializerSettings CamelCaseSerializerSettingsFormat => new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        Formatting = Formatting.Indented,
        
        ContractResolver = new DefaultContractResolver()
        {
            NamingStrategy = new CamelCaseNamingStrategy()
        }
    };

    public static JsonSerializerSettings WithNull => new()
    {
        NullValueHandling = NullValueHandling.Include,
        Converters = [
            new JsonEmptyStringEnumConverter()
        ]
    };

    public static JsonSerializerSettings Default => new()
    {
        Converters = [
            new JsonEmptyStringEnumConverter()
        ]
    };

    public static JsonSerializerSettings DefaultExpando =>
        new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            Converters =
            [
                new IgnoreNullValueExpandoObjectConverter()
            ]
        };

}
