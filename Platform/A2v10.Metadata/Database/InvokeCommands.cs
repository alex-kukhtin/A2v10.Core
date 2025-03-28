// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder
{
    public Task<IInvokeResult> InvokeAsync(IModelCommand cmd, String command, ExpandoObject? prms)
    {
        return command.ToLowerInvariant() switch
        {
            "apply" => ApplyAsync(prms),
            _ => throw new NotImplementedException($"Implement invoke for {command}")
        };
    }

    private Task<IInvokeResult> ApplyAsync(ExpandoObject? prms)
    {
        if (_table.Schema != "doc")
            throw new NotImplementedException($"The apply command is available for documents only");
        return ApplyDocumentAsync(prms);
    }
}
