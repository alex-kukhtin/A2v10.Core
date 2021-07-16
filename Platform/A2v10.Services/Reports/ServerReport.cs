// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Services
{
	public class ServerReport : IModelReportHandler
	{
		private readonly IReportEngine _engine;
		private readonly IDbContext _dbContext;

		public ServerReport(IReportEngine engine, IDbContext dbContext)
		{
			_engine = engine;
			_dbContext = dbContext;
		}

		public async Task<IInvokeResult> ExportAsync(IModelReport report, ExportReportFormat format, ExpandoObject query, Action<ExpandoObject> setParams)
		{
			var info = await GetReportInfoAsync(report, query, setParams);
			return await _engine.ExportAsync(info, format);
		}

		public async Task<IReportInfo> GetReportInfoAsync(IModelReport report, ExpandoObject query, Action<ExpandoObject> setParams)
		{
			var vars = report.CreateVariables(query, setParams);
			var prms = report.CreateParameters(query, setParams);

			IDataModel dm = null;
			if (report.HasModel())
				dm = await _dbContext.LoadModelAsync(report.DataSource, report.LoadProcedure(), prms);

			return new ExternalReportInfo()
			{
				Path = report.Path,
				Name = report.Name,
				Report = report.Report,
				DataModel = dm,
				Variables = vars.IsEmpty() ? null : vars
			};
		}
	}
}
