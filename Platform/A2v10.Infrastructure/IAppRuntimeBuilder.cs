// Copyright © 2024-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;

namespace A2v10.Infrastructure;

public interface IAppRuntimeResult
{
	public IDataModel? DataModel { get; }
	public String? ActionResult { get; }
}
/* A door of a metadata folder, as an entry of "blobs": a field of the data names the model the data
 * come as (the Save checks <Model>.<Key>); a door that is no field - an attachment's upload - names none
 */
public record BlobDoor(String? Model, String? Accept, Int32? MaxSize /*KB*/);

// what the model.json the reader synthesizes for a folder without one says of it; Blobs: key -> door
public record EndpointDescriptor(Boolean RequireBase, IReadOnlyDictionary<String, BlobDoor> Blobs);

public interface IAppRuntimeBuilder
{
	Boolean IsAutoSupported { get; }
    Boolean IsMetaSupported { get; }
    // app.json 'useGrants' of a metadata application; false where there are no metadata
    Task<Boolean> UseGrantsAsync();

	String MetadataScripts(String minify);
	String MetadataStyles(String minify);

    Task<IAppRuntimeResult> RenderAsync(IPlatformUrl platformUrl, IModelView view, Boolean isReload);
	Task<ExpandoObject> SaveAsync(IPlatformUrl platformUrl, IModelView view, ExpandoObject data, ExpandoObject savePrms);
    Task<IInvokeResult> InvokeAsync(IPlatformUrl platformUrl, String command, IModelCommand cmd, ExpandoObject? prms);
    Task<IDataModel> ExecuteCommandAsync(IModelCommand command, ExpandoObject parameters);
	Task DbRemoveAsync(IPlatformUrl platformUrl, IModelView view, String? propName, ExpandoObject execPrms);
	Task<IDataModel> ExpandAsync(IPlatformUrl platformUrl, IModelView view, ExpandoObject execPrms);
    Task<ExpandoObject> LoadLazyAsync(IPlatformUrl platformUrl, IModelView view);
    Task<EndpointDescriptor> DescribeAsync(String path);
}