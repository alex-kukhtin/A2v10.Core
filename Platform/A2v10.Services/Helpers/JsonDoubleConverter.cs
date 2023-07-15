// Copyright © 2020-2023 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace A2v10.Services;

public class JsonDoubleConverter : JsonConverter<Double>
{
	public override void WriteJson(JsonWriter writer, Double value, JsonSerializer serializer)
	{
		if (Double.IsNaN(value))
			writer.WriteValue("NaN");
		else if (Double.IsInfinity(value))
			writer.WriteValue("Infinity");
		else if (Math.Truncate(value) == value)
			writer.WriteValue(Convert.ToInt64(value));
		else
			writer.WriteValue(value);
	}

	public override Double ReadJson(JsonReader reader, Type objectType, Double existingValue, Boolean hasExistingValue, JsonSerializer serializer)
	{
		return serializer.Deserialize<Double>(reader);
	}
}

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
                .ToDictionary(p => p.Key, p => p.Value);
            serializer.Serialize(writer, dictionary);
        }
        else
            base.WriteJson(writer, value, serializer);
    }
}