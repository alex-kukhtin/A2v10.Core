﻿// Copyright © 2024-2025 Olekdsandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;

namespace A2v10.Infrastructure;

public class NullAppRuntimeBuilder : IAppRuntimeBuilder
{
	public bool IsAutoSupported => false;
    public Boolean IsMetaSupported => false;

    public String MetadataScripts(String _)
    {
        return String.Empty;
    }
    public String MetadataStyles(String _)
    {
        return String.Empty;
    }

    const String THROW_MESSAGE = "Install package A2v10.AppRuntimeBuilder or A2v10.Metadata.SqlServer";

    public Task<EndpointTableInfo> ModelInfoFromPathAsync(String path)
    {
        throw new NotImplementedException(THROW_MESSAGE);
    }

    public Task<IInvokeResult> InvokeAsync(IPlatformUrl platformUrl, String command, IModelCommand cmd, ExpandoObject? prms)
	{
        throw new NotImplementedException(THROW_MESSAGE);
    }

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

    public Task<ExpandoObject> LoadLazyAsync(IPlatformUrl platformUrl, IModelView view)
    {
        throw new NotImplementedException(THROW_MESSAGE);
    }
}
