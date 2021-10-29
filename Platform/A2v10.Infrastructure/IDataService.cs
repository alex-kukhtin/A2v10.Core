// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;

namespace A2v10.Infrastructure
{
	public interface IDataLoadResult
	{
		public IDataModel Model { get; }
		public IModelView View { get; }
	}

	public interface IBlobInfo
	{
		String Mime { get; }
		String Name { get; }
		Guid Token { get; }
		Byte[] Stream { get; }
		String BlobName { get; }
	}

	public interface IInvokeResult
	{
		Byte[] Body { get; }
		String ContentType { get; }
		String FileName { get; }
	}

	public interface ILayoutDescription
	{
		String ModelScripts { get; }
		String ModelStyles { get; }
	}

	public interface IDataService
	{
		Task<IDataLoadResult> LoadAsync(UrlKind kind, String baseUrl, Action<ExpandoObject> setParams);
		Task<IDataLoadResult> LoadAsync(String baseUrl, Action<ExpandoObject> setParams);
		Task<IBlobInfo> LoadBlobAsync(UrlKind kind, String baseUrl, Action<ExpandoObject> setParams, String suffix = null);

		Task<String> ReloadAsync(String baseUrl, Action<ExpandoObject> setParams);
		
		Task<String> LoadLazyAsync(String baseUrl, Object Id, String propertyName, Action<ExpandoObject> setParams);
		Task<String> LoadLazyAsync(ExpandoObject queryData, Action<ExpandoObject> setParams);

		Task<String> ExpandAsync(String baseUrl, Object id, Action<ExpandoObject> setParams);
		Task<String> ExpandAsync(ExpandoObject queryData, Action<ExpandoObject> setParams);

		Task<String> SaveAsync(String baseUrl, ExpandoObject data, Action<ExpandoObject> setParams);
		Task DbRemoveAsync(String baseUrl, Object Id, String propertyName, Action<ExpandoObject> setParams);

		Task<IInvokeResult> InvokeAsync(String baseUrl, String command, ExpandoObject data, Action<ExpandoObject> setParams);
		Byte[] Html2Excel(String html);

		Task<ILayoutDescription> GetLayoutDescriptionAsync(String baseUrl);
	}
}
