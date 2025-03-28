// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

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
public record EndpointTableInfo(String? DataSoruce, String Schema, String Table);

public interface IAppRuntimeBuilder
{
	Boolean IsAutoSupported { get; }
    Boolean IsMetaSupported { get; }

	String MetadataScripts(String minify);
	String MetadataStyles(String minify);

	Task<EndpointTableInfo> ModelInfoFromPathAsync(String path);
    Task<IAppRuntimeResult> RenderAsync(IPlatformUrl platformUrl, IModelView view, Boolean isReload);
	Task<ExpandoObject> SaveAsync(IPlatformUrl platformUrl, IModelView view, ExpandoObject data, ExpandoObject savePrms);
    Task<IInvokeResult> InvokeAsync(IPlatformUrl platformUrl, String command, IModelCommand cmd, ExpandoObject? prms);
    Task<IDataModel> ExecuteCommandAsync(IModelCommand command, ExpandoObject parameters);
	Task DbRemoveAsync(IPlatformUrl platformUrl, IModelView view, String? propName, ExpandoObject execPrms);
	Task<IDataModel> ExpandAsync(IPlatformUrl platformUrl, IModelView view, ExpandoObject execPrms);
}