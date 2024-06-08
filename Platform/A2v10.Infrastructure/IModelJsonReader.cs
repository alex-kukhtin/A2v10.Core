// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;

namespace A2v10.Infrastructure;

public enum AutoRender 
{ 
	Page,
	Dialog
}
public interface IModelJsonAuto
{
	public AutoRender Render { get; }
}

// as model.json.schema
public enum PermissionBits
{
	View = PermissionFlag.CanView,
	Edit = PermissionFlag.CanEdit,
	Delete = PermissionFlag.CanDelete,
	Apply = PermissionFlag.CanApply,
	Unapply = PermissionFlag.CanUnapply,
	Create = PermissionFlag.CanCreate,
	Flag64 = PermissionFlag.CanFlag64,
	Flag128 = PermissionFlag.CanFlag128,
	Flag256 = PermissionFlag.CanFlag256,
}

public interface IModelBase
{
	[Flags]
	enum ParametersFlags
	{
		None = 0x00,
		SkipId = 0x01,
		SkipModelJsonParams = 0x02,
	}

	String? DataSource { get; }
	String? CurrentModel { get; }
	Boolean Signal { get; }

	String LoadProcedure();
    String ExportProcedure();
    String UpdateProcedure();
    Boolean HasModel();

	Boolean CheckRoles(IEnumerable<String>? roles);
	String Path { get; }
	String BaseUrl { get; }
	Int32 CommandTimeout { get; }
	IModelJsonAuto? ModelAuto { get; }
	ExpandoObject CreateParameters(IPlatformUrl url, Object? id, Action<ExpandoObject>? setParams = null, ParametersFlags flags = ParametersFlags.None);
	Dictionary<String, PermissionBits>? Permissions { get; }
}

public enum ModelBlobType
{
    sql,
    json,
	clr,
	parse,
	blobStorage,
	excel
}

public enum ModelParseType
{
	none,
    json,
	xlsx,
	excel,
	csv,
	dbf,
	xml,
	auto
}

public interface IModelBlob : IModelBase
{
	String? Id { get; }
	String? Key { get; }    
	ModelBlobType Type { get; }	
	ModelParseType Parse { get; }
    String? ClrType { get; }
    String? OutputFileName { get; }
	Boolean Zip { get; }
	String? BlobStorage { get; }
	String? BlobSource { get; }
	String? Container { get; }
	String? Locale { get; }
	ExpandoObject? Parameters { get; }
	String DeleteBlobProcedure();
	String LoadBlobProcedure();
	String UpdateBlobProcedure();
    
}

public interface IModelMerge : IModelBase
{
	ExpandoObject CreateMergeParameters(IDataModel model, ExpandoObject prms);
}

public enum ModelJsonExportFormat
{
    unknown,
    xlsx,
    dbf,
    csv
}

public interface IModelExport
{
    String? FileName { get;  }
    String? Template { get; }
	ModelJsonExportFormat Format { get; }
    String? Encoding { get; }
	String? GetTemplateExpression();
	Encoding GetEncoding();
}

public interface IModelView : IModelBase
{
	Boolean Copy { get; }
	String? Template { get; }
	String? EndpointHandler { get; }
    IModelExport? Export { get; }
    Boolean Indirect { get; }
	String? Target { get; }
	String? TargetId { get; }
	IModelView? TargetModel { get; }

	IModelMerge? Merge { get; }

	List<String>? Scripts { get; }
	List<String>? Styles { get; }

	String GetView(Boolean bMobile);
	String? GetRawView(Boolean bMobile);
	Boolean IsDialog { get; }
	Boolean IsIndex { get; }

	Boolean IsSkipDataStack { get; }

	Boolean IsPlain { get; }
	String? SqlTextKey();
	String ExpandProcedure();
	String LoadLazyProcedure(String property);
	String DeleteProcedure(String? property);

	IModelView Resolve(IDataModel model);
}

public interface IModelInvokeCommand
{
	Task<IInvokeResult> ExecuteAsync(IModelCommand command, ExpandoObject parameters);
}

public interface IModelCommand : IModelBase
{
	IModelInvokeCommand GetCommandHandler(IServiceProvider serviceProvider);

	String? Target { get; }
	String? File { get; }
	String? ClrType { get; }
	ExpandoObject? Args { get; }
}

public interface IModelReportHandler
{
	Task<IInvokeResult> ExportAsync(IModelReport report, ExportReportFormat format, ExpandoObject? query, Action<ExpandoObject> setParams);
	Task<IReportInfo> GetReportInfoAsync(IModelReport report, ExpandoObject? query, Action<ExpandoObject> setParams);
}

public interface IModelReport : IModelBase
{
	String? Name { get; }
	String? Report { get; }

	IModelReportHandler GetReportHandler(IServiceProvider serviceProvider);

	ExpandoObject CreateParameters(ExpandoObject? query, Action<ExpandoObject> setParams);
	ExpandoObject CreateVariables(ExpandoObject? query, Action<ExpandoObject> setParams);
}

public interface IModelJsonReader
{
	Task<IModelView?> TryGetViewAsync(IPlatformUrl url);
	Task<IModelView> GetViewAsync(IPlatformUrl url);
	Task<IModelBlob?> GetBlobAsync(IPlatformUrl url, String? suffix = null);
	Task<IModelCommand> GetCommandAsync(IPlatformUrl url, String command);
	Task<IModelReport> GetReportAsync(IPlatformUrl url);
}

