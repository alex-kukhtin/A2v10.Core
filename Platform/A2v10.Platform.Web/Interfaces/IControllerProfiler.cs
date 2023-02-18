// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using A2v10.Infrastructure;

namespace A2v10.Platform.Web;

public interface IControllerProfiler
{
    IProfiler Profiler { get; }
    IProfileRequest? BeginRequest();
    void EndRequest(IProfileRequest? request);
}
