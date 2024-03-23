// Copyright © 2023-2024 Oleksandr Kukhtin. All rights reserved.

using A2v10.Data.Interfaces;
using System;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public interface IEndpointHandler
{
    Task<String> RenderResultAsync(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject prms);
	Task<IDataModel> ReloadAsync(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject prms);
	Task<ExpandoObject> SaveAsync(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject data, ExpandoObject prms);
}

