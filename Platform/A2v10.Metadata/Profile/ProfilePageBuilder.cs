// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;
using A2v10.Services;

namespace A2v10.Metadata;

/* /_profile. No right to check: every query is keyed by the current user (SqlBuilderProfile), and the
 * account is written by Identity for the current user (IAppUserProfile).
 */
internal class ProfilePageBuilder(IServiceProvider _serviceProvider, ProfilePageEndpointMetadata _endpoint,
    IPlatformUrl _platformUrl, String? _dataSource, AppPlatformId _platformId) : IModelBuilder
{
    // the template's $invoke and the switch below share the name
    internal const String SetPersonalCommand = "setPersonal";

    private readonly SqlBuilderProfile _sqlBuilder = new(_serviceProvider, _platformId);
    private readonly DatabaseMetadataProvider _metadataProvider = _serviceProvider.GetRequiredService<DatabaseMetadataProvider>();

    public String Path => _endpoint.Path;

    private async Task<TableMetadata?> ProfileAsync() => (await _metadataProvider.ProfileEndpointAsync(_dataSource))?.Storage;

    public async Task<IAppRuntimeResult> RenderAsync(IModelView view, Boolean isReload)
    {
        if (!_platformUrl.Action.Equals(ProfilePageEndpointMetadata.IndexAction, StringComparison.OrdinalIgnoreCase))
            throw Constants.Actions.Unknown(_platformUrl.Action);
        var profile = await ProfileAsync();
        var dm = await _sqlBuilder.LoadAsync(_dataSource, profile);
        if (isReload)
            return new AppRuntimeResult(dm, null);
        // the page is the renderer's (XamlPageRenderer.ProfilePageAsync)
        var request = new PageRequest(_endpoint, _platformUrl, _dataSource, view, dm, ProfileTemplateBuilder.Template);
        return new AppRuntimeResult(dm, await IPageRenderer.Of(_serviceProvider, Path).RenderPageAsync(request));
    }

    public async Task<ExpandoObject> SaveModelAsync(ExpandoObject data, ExpandoObject savePrms)
    {
        var profile = await ProfileAsync()
            ?? throw new InvalidOperationException($"{Path}: nothing to save - the application has no profile/metadata.json");
        return await _sqlBuilder.SaveAsync(_dataSource, profile, data);
    }

    public async Task<IInvokeResult> InvokeAsync(IModelCommand cmd, String command, ExpandoObject? prms)
    {
        if (command != SetPersonalCommand)
            throw IModelBuilder.NotHere(command, Path);
        var args = prms ?? [];
        await _serviceProvider.GetRequiredService<IAppUserProfile>().SetPersonalAsync(
            args.GetNotNull<String>("PersonName"),
            Offered(args.Get<String>("Locale"), ProfileTemplateBuilder.Locales(_serviceProvider), "locale"),
            Offered(args.Get<String>("Theme"), ProfileTemplateBuilder.Themes(_serviceProvider), "theme"));
        return new InvokeResult(Encoding.UTF8.GetBytes("{}"), MimeTypes.Application.Json);
    }

    /* What the page offered and nothing else: the value lands in the sign-in's claims. Nothing offered,
     * nothing written - the model still carries the stored value, and it is not a choice the user made.
     */
    private String? Offered(String? value, IReadOnlyCollection<String> offered, String what) =>
        offered.Count == 0 || value == null ? null
            : offered.Contains(value) ? value
            : throw new InvalidOperationException($"{Path}: '{value}' is not a {what} the page offers ({String.Join(", ", offered)})");
}
