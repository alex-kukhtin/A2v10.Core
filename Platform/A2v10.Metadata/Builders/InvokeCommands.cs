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
            "apply" or "post" => PostAsync(prms),
            "fetch" => _sqlBuilder.FetchAsync(prms),
            "fetchfolder" => _sqlBuilder.FetchFolderAsync(prms),
            "unapply" or "unpost" => UnPostAsync(prms),
            var s when s.EndsWith(".unique") => _sqlBuilder.CheckUniqueAsync(prms, command.Split('.')[0]),
            _ => throw new NotImplementedException($"Implement invoke for {command}")
        };
    }

    private Task<IInvokeResult> PostAsync(ExpandoObject? prms)
    {
        if (!Table.IsDocument)
            throw new NotImplementedException($"The Post command is available for documents only");
        return _sqlBuilder.PostDocumentAsync(prms); 
    }

    private Task<IInvokeResult> UnPostAsync(ExpandoObject? prms)
    {
        if (!Table.IsDocument)
            throw new NotImplementedException($"The UnPost command is available for documents only");
        return _sqlBuilder.UnPostDocumentAsync(prms);
    }
}
