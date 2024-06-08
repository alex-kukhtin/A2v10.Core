// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;

using A2v10.Services.Interop;
using System.Globalization;

namespace A2v10.Services;

public partial class DataService
{
	public async Task<IBlobInfo?> LoadBlobAsync(UrlKind kind, String baseUrl, Action<ExpandoObject> setParams, String? suffix = null)
	{
		var platformUrl = CreatePlatformUrl(kind, baseUrl);
		IModelBlob blob = await _modelReader.GetBlobAsync(platformUrl, suffix)
			?? throw new DataServiceException($"Blob is null");

		var prms = new ExpandoObject();
		prms.Set("Id", blob.Id);
		if (!String.IsNullOrEmpty(blob.Key))
			prms.Set("Key", blob.Key);
		setParams?.Invoke(prms);

		var blobInfo = blob.Type switch
		{
			ModelBlobType.sql => await LoadBlobSql(blob, prms),
			ModelBlobType.json => await LoadBlobJson(blob, prms),
			ModelBlobType.clr => await LoadBlobClr(blob, prms),
			ModelBlobType.blobStorage => await LoadBlobStorage(blob, prms),
			ModelBlobType.excel => await LoadBlobExcel(blob, prms),
			_ => throw new NotImplementedException(blob.Type.ToString()),
		};
		return blobInfo;
	}

	private Task<BlobInfo?> LoadBlobSql(IModelBlob blob, ExpandoObject prms)
	{
		var loadProc = blob.LoadBlobProcedure();
		if (String.IsNullOrEmpty(loadProc))
			throw new DataServiceException($"LoadBlobProcedure is null");
		return _dbContext.LoadAsync<BlobInfo>(blob?.DataSource, loadProc, prms);
	}

