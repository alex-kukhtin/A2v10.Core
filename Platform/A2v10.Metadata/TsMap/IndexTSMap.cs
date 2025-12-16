// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class IndexModelBuilder
{
    internal Task<String> CreateMapTS()
    {

        var templ = $$"""
        export interface TRoot extends IRoot {
        }
        """;
        return Task.FromResult<String>(templ);
    }
}
