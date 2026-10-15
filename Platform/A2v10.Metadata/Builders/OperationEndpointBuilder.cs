// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* Read-only, one action. There is no SaveModelAsync and no dispatch to one: the operation
 * registry is written by the application's own metadata, never through this dialog.
 */
internal class OperationEndpointBuilder(IServiceProvider _serviceProvider,
    OperationEndpointMetadata _endpoint, IPlatformUrl _platformUrl, String? _dataSource) : IModelBuilder
{
    private readonly SqlBuilderOperations _sqlBuilder = new(_serviceProvider);

    public String Path => _endpoint.Path;

    /* Fetch and nothing else. The registry is a reference target, so a selector pointing here
     * types into it - that is the one command it has to answer.
     *
     * 'inherit' is refused rather than ignored: it names columns of THIS table that the picking
     * row wants carried along, and the registry has none to give. Ignoring it would hand back a
     * row missing the fields the handler on the other side is about to assign from.
     */
    public Task<IInvokeResult> InvokeAsync(IModelCommand cmd, String command, ExpandoObject? prms)
    {
        if (!String.Equals(command, "fetch", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"'{command}' is not supported for '{Path}'");
        if (!String.IsNullOrEmpty(prms?.Get<String>("inherit")))
            throw new NotSupportedException($"'inherit' is not supported for '{Path}'");
        return _sqlBuilder.FetchAsync(_dataSource, prms);
    }

    public async Task<IAppRuntimeResult> RenderAsync(IModelView view, Boolean isReload)
    {
        if (_platformUrl.Action != OperationEndpointMetadata.BrowseAction)
            throw new InvalidOperationException($"Operations. Unsupported action '{_platformUrl.Action}'");

        var dm = await _sqlBuilder.LoadBrowseModel(_dataSource);
        if (isReload)
            return new AppRuntimeResult(dm, null);
        var request = new PageRequest(_endpoint, _platformUrl, _dataSource, view, dm, null);
        return new AppRuntimeResult(dm, await IPageRenderer.Of(_serviceProvider, Path).RenderPageAsync(request));
    }
}