	private async Task<BlobInfo?> LoadBlobExcel(IModelBlob blob, ExpandoObject prms)
	{
		var loadProc = blob.LoadProcedure();
		if (String.IsNullOrEmpty(loadProc))
			throw new DataServiceException($"LoadProcedure is null");
		var dm = await _dbContext.LoadModelAsync(blob.DataSource, loadProc, prms)
			?? throw new InvalidOperationException("LoadBlobExcel. DataModel is null");
		var bytes = ExSheet.CreateFromDataModel(dm);

		var fn = blob.OutputFileName ?? "ffile";
		if (!fn.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
			fn += ".xlsx";
		return new BlobInfo()
		{
			SkipToken = true,
			Mime = MimeTypes.Application.Xlsx,
			Stream = bytes,
			Name = fn
		};
	}

	private async Task<BlobInfo?> LoadBlobStorage(IModelBlob blob, ExpandoObject prms)
	{
		var blobInfo = await LoadBlobSql(blob, prms);
		var blobName = blobInfo?.BlobName
			?? throw new InvalidOperationException("BlobName is null");
		if (blobInfo.Stream != null)
			return blobInfo;

		var blobEngine = _serviceProvider.GetRequiredService<IBlobStorageProvider>();
		var storage = blobEngine.FindBlobStorage(blob.BlobStorage ??
			throw new InvalidOperationException("BlobStorage is null"));

		var bytes = await storage.LoadAsync(blob.BlobSource, blob.Container, blobName);

		blobInfo.Stream = bytes.ToArray();
		return blobInfo;
	}

	private Object CreateClrObject(String? strClrType)
	{
		if (String.IsNullOrEmpty(strClrType))
			throw new DataServiceException($"ClrType is null");
		var (assembly, clrType) = ClrHelpers.ParseClrType(strClrType);
		var ass = Assembly.Load(assembly);
		var tp = ass.GetType(clrType)
			?? throw new InvalidOperationException($"Type {strClrType} not found");
		var ctor = tp.GetConstructor([typeof(IServiceProvider)])
			?? throw new InvalidOperationException($"ctor(IServiceProvider) not found in {strClrType}");
		var elem = ctor.Invoke([_serviceProvider])
			?? throw new InvalidOperationException($"Unable to create element of {strClrType}");
		return elem;
	}

	private async Task<BlobInfo?> LoadBlobClr(IModelBlob modelBlob, ExpandoObject prms)
	{
		var elem = CreateClrObject(modelBlob.ClrType);
		if (elem is not IClrInvokeBlob invokeBlob)
			throw new InvalidOperationException($"The type '{modelBlob.ClrType}' must implement the interface IClrInvokeBlob");
		var result = await invokeBlob.InvokeAsync(prms);
		return new BlobInfo()
		{
			Name = result.Name,
			Mime = result.Mime,
			Stream = result.Stream,
			BlobName = result.BlobName,
			SkipToken = true
		};
	}

	private async Task<BlobInfo?> LoadBlobJson(IModelBlob modelBlob, ExpandoObject prms)
	{
		var loadProc = modelBlob.LoadProcedure();
		if (String.IsNullOrEmpty(loadProc))
			throw new DataServiceException($"LoadProcedure is null");
		var dm = await _dbContext.LoadModelAsync(modelBlob.DataSource, loadProc, prms);
		var settings = JsonHelpers.IndentedSerializerSettings;
		var json = JsonConvert.SerializeObject(dm.Root, settings);
		Byte[]? stream;
		String mime = MimeTypes.Application.Json;
		if (modelBlob.Zip)
		{
			mime = MimeTypes.Application.Zip;
			stream = ZipUtils.CompressText(json);
		}
		else
		{
			stream = Encoding.UTF8.GetBytes(json);
		}
		return new BlobInfo()
		{
			SkipToken = true,
			Mime = mime,
			Stream = stream,
			Name = modelBlob.OutputFileName
		};
	}

	public async Task<ExpandoObject> SaveBlobAsync(String baseUrl, Action<IBlobUpdateInfo> setBlob, Action<ExpandoObject>? setParams, UrlKind urlKind = UrlKind.File)
	{
		var platformUrl = CreatePlatformUrl(urlKind, baseUrl);
		var blobModel = await _modelReader.GetBlobAsync(platformUrl)
			?? throw new DataServiceException($"Blob is null");
		return blobModel.Type switch
		{
			ModelBlobType.parse => await ParseFile(blobModel, setBlob, setParams),
			ModelBlobType.sql => await SaveBlobSql(blobModel, setBlob, setParams),
			ModelBlobType.clr => await SaveBlobClr(blobModel, setBlob, setParams),
			ModelBlobType.blobStorage => await SaveBlobStorage(blobModel, setBlob, setParams),
			_ =>
				throw new NotImplementedException(blobModel.Type.ToString())
		};
	}

	private async Task<ExpandoObject> SaveBlobClr(IModelBlob blobModel, Action<IBlobUpdateInfo> setBlob, Action<ExpandoObject>? setParams)
	{
		BlobUpdateInfo blobInfo = new()
		{
			Id = blobModel.Id,
		};
		setBlob(blobInfo);
		var args = new ExpandoObject();
		setParams?.Invoke(args);
		args.Add("Blob", blobInfo);

		var obj = CreateClrObject(blobModel.ClrType);
		if (obj is not IClrInvokeTarget clrInvokeTarget)
			throw new InvalidOperationException($"The type '{blobModel.ClrType}' must implement the interface clrInvokeTarget");

		var result = await clrInvokeTarget.InvokeAsync(args);

		if (result is InvokeBlobResult invokeBlobResult)
		{
			if (invokeBlobResult.Stream == null)
				throw new InvalidOperationException("InvokeBlobResult.Stream is null");
			blobInfo.Stream = new MemoryStream(invokeBlobResult.Stream);
			return await SaveBlobInt(blobInfo, blobModel, setParams);
		}
		else if (result is String strResult)
		{
			BlobUpdateString blobStringInfo = new()
			{
				TenantId = blobInfo.TenantId,
				UserId = blobInfo.UserId,
				Id = blobInfo.Id,
				Key = blobInfo.Key,
				Name = blobInfo.Name,
				Mime = blobInfo.Mime,
				Stream = strResult
			};
			return await SaveBlobString(blobStringInfo, blobModel, setParams);
		}
		if (result is not ExpandoObject eoResult)
			throw new InvalidOperationException($"{blobModel.ClrType}. The return type of the must be an ExpandoObject");
		return eoResult;
	}

	private Task<ExpandoObject> SaveBlobSql(IModelBlob blobModel, Action<IBlobUpdateInfo> setBlob, Action<ExpandoObject>? setParams)
	{
		BlobUpdateInfo blobInfo = new()
		{
			Id = blobModel.Id
		};
		setBlob(blobInfo);
		return SaveBlobInt(blobInfo, blobModel, setParams);
	}

	private async Task<ExpandoObject> SaveBlobString(BlobUpdateString blobInfo, IModelBlob blobModel, Action<ExpandoObject>? setParams)
	{
		ExpandoObject savePrms = [];
		setParams?.Invoke(savePrms);

		blobInfo.UserId = savePrms.Get<Int64>("UserId"); // required
		blobInfo.TenantId = savePrms.Get<Int32?>("TenantId"); // optional
		var key = savePrms.Get<String>("Key");
		if (!String.IsNullOrEmpty(key))
			blobInfo.Key = key; // optional

		var saveProc = (blobModel?.UpdateBlobProcedure())
			?? throw new DataServiceException($"UpdateBlobProcedure is null");

		var output = await _dbContext.ExecuteAndLoadAsync<BlobUpdateString, BlobUpdateOutput>(blobModel.DataSource, saveProc, blobInfo)
			?? throw new InvalidOperationException("Update result is null");

		var result = new ExpandoObject()
		{
			{ "Id",    output.Id },
			{ "Name",  output.Name ?? blobInfo.Name ?? String.Empty },
			{ "Mime",  blobInfo.Mime ?? String.Empty },
		};
		if (output.Token != null)
			result.Add("Token", output.Token);
		return result;
	}

	private async Task<ExpandoObject> SaveBlobInt(BlobUpdateInfo blobInfo, IModelBlob blobModel, Action<ExpandoObject>? setParams)
	{
		ExpandoObject savePrms = [];
		setParams?.Invoke(savePrms);

		blobInfo.UserId = savePrms.Get<Int64>("UserId"); // required
		blobInfo.TenantId = savePrms.Get<Int32?>("TenantId"); // optional
		var key = savePrms.Get<String>("Key");
		if (!String.IsNullOrEmpty(key))
			blobInfo.Key = key; // optional

		var saveProc = (blobModel?.UpdateBlobProcedure())
			?? throw new DataServiceException($"UpdateBlobProcedure is null");

		var output = await _dbContext.ExecuteAndLoadAsync<BlobUpdateInfo, BlobUpdateOutput>(blobModel.DataSource, saveProc, blobInfo)
			?? throw new InvalidOperationException("Update result is null");

		var result = new ExpandoObject()
		{
			{ "Id",    output.Id },
			{ "Name",  blobInfo.Name ?? String.Empty },
			{ "Mime",  blobInfo.Mime ?? String.Empty },
		};
		if (output.Token != null)
			result.Add("Token", output.Token);
		return result;
	}
	private async Task<ExpandoObject> SaveBlobStorage(IModelBlob blobModel, Action<IBlobUpdateInfo> setBlob, Action<ExpandoObject>? setParams)
	{
		BlobUpdateInfo blobInfo = new()
		{
			Id = blobModel.Id
		};
		setBlob(blobInfo);
		if (blobInfo.Stream == null)
			throw new InvalidOperationException("Stream is null");
		var tenant = String.Empty;
		if (blobInfo.TenantId != null)
			tenant = $"{blobInfo.TenantId}/";
		var folder = DateTime.UtcNow.ToString("yyyyMMdd");

		blobInfo.BlobName = $"{tenant}{folder}/{Guid.NewGuid()}_{blobInfo.Name}";

		var blobEngine = _serviceProvider.GetRequiredService<IBlobStorageProvider>();
		var storage = blobEngine.FindBlobStorage(blobModel.BlobStorage ??
			throw new InvalidOperationException("BlobStorage is null"));
		await storage.SaveAsync(blobModel.BlobSource, blobModel.Container, blobInfo);

		blobInfo.Stream = null; // not stream in SQL
		return await SaveBlobInt(blobInfo, blobModel, setParams);
	}

	private Task<ExpandoObject> ParseFile(IModelBlob blobModel, Action<IBlobUpdateInfo> setBlob, Action<ExpandoObject>? setParams)
	{
		return blobModel.Parse switch
		{
			ModelParseType.xlsx or ModelParseType.excel => ParseXlsx(blobModel, setBlob, setParams),
			ModelParseType.json => ParseJson(blobModel, setBlob, setParams),
			ModelParseType.auto => ParseAuto(blobModel, setBlob, setParams),
			ModelParseType.csv => ParseFlat("csv", blobModel, setBlob, setParams),
			ModelParseType.dbf => ParseFlat("dbf", blobModel, setBlob, setParams),
			ModelParseType.xml => ParseFlat("xml", blobModel, setBlob, setParams),
			_ => throw new NotImplementedException(blobModel.Parse.ToString())
		};
	}
	private Task<ExpandoObject> ParseAuto(IModelBlob blobModel, Action<IBlobUpdateInfo> setBlob, Action<ExpandoObject>? setParams)
	{
		BlobUpdateInfo blobInfo = new();
		setBlob(blobInfo);
		if (blobInfo.Name == null)
			throw new InvalidOperationException("Name is null");
		var ext = Path.GetExtension(blobInfo.Name).ToLowerInvariant();
		return ext switch
		{
			".xlsx" => ParseXlsx(blobModel, setBlob, setParams),
			".json" => ParseJson(blobModel, setBlob, setParams),
			".csv" => ParseFlat("csv", blobModel, setBlob, setParams),
			".dbf" => ParseFlat("dbf", blobModel, setBlob, setParams),
			".xml" => ParseFlat("xml", blobModel, setBlob, setParams),
			_ => throw new NotImplementedException()
		};
	}

	private async Task<ExpandoObject> ParseJson(IModelBlob blobModel, Action<IBlobUpdateInfo> setBlob, Action<ExpandoObject>? setParams)
	{
		BlobUpdateInfo blobInfo = new();
		setBlob(blobInfo);
		if (blobInfo.Stream == null)
			throw new InvalidOperationException("Stream is null");
		String? json;
		if (blobModel.Zip)
			json = ZipUtils.DecompressText(blobInfo.Stream);
		else
		{
			using var sr = new StreamReader(blobInfo.Stream);
			json = sr.ReadToEnd();
		}
		if (json == null)
			throw new InvalidOperationException("Json is null");
		var data = JsonConvert.DeserializeObject<ExpandoObject>(json) ??
			throw new InvalidOperationException("Data is null");
		var prms = new ExpandoObject();
		if (blobModel.Id != null)
			prms.Add("Id", blobModel.Id);
		setParams?.Invoke(prms);
		var res = await _dbContext.SaveModelAsync(blobModel.DataSource, blobModel.UpdateProcedure(), data, prms, null, blobModel.CommandTimeout);
		return res.Root;
	}

	private async Task<ExpandoObject> ParseXlsx(IModelBlob blobModel, Action<IBlobUpdateInfo> setBlob, Action<ExpandoObject>? setParams)
	{
		BlobUpdateInfo blobInfo = new();
		setBlob(blobInfo);
		if (blobInfo.Stream == null)
			throw new InvalidOperationException("Stream is null");
		using var xp = new ExcelParser();
		var dm = xp.CreateDataModel(blobInfo.Stream);

		var prms = new ExpandoObject();
		if (blobModel.Id != null)
			prms.Add("Id", blobModel.Id);
		setParams?.Invoke(prms);
		var res = await _dbContext.SaveModelAsync(blobModel.DataSource, blobModel.UpdateProcedure(), dm.Data, prms, null, blobModel.CommandTimeout);
		return res.Root;
	}

	private async Task<ExpandoObject> ParseFlat(String format, IModelBlob blobModel, Action<IBlobUpdateInfo> setBlob, Action<ExpandoObject>? setParams)
	{
		BlobUpdateInfo blobInfo = new();
		setBlob(blobInfo);
		if (blobInfo.Stream == null)
			throw new InvalidOperationException("Stream is null");

		IFormatProvider formatProvider = CultureInfo.InvariantCulture;
		var rdr = _externalDataProvider.GetReader(format, null, blobInfo.Name);
		if (!blobModel.HasModel())
			return rdr.CreateDataModel(blobInfo.Stream);
		else
		{
			var prms = new ExpandoObject();
			setParams?.Invoke(prms);
			var dm = await _dbContext.SaveModelAsync(blobModel.DataSource, blobModel.UpdateProcedure(), [], prms,
				(table) =>
				{
					if (!String.IsNullOrEmpty(blobModel.Locale))
						table.FormatProvider = CultureInfo.GetCultureInfo(blobModel.Locale);
					return rdr.ParseFile(blobInfo.Stream, table);
				},
				blobModel.CommandTimeout
			);
			return dm?.Root ?? [];
		}
	}

	public async Task<ExpandoObject> DeleteBlobAsync(String baseUrl, Action<ExpandoObject>? setParams)
	{
		var platformUrl = CreatePlatformUrl(UrlKind.File, baseUrl);
		var blobModel = await _modelReader.GetBlobAsync(platformUrl)
			?? throw new DataServiceException($"Blob is null");
		return blobModel.Type switch
		{
			ModelBlobType.sql => await DeleteBlobSql(blobModel, setParams),
			ModelBlobType.blobStorage => await DeleteBlobStorage(blobModel, setParams),
			_ =>
				throw new NotImplementedException(blobModel.Type.ToString())
		};
	}


	Task<ExpandoObject> DeleteBlobSql(IModelBlob model, Action<ExpandoObject>? setParams)
	{
		throw new NotImplementedException();
	}
	Task<ExpandoObject> DeleteBlobStorage(IModelBlob model, Action<ExpandoObject>? setParams)
	{
		throw new NotImplementedException();
	}
}
