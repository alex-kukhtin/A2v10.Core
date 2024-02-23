// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public class NullEndpointHandler : IEndpointHandler
{
	public Task<String> RenderResultAsync(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject prms)
	{
		throw new NotImplementedException();
	}
}
