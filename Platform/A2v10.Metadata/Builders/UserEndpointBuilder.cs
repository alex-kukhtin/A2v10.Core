// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* The picker of logins: browse and fetch, nothing written. A login is created and blocked on
 * /admin/user, never through a reference. 'inherit' is refused for the reason the operations refuse
 * it: the picking row would wait for columns a login has none of.
 */
internal class UserEndpointBuilder(IServiceProvider _serviceProvider,
    UserEndpointMetadata _endpoint, IPlatformUrl _platformUrl, String? _dataSource) : IModelBuilder
{
    private readonly SqlBuilderUserPicker _sqlBuilder = new(_serviceProvider);

    public String Path => _endpoint.Path;

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
        if (_platformUrl.Action != UserEndpointMetadata.BrowseAction)
            throw new InvalidOperationException($"Users. Unsupported action '{_platformUrl.Action}'");

        var dm = await _sqlBuilder.LoadBrowseModel(_dataSource);
        if (isReload)
            return new AppRuntimeResult(dm, null);
        var request = new PageRequest(_endpoint, _platformUrl, _dataSource, view, dm, null);
        return new AppRuntimeResult(dm, await IPageRenderer.Of(_serviceProvider, Path).RenderPageAsync(request));
    }
}
