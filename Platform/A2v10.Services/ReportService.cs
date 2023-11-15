
using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Services
{
	public class ReportService(IServiceProvider serviceProvider, IModelJsonReader modelReader, IProfiler profiler) : IReportService
	{
		private readonly IModelJsonReader _modelReader = modelReader;
		private readonly IServiceProvider _serviceProvider = serviceProvider;
		private readonly IProfiler _profiler = profiler;

        static PlatformUrl CreatePlatformUrl(String baseUrl)
		{
			return new PlatformUrl(UrlKind.Report, baseUrl);
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
