// Copyright © 2021-2023 Olekdsandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;
using System.IO;

using Stimulsoft.Report;
using Stimulsoft.Report.Mvc;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.ReportEngine.Stimulsoft;

public class StimulsoftReportEngine : IReportEngine
{
	private readonly IAppCodeProvider _appCodeProvider;
	private readonly ICurrentUser _currentUser;

	public StimulsoftReportEngine(IAppCodeProvider appCodeProvider, ICurrentUser currentUser)
	{
		_appCodeProvider = appCodeProvider;
		_currentUser = currentUser;
	}

	internal StiReport CreateReport(IReportInfo reportInfo)
	{
		var repstream = _appCodeProvider.FileStreamRO(Path.Combine(reportInfo.Path, $"{reportInfo.Report}.mrt"));
		var rep = new StiReport();
		if (repstream != null)
			rep.Load(repstream);
		else if (reportInfo.Stream != null)
			rep.Load(reportInfo.Stream);
		else
			throw new InvalidOperationException("Neither Path nor Stream is defined");
		AddReferencedAssemblies(rep);
		//rep.SubstDataSources();
		if (!String.IsNullOrEmpty(reportInfo.Name))
			rep.ReportName = reportInfo.Name;
		AddDataModel(rep, reportInfo.DataModel);
		AddVariables(rep, reportInfo.Variables);
		return rep;
	}

	static void AddReferencedAssemblies(StiReport rep)
	{
		Int32 asCount = 1;
		Int32 len = rep.ReferencedAssemblies.Length;
		var ra = new String[len + asCount];
		Array.Copy(rep.ReferencedAssemblies, ra, len);
		ra[len] = "A2v10.Infrastructure";
		rep.ReferencedAssemblies = ra;
	}

	static public void AddDataModel(StiReport report, IDataModel? dm)
	{
		if (dm == null)
			return;
		var dynModel = dm.GetDynamic();
		foreach (var x in dynModel)
			report.RegBusinessObject(x.Key, x.Value);
	}

	static public void AddVariables(StiReport report, ExpandoObject? vars)
	{
		if (vars == null)
			return;
		var items = report?.Dictionary?.Variables?.Items;
		if (items == null)
			return;
		foreach (var vp in vars)
		{
			if (vp.Value != null)
			{
				foreach (var xs in items)
				{
					if (xs.Name == vp.Key)
					{
						xs.ValueObject = vp.Value;
						xs.Type = vp.Value.GetType();
					}
				}
			}
		}
	}

	public Task<IInvokeResult> ExportAsync(IReportInfo reportInfo, ExportReportFormat format)
	{
		var rep = CreateReport(reportInfo);

		StiNetCoreActionResult result = format switch {
			ExportReportFormat.Pdf =>
				StiNetCoreReportResponse.ResponseAsPdf(rep, StimulsoftReportSettings.PdfExportSettings),
			ExportReportFormat.Excel =>
				StiNetCoreReportResponse.ResponseAsExcel2007(rep, StimulsoftReportSettings.ExcelExportSettings),
			ExportReportFormat.Word =>
				StiNetCoreReportResponse.ResponseAsWord2007(rep, StimulsoftReportSettings.WordExportSettings),
			ExportReportFormat.OpenText =>
				StiNetCoreReportResponse.ResponseAsOdt(rep, StimulsoftReportSettings.OdtExportSettings),
			ExportReportFormat.OpenSheet =>
				StiNetCoreReportResponse.ResponseAsOds(rep, StimulsoftReportSettings.OdsExportSettings),
			_ =>
				throw new NotImplementedException($"Format '{format}' is not supported in this version")
		};

		var res = new StimulsoftInvokeResult(
			Body: result.Data,
			ContentType: result.ContentType,
			FileName: result.FileName
		);
		return Task.FromResult<IInvokeResult>(res);
	}
}
