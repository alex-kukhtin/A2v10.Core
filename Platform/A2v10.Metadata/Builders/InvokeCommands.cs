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
            "fetch" => _sqlBuilder.FetchAsync(prms),
            "fetchfolder" => _sqlBuilder.FetchFolderAsync(prms),
            "unapply" => UnApplyAsync(prms),
            var s when s.EndsWith(".unique") => _sqlBuilder.CheckUniqueAsync(prms, command.Split('.')[0]),
            _ => throw new NotImplementedException($"Implement invoke for {command}")
        };
    }

    private Task<IInvokeResult> ApplyAsync(ExpandoObject? prms)
    {
        if (!Table.IsDocument)
            throw new NotImplementedException($"The Apply command is available for documents only");
        return _sqlBuilder.ApplyDocumentAsync(prms); 
    }

    private Task<IInvokeResult> UnApplyAsync(ExpandoObject? prms)
    {
        if (!Table.IsDocument)
            throw new NotImplementedException($"The UnApply command is available for documents only");
        return _sqlBuilder.UnApplyDocumentAsync(prms);
    }
}
