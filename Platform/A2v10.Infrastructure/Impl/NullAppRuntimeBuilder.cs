// Copyright © 2024 Olekdsandr Kukhtin. All rights reserved.

using A2v10.Data.Interfaces;
using System;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public class NullAppRuntimeBuilder : IAppRuntimeBuilder
{
	public bool IsAutoSupported => false;

	const String THROW_MESSAGE = "Install package A2v10.AppRuntimeBuilder";
	public Task<IAppRuntimeResult> RenderAsync(IPlatformUrl platformUrl, IModelView view, bool isReload)
	{
		throw new NotImplementedException(THROW_MESSAGE);
	}

	public Task<ExpandoObject> SaveAsync(IPlatformUrl platformUrl, IModelView view, ExpandoObject data, ExpandoObject savePrms)
	{
		throw new NotImplementedException(THROW_MESSAGE);
	}

	public Task<IDataModel> ExecuteCommandAsync(IModelCommand command, ExpandoObject parameters)
	{
		throw new NotImplementedException(THROW_MESSAGE);
	}

	public Task DbRemoveAsync(IPlatformUrl platformUrl, IModelView view, String? propName, ExpandoObject execPrms)
	{
		throw new NotImplementedException(THROW_MESSAGE);
	}

	public Task<IDataModel> ExpandAsync(IPlatformUrl platformUrl, IModelView view, ExpandoObject execPrms)
	{
		throw new NotImplementedException(THROW_MESSAGE);
	}
}
