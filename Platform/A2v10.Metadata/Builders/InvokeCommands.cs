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
            "deletefolder" => _sqlBuilder.DeleteFolderAsync(prms),
            "unapply" or "unpost" => UnPostAsync(prms),
            var s when s.EndsWith(".unique") => _sqlBuilder.CheckUniqueAsync(prms, command.Split('.')[0]),
            _ => throw IModelBuilder.NotHere(command, Path)
        };
    }

    // a document posts; anything else is refused as every builder refuses what it does not serve
    private Task<IInvokeResult> PostAsync(ExpandoObject? prms)
    {
        if (!Table.IsDocument)
            throw IModelBuilder.NotHere("post", Path);
        return _sqlBuilder.PostDocumentAsync(prms);
    }

    private Task<IInvokeResult> UnPostAsync(ExpandoObject? prms)
    {
        if (!Table.IsDocument)
            throw IModelBuilder.NotHere("unpost", Path);
        return _sqlBuilder.UnPostDocumentAsync(prms);
    }
}
