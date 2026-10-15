// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* The attachments of a record under ?base=: the owner is the url's id. The base has been checked by the
 * time a call arrives - the synthesized model.json says requireBase, so DataService runs BaseAccess
 * through the reader. The upload is not here: it is BlobService's, through the door the synthesized
 * model.json carries (AttachmentEndpointMetadata.Blobs)
 */
internal class AttachmentEndpointBuilder(IServiceProvider _serviceProvider, AttachmentEndpointMetadata _endpoint,
    IPlatformUrl _platformUrl, String? _dataSource, AppPlatformId _platformId, Boolean _useGrants) : IModelBuilder
{
    private readonly SqlBuilderAttachments _sqlBuilder = new(_serviceProvider, _platformId, _useGrants);

    public String Path => _endpoint.Path;

    public async Task<IAppRuntimeResult> RenderAsync(IModelView view, Boolean isReload)
    {
        if (_platformUrl.Action != AttachmentEndpointMetadata.IndexAction)
            throw IModelBuilder.NotHere(_platformUrl.Action, Path);

        var dm = await _sqlBuilder.LoadAsync(_dataSource, _platformUrl.Id,
            _platformUrl.Query?.Get<String>(Constants.FieldNames.BaseQuery));
        if (isReload)
            return new AppRuntimeResult(dm, null);
        var request = new PageRequest(_endpoint, _platformUrl, _dataSource, view, dm, AttachmentsTemplateBuilder.IndexTemplate);
        return new AppRuntimeResult(dm, await IPageRenderer.Of(_serviceProvider, Path).RenderPageAsync(request));
    }

    // the posted Base is the one DataService checked (InvokeAsync reads it from the final parameters)
    public Task<IInvokeResult> InvokeAsync(IModelCommand cmd, String command, ExpandoObject? prms) =>
        String.Equals(command, AttachmentsTemplateBuilder.DeleteCommand, StringComparison.OrdinalIgnoreCase)
            ? _sqlBuilder.DeleteAsync(_dataSource, prms)
            : throw IModelBuilder.NotHere(command, Path);
}
