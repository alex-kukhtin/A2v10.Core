// Copyright © 2024 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;

namespace A2v10.Infrastructure;

public interface IAppRuntimeResult
{
	public IDataModel? DataModel { get; }
	public String? ActionResult { get; }
}
public interface IAppRuntimeBuilder
{
	Task<IAppRuntimeResult> RenderAsync(IPlatformUrl platformUrl, IModelView view, Boolean isReload);
	Task<ExpandoObject> SaveAsync(IPlatformUrl platformUrl, IModelView view, ExpandoObject data, ExpandoObject savePrms);
}
