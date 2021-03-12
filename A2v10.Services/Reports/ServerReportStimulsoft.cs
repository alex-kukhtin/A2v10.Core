// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Services
{
	public class ServerReportStimulsoft : IModelReportHandler
	{
		private readonly IExternalReport _externalReport;
		private readonly IAppCodeProvider _appCodeProvider;
		private readonly IDbContext _dbContext;

		public ServerReportStimulsoft(IExternalReport externalReport, IAppCodeProvider appCodeProvider, IDbContext dbContext)
		{
			_externalReport = externalReport;
			_appCodeProvider = appCodeProvider;
			_dbContext = dbContext;
		}

		public async Task<IInvokeResult> ExportAsync(IModelReport report, ExportReportFormat format, ExpandoObject query, Action<ExpandoObject> setParams)
		{
			var vars = report.CreateVariables(query, setParams);
			var prms = report.CreateParameters(query, setParams);

			String repPath = report.ReportPath();
			if (repPath != null)
				repPath = _appCodeProvider.MakeFullPath(report.Path, $"{repPath}.mrt");

			IDataModel dm = null;
			if (report.HasModel())
				dm = await _dbContext.LoadModelAsync(report.DataSource, report.LoadProcedure(), prms);

			var ri = new ExternalReportInfo()
			{
				ReportPath = repPath,
				Name = report.Name,
				DataModel = dm,
				Variables = vars.IsEmpty() ? null : vars
			};
			return await _externalReport.Export(ri, format);
		}
	}
}
