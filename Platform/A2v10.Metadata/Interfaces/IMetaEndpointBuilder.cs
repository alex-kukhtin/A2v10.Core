// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal interface IMetaEndpointBuilder
{
    Task<IAppRuntimeResult> RenderAsync(IPlatformUrl platformUrl, IModelView view, bool isReload);
}
