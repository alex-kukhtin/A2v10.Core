// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Dynamic;

namespace A2v10.Infrastructure;
public interface IApplicationHost
{
    Boolean Mobile { get; }
    Boolean IsAdminMode { get; set; }

    Boolean IsDebugConfiguration { get; }

    Boolean IsProductionEnvironment { get; }

    Boolean IsRegistrationEnabled { get; }
    Boolean IsAdminAppPresent { get; }

    Boolean IsMultiTenant { get; }
    Boolean IsMultiCompany { get; }
    Boolean IsUsePeriodAndCompanies { get; }

    String? CatalogDataSource { get; }
    String? TenantDataSource { get; }

    IApplicationReader ApplicationReader { get; }

    void StartApplication(Boolean isAdmin);

    String? GetAppSettings(String? source);
    ExpandoObject GetEnvironmentObject(String key);

    void CheckIsMobile(String host);
}

