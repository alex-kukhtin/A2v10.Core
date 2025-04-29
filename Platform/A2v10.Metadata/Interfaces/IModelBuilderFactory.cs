// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal interface IModelBuilderFactory
{
    Task<IModelBuilder> BuildAsync(IPlatformUrl platformUrl, IModelBase modelBase);
    Task<IModelBuilder> BuildAsync(IPlatformUrl platformUrl, TableMetadata table, String? dataSource);
}
