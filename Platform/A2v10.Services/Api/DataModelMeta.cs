// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Data.Interfaces;

namespace A2v10.Services.Api;

public static class DataModelMeta
{
    public static ExpandoObject BuildDataModelMeta(this IDataModel? model)
    {
        if (model == null)
            return [];

        ExpandoObject buildProps(IDataMetadata metadata)
        {
            var props = new ExpandoObject();
            foreach (var p in metadata.Fields)
            {
                props.TryAdd(p.Key, new ExpandoObject() {
                    { "type", p.Value.TypeScriptName },
                    { "len", p.Value.Length == 0 ? null : p.Value.Length }
                });
            }
            return props;
        }

        ExpandoObject buildTypes()
        {
            var types = new ExpandoObject();
            foreach (var t in model.Metadata)
            {
                types.Add(t.Key, new ExpandoObject()
                {
                    {"props", buildProps(t.Value)  },
                    {"id", t.Value.Id },
                    {"name", t.Value.Name },
                });
            }
            return types;
        }

        // The filter dictionary is form, not data: project it from the $ModelInfo echo
        // (keyed by root name; a reference filter is echoed as {Id, Name}, a scalar as a value).
        ExpandoObject? buildFilters()
        {
            if (model.Root is not IDictionary<String, Object?> root
                || !root.TryGetValue("$ModelInfo", out var mi) || mi is not ExpandoObject modelInfo)
                return null;
            var filters = new ExpandoObject();
            foreach (var (rootKey, entry) in (IDictionary<String, Object?>)modelInfo)
            {
                if (entry is not IDictionary<String, Object?> entryDict
                    || !entryDict.TryGetValue("Filter", out var f) || f is not ExpandoObject filter)
                    continue;
                var props = new ExpandoObject();
                foreach (var (key, value) in (IDictionary<String, Object?>)filter)
                    props.TryAdd(key, new ExpandoObject() {
                        // Period* is the platform's naming convention for range filters,
                        // so the {From, To} shape is not mistaken for a reference.
                        { "type", key.StartsWith("Period", StringComparison.Ordinal) ? "period"
                            : value is ExpandoObject ? "reference" : "value" }
                    });
                filters.TryAdd(rootKey, props);
            }
            return ((IDictionary<String, Object?>)filters).Count > 0 ? filters : null;
        }

        var result = new ExpandoObject()
        {
            {"types", buildTypes() }
        };
        var filters = buildFilters();
        if (filters != null)
            result.Add("filters", filters);
        return result;
    }
}
