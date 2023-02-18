// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Dynamic;
using A2v10.Infrastructure;

namespace A2v10.Services
{
    public static class ParameterBuilder
    {
        public static class WellKnownParamNames
        {
            public const String TenantId = "TenantId";
            public const String UserId = "UserId";
            public const String CompanyId = "CompanyId";
        }

        public static ExpandoObject BuildIndirectParams(IPlatformUrl url, Action<ExpandoObject> setParams)
        {
            var result = new ExpandoObject();
            result.SetNotNull("Id", url.Id);
            setParams?.Invoke(result);
            return result;
        }
    }
}
