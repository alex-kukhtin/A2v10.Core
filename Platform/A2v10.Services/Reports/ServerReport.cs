﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

using A2v10.Data.Interfaces;

namespace A2v10.Services;
public class ServerReport : IModelReportHandler
{
    private readonly IReportEngine _engine;
    private readonly IDbContext _dbContext;

    public ServerReport(IReportEngine engine, IDbContext dbContext)
    {
        _engine = engine;
        _dbContext = dbContext;
    }

    public async Task<IInvokeResult> ExportAsync(IModelReport report, ExportReportFormat format, ExpandoObject? query, Action<ExpandoObject> setParams)
    {
        var info = await GetReportInfoAsync(report, query, setParams);
        return await _engine.ExportAsync(info, format);
    }

    public async Task<IReportInfo> GetReportInfoAsync(IModelReport report, ExpandoObject? query, Action<ExpandoObject> setParams)
    {
        var vars = report.CreateVariables(query, setParams);
        var prms = report.CreateParameters(query, setParams);

        IDataModel? dm = null;
        if (report.HasModel())
            dm = await _dbContext.LoadModelAsync(report.DataSource, report.LoadProcedure(), prms);

        if (String.IsNullOrEmpty(report.Report))
            throw new DataServiceException("Report is null");

        return new ExternalReportInfo(report: report.Report, path: report.Path)
        {
            Name = report.Name,
            DataModel = dm,
            Variables = vars.IsEmpty() ? null : vars
        };
    }
}

