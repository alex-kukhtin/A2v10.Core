// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Infrastructure;

namespace A2v10.Cli;

// the renderer times what it does; one command is no request worth timing
internal class NullProfiler : IProfiler
{
    private sealed class NullRequest : IProfileRequest
    {
        public IDisposable? Start(ProfileAction kind, String description) => null;
        public void Stop() { }
    }

    public Boolean Enabled { get; set; }
    public IProfileRequest? BeginRequest(String address, String? session) => null;
    public IProfileRequest CurrentRequest { get; } = new NullRequest();
    public void EndRequest(IProfileRequest? request) { }
    public String? GetJson() => null;
}
