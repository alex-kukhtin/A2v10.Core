using System.Dynamic;
using A2v10.Services.Api;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace A2v10.ApiHost;

internal sealed class FilterBagBinder : IModelBinder
{
    private static readonly HashSet<String> Control =
        typeof(IndexQuery).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var bag = new ExpandoObject();
        var dict = (IDictionary<String, Object?>)bag;
        foreach (var (key, value) in bindingContext.HttpContext.Request.Query)
        {
            if (Control.Contains(key))
                continue;
            // Raw strings — the binder does not coerce; the read interprets the value.
            dict[key] = value.Count > 1 ? value.ToArray() : value.ToString();
        }
        bindingContext.Result = ModelBindingResult.Success(bag);
        return Task.CompletedTask;
    }
}
