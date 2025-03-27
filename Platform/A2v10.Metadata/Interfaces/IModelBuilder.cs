// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal interface IModelBuilder
{
    Task BuildAsync(IPlatformUrl platformUrl, IModelBase modelBase);
    Task BuildAsync(IPlatformUrl platformUrl, TableMetadata table, String? dataSource);
    Task<IDataModel> LoadModelAsync();
    Task<ExpandoObject> SaveModelAsync(ExpandoObject data, ExpandoObject savePrms);
    Task<String> RenderPageAsync(IModelView modelView, IDataModel dataModel);
    Task<String> CreateTemplateAsync();
    Form CreateDefaultForm();
}
