

using System.Dynamic;

using A2v10.App.Infrastructure;

namespace MainApp.Catalog;

// GENERATED CODE - DO NOT MODIFY   
public partial class Agent : CatalogBase<Int64>
{
    public Agent(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        Init();
    }   

    public String? Code { get; set; }

    public Agent(IServiceProvider serviceProvider, ExpandoObject? src) : base(serviceProvider, src)
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
