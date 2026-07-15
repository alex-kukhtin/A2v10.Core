using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        return new ExpandoObject()
        {
            {"types", buildTypes() }
        };
    }
}
