// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;

namespace A2v10.Infrastructure;

public class NullEndpointHandler : IEndpointHandler
{
	public string RenderResult(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject prms)
	{
		throw new NotImplementedException(modelView.EndpointHandler);
	}
}
