using A2v10.Infrastructure.ClrMetadata;
using System.Dynamic;


// GENERATED CODE - DO NOT MODIFY   


namespace MainApp;
public static class StartupClr
{
    public static void Register(AppMetadataClrOptions opts)
    {
        // TODO: catalog/agent!!!! Singular!!!!
        opts.AddElement("catalog/agents", (model, serviceProvider) => new MainApp.Catalog.Agent(model.Get<ExpandoObject>("Agent")));
    }
}

