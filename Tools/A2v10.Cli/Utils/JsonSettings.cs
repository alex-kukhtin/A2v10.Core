// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace A2v10.Cli;

internal static class JsonSettings
{
    public static JsonSerializerSettings CamelCaseSerializerSettingsFormat => new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Include,
        Formatting = Formatting.Indented,

        ContractResolver = new DefaultContractResolver()
        {
            NamingStrategy = new CamelCaseNamingStrategy()
        }
    };
}
