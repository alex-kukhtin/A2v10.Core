// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using A2v10.Data.Interfaces;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.Infrastructure
{
	public interface IModelBase
	{
		String DataSource { get; }

		String LoadProcedure();
		Boolean HasModel();

		String Path { get; }
		String BaseUrl { get; }
	}

	public interface IModelBlob
	{
		String DataSource { get; }
		String LoadProcedure();
		String Id { get; }
		String Key { get; }
	}

	public interface IModelView: IModelBase
	{
		Boolean Copy { get; }
		String Template { get; }

		Boolean Indirect { get; }
		String Target { get; }
		String TargetId { get; }
		IModelView TargetModel { get; }

		ExpandoObject Parameters { get; }

		IModelBase Merge { get; }

		String GetView(Boolean bMobile);
		Boolean IsDialog { get; }

		String ExpandProcedure();
		String UpdateProcedure();
		String LoadLazyProcedure(String property);
		String DeleteProcedure(String property);

		IModelView Resolve(IDataModel model);
	}

	public interface IModelInvokeCommand
	{
		Task<IInvokeResult> ExecuteAsync(IModelCommand command, ExpandoObject parameters);
	}

	public interface IModelCommand : IModelBase
	{
		ExpandoObject Parameters { get; }
		IModelInvokeCommand GetCommandHandler(IServiceProvider serviceProvider);
	}

	public enum ModelReportType
	{
		stimulsoft,
		xml,
		json
	}

	public interface IModelReportHandler
	{
		Task<IInvokeResult> ExportAsync(IModelReport report, ExportReportFormat format, Action<ExpandoObject> setParams);
	}

	public interface IModelReport : IModelBase
	{
		String Name { get; }
		ExpandoObject Variables { get; }

		String ReportPath();

		IModelReportHandler GetReportHandler(IServiceProvider serviceProvider);
	}

	public interface IModelJsonReader
	{
		Task<IModelView> GetViewAsync(IPlatformUrl url);
		Task<IModelBlob> GetBlobAsync(IPlatformUrl url, String suffix = null);
		Task<IModelCommand> GetCommandAsync(IPlatformUrl url, String command);
		Task<IModelReport> GetReportAsync(IPlatformUrl url);
	}
}
