
using A2v10.Infrastructure.ClrMetadata;

using System.Dynamic;

namespace MainApp.Catalog;

// GENERATED CODE - DO NOT MODIFY   
public partial class Agent : CatalogBase<Int64>
{
    public Agent() : base() { }

    public String? Code { get; set; }

    public Agent(ExpandoObject? src) : base(src)
    {
        if (src != null)
        {
            var d = (IDictionary<String, Object?>)src;
            Code = d.TryGetString(nameof(Code));
        }
        Init();
    }

    public override void ToExpando()
    {
        base.ToExpando();
        if (_source == null)
            return;
        var d = (IDictionary<String, Object?>) _source;
        d[nameof(Code)] = Code;
    }
}
