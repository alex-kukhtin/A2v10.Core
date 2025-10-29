
using System.Dynamic;
using A2v10.Infrastructure;

namespace MainApp.Catalog;

public struct CancelToken
{
    public Boolean Cancel { get; set; }
    public String Message { get; set; }
}

public class PlatformElement
{
    public Int64 Id { get; init; }

    #region EVENTS
    public Func<CancelToken, Task>? BeforeSave { get; init; }
    public Func<Task>? AfterSave { get; init; }
    #endregion
}

public class CatalogElem : PlatformElement
{
    public Boolean Void { get; set; }
    public Boolean IsSystem { get; set; }
    public String? Name { get; set; } = String.Empty;
    public String? Memo { get; set; } = String.Empty;
}


public partial class Agent : CatalogElem
{
    public static Agent FromExpando(ExpandoObject o)
    {
        return new Agent
        {
            Id = o.Get<Int64>(nameof(Id)),
            Code = o.Get<String>(nameof(Code)),
            Name = o.Get<String>(nameof(Name)),
            Memo = o.Get<String>(nameof(Memo)),
        };
    }

    public static Agent FromDynamic(dynamic o)
    {
        return new Agent
        {
            Id = o.Id,
            Code = o.Code,
            Name = o.Name,
            Memo = o.Memo,
        };
    }
}

public class ClrElementProvider
{

    public Dictionary<String, Func<ExpandoObject, PlatformElement>> _map = new()
    {
        { "catalog/agent", (o) => Agent.FromDynamic(o) }
    };

    public PlatformElement GetElement(String path, ExpandoObject o)
    {
        if (_map.TryGetValue(path.ToLowerInvariant(), out var func))
            return func(o);
        throw new NotSupportedException($"ElementProvider: Type for {path} is not found");
    }
}