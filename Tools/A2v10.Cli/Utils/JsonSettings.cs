// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using A2v10.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace A2v10.Cli;

public class IgnoreNullValueExpandoObjectConverter : ExpandoObjectConverter
{
    public override bool CanWrite => true;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        static Boolean IsValueNotEmpty(Object val)
        {
            if (val == null)
                return false;

            // skip empty lists
            if (val is List<ExpandoObject> iList && iList.Count == 0)
                return false;

            return val switch
            {
                String strVal => !String.IsNullOrEmpty(strVal),
                Boolean boolVal => boolVal,
                Double doubleVal => doubleVal != 0,
                Int32 intVal => intVal != 0,
                _ => true,
            };
        }

        if (value is IDictionary<String, Object> expando)
        {
            var dictionary = expando
                .Where(p => IsValueNotEmpty(p.Value))
                .ToDictionary(p => p.Key.ToCamelCase(), p => p.Value);
            serializer.Serialize(writer, dictionary);
        }
        else
            base.WriteJson(writer, value, serializer);
    }
}
internal static class JsonSettings
{
    public static JsonSerializerSettings CamelCaseSerializerSettingsFormat => new()
    {
        NullValueHandling = NullValueHandling.Include,
        DefaultValueHandling = DefaultValueHandling.Include,
        Formatting = Formatting.Indented,

        ContractResolver = new DefaultContractResolver()
        {
            NamingStrategy = new CamelCaseNamingStrategy()
        },
        Converters = [
            //new IgnoreNullValueExpandoObjectConverter()
        ]
    };
}
