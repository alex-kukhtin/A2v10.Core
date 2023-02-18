// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public enum ExportReportFormat
{
    Undefined,
    Pdf,
    Excel,
    Word,
    OpenSheet,
    OpenText
};

public interface IReportService
{
    Task<IInvokeResult> ExportAsync(String url, ExportReportFormat format, Action<ExpandoObject> setParams);
    Task<IReportInfo> GetReportInfoAsync(String url, Action<ExpandoObject> setParams);
}
