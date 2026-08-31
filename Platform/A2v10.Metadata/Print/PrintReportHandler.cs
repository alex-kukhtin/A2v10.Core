// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Infrastructure;
using A2v10.Services;

namespace A2v10.Metadata;

internal class PrintReportHandler(IReportEngineProvider _reportEngineProvider, DatabaseMetadataProvider _metadataProvider, IAppCodeProvider _appCodeProvider,
    IServiceProvider _serviceProvider) : IModelReportHandler
{
    public async Task<IInvokeResult> ExportAsync(IModelReport report, ExportReportFormat format, ExpandoObject? query, Action<ExpandoObject> setParams)
    {
        var reportEngine = _reportEngineProvider.FindReportEngine(format.ToString().ToLowerInvariant())
            ?? throw new InvalidOperationException($"PrintReportHandler: ReportEngine '{format}' not found");
        var repInfo = await GetReportInfoAsync(report, query, setParams);
        return await reportEngine.ExportAsync(repInfo, format);
    }

    public async Task<IReportInfo> GetReportInfoAsync(IModelReport report, ExpandoObject? query, Action<ExpandoObject> setParams)
    {
        var vars = report.CreateVariables(query, setParams);
        var prms = report.CreateParameters(query, setParams);

        var baseUrl = query?.Get<String>("Base")
            ?? throw new InvalidOperationException("PrintReportHandler: Base is null");
        var repName = query?.Get<String>("Rep")
            ?? throw new InvalidOperationException("PrintReportHandler: Rep is null");
        var endpointInfo = await _metadataProvider.GetModelInfoFromPathAsync(baseUrl);
        var endpoint = await _metadataProvider.GetNormalEndpointAsync(report.DataSource, endpointInfo.Schema, endpointInfo.Table);

        var printTemplate = endpoint.Declaration.PrintForm(repName);

        var filePath = PrintRequest.FileOf(endpoint, printTemplate.Path);
        var stream = _appCodeProvider.FileStreamRO(filePath)
            ?? throw new InvalidOperationException($"print: blank '{filePath}' not found");

        var bd = new BuilderDescriptor()
        {
            Endpoint = endpoint,
            DataSource = report.DataSource,
            PlatformUrl = new PlatformUrl(UrlKind.Page, report.BaseUrl.Trim('/'))
        };
        var sqlBuilder = new SqlBuilder(bd, _serviceProvider);
        var dm = await sqlBuilder.LoadPrintModelAsync(printTemplate);

        return new ExternalReportInfo(report: report.Report ?? "report", path: report.Path)
        {
            Name = printTemplate.Title,          
            Stream = stream,
            DataModel = dm,
            Variables = vars.IsEmpty() ? null : vars
        };
    }
}
