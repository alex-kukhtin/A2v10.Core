// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace A2v10.Infrastructure
{
    public enum ProfileAction
    {
        Sql,
        Render,
        Workflow,
        Script,
        Report,
        Exception
    };

    public interface IProfileRequest
    {
        IDisposable? Start(ProfileAction kind, String description);
        void Stop();
    }

    public interface IProfiler
    {
        Boolean Enabled { get; set; }

        IProfileRequest? BeginRequest(String address, String? session);
        IProfileRequest CurrentRequest { get; }
        void EndRequest(IProfileRequest? request);

        String? GetJson();
    }
}
