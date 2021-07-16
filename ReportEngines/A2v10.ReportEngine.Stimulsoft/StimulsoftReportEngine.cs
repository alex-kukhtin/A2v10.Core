// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Stimulsoft.Report;
using Stimulsoft.Report.Mvc;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using Stimulsoft.Base;
using Microsoft.Extensions.Configuration;

namespace A2v10.ReportEngine.Stimulsoft
{
	public class StimulsoftReportEngine : IReportEngine
	{
		private readonly IAppCodeProvider _appCodeProvider;
		private readonly IUserStateManager _userStateManager;

		public StimulsoftReportEngine(IAppCodeProvider appCodeProvider, IUserStateManager userStateManager)
		{
			_appCodeProvider = appCodeProvider;
			_userStateManager = userStateManager;
		}

		internal StiReport CreateReport(IReportInfo reportInfo)
		{
			String reportPath = _appCodeProvider.MakeFullPath(reportInfo.Path, $"{reportInfo.Report}.mrt", _userStateManager.IsAdmin);

			var rep = new StiReport();
			if (!String.IsNullOrEmpty(reportPath))
				rep.Load(reportPath);
			else if (reportInfo.Stream != null)
				rep.Load(reportInfo.Stream);
			else
				throw new InvalidOperationException("Тeither Path nor Stream is defined");
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

		static public void AddDataModel(StiReport report, IDataModel dm)
		{
			if (dm == null)
				return;
			var dynModel = dm.GetDynamic();
			foreach (var x in dynModel)
				report.RegBusinessObject(x.Key, x.Value);
		}

		static public void AddVariables(StiReport report, ExpandoObject vars)
		{
			if (vars == null)
				return;
			if (report?.Dictionary?.Variables?.Items == null)
				return;
			foreach (var vp in vars)
			{
				if (vp.Value != null)
				{
					foreach (var xs in report?.Dictionary?.Variables?.Items)
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

			var res = new StimulsoftInvokeResult()
			{
				Body = result.Data,
				ContentType = result.ContentType,
				FileName = result.FileName
			};
			return Task.FromResult<IInvokeResult>(res);
		}
	}
}
