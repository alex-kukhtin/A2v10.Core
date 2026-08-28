// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* What an endpoint can be asked to do. Every action refuses by default, so a builder declares
 * only what it actually serves and its own surface stops lying about the rest: a default is
 * reachable through the interface and never through the class, so nobody can call a refusal on a
 * concrete builder by accident. See CLAUDE.md, "System endpoints".
 *
 * The one member without a default is 'Path'. A builder must be able to say where it is, because
 * that is what every refusal below is worded with - and there is nothing else all of them have.
 * A builder that renders nothing is still a builder; commands will need exactly that.
 */
internal interface IModelBuilder
{
    String Path { get; }

    private static NotSupportedException NotHere(String action, String path) =>
        new($"'{action}' is not supported for '{path}'");

    /* Load and render are one member and not two: the split is the normal builder's business, and
     * a report has no seam there at all - it fetches and renders in one pass. Two members forced
     * the caller to know which shape it was holding.
     */
    Task<IAppRuntimeResult> RenderAsync(IModelView view, Boolean isReload) =>
        throw NotHere(nameof(RenderAsync), Path);

    Task<ExpandoObject> SaveModelAsync(ExpandoObject data, ExpandoObject savePrms) =>
        throw NotHere(nameof(SaveModelAsync), Path);

    Task<IDataModel> LoadLazyModelAsync() =>
        throw NotHere(nameof(LoadLazyModelAsync), Path);

    Task<IDataModel> ExpandAsync(ExpandoObject expandPrms) =>
        throw NotHere(nameof(ExpandAsync), Path);

    Task<IInvokeResult> InvokeAsync(IModelCommand cmd, String command, ExpandoObject? prms) =>
        throw NotHere(nameof(InvokeAsync), Path);

    Task DbRemoveAsync(String? propName, ExpandoObject execPrms) =>
        throw NotHere(nameof(DbRemoveAsync), Path);
}

internal interface IEndpointModelBuilder
{
    Task<String> CreateTemplateTSAsync();
    Task<String> CreateMapTSAsync();
}
