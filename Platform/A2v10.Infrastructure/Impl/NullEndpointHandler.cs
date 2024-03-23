// Copyright © 2023-2024 Oleksandr Kukhtin. All rights reserved.

using A2v10.Data.Interfaces;
using System;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public class NullEndpointHandler : IEndpointHandler
{
	public Task<IDataModel> ReloadAsync(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject prms)
	{
		throw new NotImplementedException();
	}

	public Task<String> RenderResultAsync(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject prms)
	{
		throw new NotImplementedException();
	}

	public Task<ExpandoObject> SaveAsync(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject data, ExpandoObject prms)
	{
		throw new NotImplementedException();
	}
}
