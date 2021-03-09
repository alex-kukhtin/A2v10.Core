using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

		public async Task<IInvokeResult> ExportAsync(IModelReport report, ExportReportFormat format, Action<ExpandoObject> setParams)
		{
			ExpandoObject vars = new();
			vars.Append(report.Variables);
			//vars.Append(prms)

			ExpandoObject prms = new();
			setParams?.Invoke(prms);

			String repPath = report.ReportPath();
			if (repPath != null)
				repPath = _appCodeProvider.MakeFullPath(report.BaseUrl, $"{repPath}.mrt");

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
