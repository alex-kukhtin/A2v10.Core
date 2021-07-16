
using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Services
{
	public class ReportService  : IReportService
	{
		private readonly IModelJsonReader _modelReader;
		private readonly IServiceProvider _serviceProvider;
		private readonly IProfiler _profiler;

		public ReportService(IServiceProvider serviceProvider, IModelJsonReader modelReader, IProfiler profiler)
		{
			_serviceProvider = serviceProvider;
			_modelReader = modelReader;
			_profiler = profiler;
		}

		IPlatformUrl CreatePlatformUrl(String baseUrl)
		{
			var url = new PlatformUrl(UrlKind.Report, baseUrl);
			return url;
		}

		public async Task<IInvokeResult> ExportAsync(String url, ExportReportFormat format, Action<ExpandoObject> setParams)
		{
			var platrformUrl = CreatePlatformUrl(url);
			using var _ = _profiler.CurrentRequest.Start(ProfileAction.Report, $"export: {platrformUrl.Action}");
			var rep = await _modelReader.GetReportAsync(platrformUrl);
			var handler = rep.GetReportHandler(_serviceProvider);
			return await handler.ExportAsync(rep, format, platrformUrl.Query, setParams);
		}

		public async Task<IReportInfo> GetReportInfoAsync(String url, Action<ExpandoObject> setParams)
		{
			var platrformUrl = CreatePlatformUrl(url);
			using var _ = _profiler.CurrentRequest.Start(ProfileAction.Report, $"export: {platrformUrl.Action}");
			var rep = await _modelReader.GetReportAsync(platrformUrl);
			var handler = rep.GetReportHandler(_serviceProvider);
			return await handler.GetReportInfoAsync(rep, platrformUrl.Query, setParams);
		}
	}
}
