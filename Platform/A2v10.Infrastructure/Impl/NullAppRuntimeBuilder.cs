// Copyright © 2024 Olekdsandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public class NullAppRuntimeBuilder : IAppRuntimeBuilder
{

	public Task<IAppRuntimeResult> RenderAsync(IPlatformUrl platformUrl, IModelView view, bool isReload)
	{
		throw new NotImplementedException("Install package A2v10.AppRuntimeBuilder");
	}

	public Task<ExpandoObject> SaveAsync(IPlatformUrl platformUrl, IModelView view, ExpandoObject data, ExpandoObject savePrms)
	{
		throw new NotImplementedException("Install package A2v10.AppRuntimeBuilder");
	}
}
