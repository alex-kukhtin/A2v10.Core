// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

public class GenerateHandler(IServiceProvider _serviceProvider) : IClrInvokeTarget
{
    private readonly DatabaseMetadataProvider _metadataProvider = _serviceProvider.GetRequiredService<DatabaseMetadataProvider>();
    private readonly IModelBuilderFactory _modelBuilderFactory = _serviceProvider.GetRequiredService<IModelBuilderFactory>();
    public async Task<Object> InvokeAsync(ExpandoObject args)
    {
        var userIdObj = args.Get<Object>("UserId");
        var schema = args.Get<String>("Schema")
            ?? throw new InvalidOperationException("Argument 'Schema' not found");
        var name = args.Get<String>("Name")
            ?? throw new InvalidOperationException("Argument 'Name' not found");

        var table = await _metadataProvider.GetSchemaAsync(null, schema, name);

        // TODO: Create EndpointGenerator
        var builder = await _modelBuilderFactory.BuildAsync(table.PlatformUrl("index"), table, null);
        var formIndex = await builder.GetFormAsync();

        builder = await _modelBuilderFactory.BuildAsync(table.PlatformUrl("edit"), table, null);
        var formEdit = await builder.GetFormAsync();

        var indexXml = XmlTextBuilder.Build(formIndex);

        var editXml = XmlTextBuilder.Build(formEdit);

        throw new InvalidOperationException($"GENERATE FOR {table.EndpointPath()}");
    }
}
