// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.IO;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;

namespace A2v10.Infrastructure;

public interface IDataLoadResult
{
	public IDataModel? Model { get; }
	public IModelView? View { get; }
	public String? ActionResult { get; }
}

public interface IBlobInfo
{
	String? Mime { get; }
	String? Name { get; }
	Guid Token { get; }
	Byte[]? Stream { get; }
	String? BlobName { get; }
	Boolean CheckToken { get; }
}

public interface IBlobUpdateInfo
{
    public Int32? TenantId { get; set; }
    public Int64? CompanyId { get; set; }
    public Int64 UserId { get; set; }
    public String? Mime { get; set; }
    public String? Name { get; set; }
    public Stream? Stream { get; set; }
    public String? BlobName { get; set; }
    public Object? Id { get; set; }
}

public interface IBlobUpdateOutput
{
    public Object Id { get; set; }
    public Guid? Token { get; set; }

}

public interface ISignalResult
{
	Int64 UserId { get; }
	String Message { get; }
	ExpandoObject? Data { get; }
}

public interface IInvokeResult
{
	Byte[] Body { get; }
	String ContentType { get; }
	String? FileName { get; }
	ISignalResult? Signal { get; }
}

public interface ILayoutDescription
{
	String? ModelScripts { get; }
	String? ModelStyles { get; }
}

public interface ISaveResult
{
	String Data { get; }
	ISignalResult? SignalResult { get; }

}

public interface IDataService
{
	Task<IDataLoadResult> LoadAsync(UrlKind kind, String baseUrl, Action<ExpandoObject> setParams, Boolean isReload = false);
	Task<IDataLoadResult> LoadAsync(String baseUrl, Action<ExpandoObject> setParams, Boolean isReload = false);
    Task<IInvokeResult> ExportAsync(String baseUrl, Action<ExpandoObject> setParams);
    Task<IBlobInfo?> LoadBlobAsync(UrlKind kind, String baseUrl, Action<ExpandoObject> setParams, String? suffix = null);
	Task<ExpandoObject> SaveBlobAsync(String baseUrl, Action<IBlobUpdateInfo> setBlob, Action<ExpandoObject>? setParams, UrlKind urlKind = UrlKind.File);
	Task<ExpandoObject> DeleteBlobAsync(String baseUrl, Action<ExpandoObject>? setParams);
    Task<String> ReloadAsync(String baseUrl, Action<ExpandoObject> setParams);	
	Task<String> LoadLazyAsync(String baseUrl, Object Id, String propertyName, Action<ExpandoObject> setParams);
	Task<String> LoadLazyAsync(ExpandoObject queryData, Action<ExpandoObject> setParams);

	Task<String> ExpandAsync(String baseUrl, Object id, Action<ExpandoObject> setParams);
	Task<String> ExpandAsync(ExpandoObject queryData, Action<ExpandoObject> setParams);

	Task<ISaveResult> SaveAsync(String baseUrl, ExpandoObject data, Action<ExpandoObject> setParams);
	Task DbRemoveAsync(String baseUrl, Object Id, String? propertyName, Action<ExpandoObject> setParams);

	Task<IInvokeResult> InvokeAsync(String baseUrl, String command, ExpandoObject? data, Action<ExpandoObject> setParams);
	Byte[] Html2Excel(String html);

	Task<ILayoutDescription?> GetLayoutDescriptionAsync(String? baseUrl);
}
