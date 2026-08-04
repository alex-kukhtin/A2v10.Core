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

/* Fields, not expression-bodied properties - and that is the whole content of this file.
 *
 * Newtonsoft keeps its contract cache (the reflected shape of every type it has seen) in the
 * ContractResolver instance. A property written as '=> new()' hands out a fresh resolver on
 * every access, so every single Deserialize rebuilds the contracts for TableMetadata and
 * everything under it instead of reusing them. One instance, shared: the resolver is built for
 * that and its cache is thread-safe. Same as JsonHelpers.CamelCaseSerializerSettings, which is
 * already written this way.
 *
 * The converters here hold no state, so sharing them is safe too. Nothing may mutate a settings
 * object taken from here - it is now everyone's, not a copy.
 */
public static class JsonSettings
{
    public static readonly JsonSerializerSettings IgnoreNull = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        Converters = [
            new JsonEmptyStringEnumConverter(),
        ],
    };

    public static readonly JsonSerializerSettings CamelCaseSerializerSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        ContractResolver = new DefaultContractResolver()
        {
            NamingStrategy = new CamelCaseNamingStrategy()
        }
    };

    public static readonly JsonSerializerSettings CamelCaseSerializerSettingsFormat = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        Formatting = Formatting.Indented,

        ContractResolver = new DefaultContractResolver()
        {
            NamingStrategy = new CamelCaseNamingStrategy()
        }
    };

    public static readonly JsonSerializerSettings WithNull = new()
    {
        NullValueHandling = NullValueHandling.Include,
        Converters = [
            new JsonEmptyStringEnumConverter()
        ]
    };

    public static readonly JsonSerializerSettings Default = new()
    {
        Converters = [
            new JsonEmptyStringEnumConverter()
        ]
    };

    public static readonly JsonSerializerSettings DefaultExpando = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        Converters =
        [
            new IgnoreNullValueExpandoObjectConverter()
        ]
    };

}
