
using A2v10.App.Infrastructure;
using System.Dynamic;


// GENERATED CODE - DO NOT MODIFY   


namespace MainApp;
public static class StartupClr
{
    private static readonly Dictionary<String, Func<ExpandoObject, IServiceProvider, IClrElement>> _elemMap = new()
    {
        // ??? TODO: catalog/agent!!!! Singular in name !!!!
        ["catalog/agents"] = (model, serviceProvider) => new MainApp.Catalog.Agent(serviceProvider, model.Get<ExpandoObject>("Agent"))
    };
    public static void Register(AppMetadataClrOptions opts)
    {
        opts.AddRange(_elemMap);
    }
}

