// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal interface IModelBuilder
{
    Task<IDataModel> LoadModelAsync();
    Task<ExpandoObject> SaveModelAsync(ExpandoObject data, ExpandoObject savePrms);
    Task<String> RenderPageAsync(IModelView modelView, IDataModel dataModel);
    Task<IInvokeResult> InvokeAsync(IModelCommand cmd, String command, ExpandoObject? prms);
    Task<String> CreateTemplateAsync();
    Form CreateDefaultForm();
    String? MetadataEndpointBuilder { get; }

    TableMetadata Table { get; }
    TableMetadata? BaseTable { get; }
    AppMetadata AppMeta { get; }
    Task<FormMetadata> GetFormAsync();
}
