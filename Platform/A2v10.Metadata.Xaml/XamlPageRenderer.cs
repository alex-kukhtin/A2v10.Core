// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Infrastructure;
using A2v10.Xaml;
using A2v10.Xaml.DynamicRendrer;

namespace A2v10.Metadata;

/* The second dispatch on the endpoint's type, beside ModelBuilderFactory's: that one knows what an
 * endpoint serves, this one how its page is drawn. A type with no arm here is refused by name.
 */
internal class XamlPageRenderer(IServiceProvider _serviceProvider, DatabaseMetadataProvider _metadataProvider) : IPageRenderer
{
    public async Task<String> RenderPageAsync(PageRequest request)
    {
        UIElement page = request.Endpoint switch
        {
            NormalEndpointMetadata endpoint => EndpointPage(endpoint, request),
            ReportEndpointMetadata report => ReportPage.Create(report, request.PlatformUrl.Query ?? []).CreatePage(),
            TagEndpointMetadata => new XamlTagsBuilder().RenderSettingsDialog(),
            AttachmentEndpointMetadata => new XamlAttachmentsBuilder().RenderIndexDialog(),
            OperationEndpointMetadata => new XamlOperationsBuilder().RenderBrowseDialog(),
            UserAdminEndpointMetadata users => await UsersPageAsync(users, request),
            var other => throw new NotSupportedException($"{other.Path}: no page for {other.GetType().Name}")
        };

        if (page is ISupportPlatformUrl supportPlatformUrl)
            supportPlatformUrl.SetPlatformUrl(request.PlatformUrl);

        var rri = new DynamicRenderPageInfo()
        {
            RootId = $"el{Guid.NewGuid()}",
            Page = page,
            ModelView = request.View,
            PlatformUrl = request.PlatformUrl,
            Model = request.Model,
            Template = request.Template
        };
        return await new DynamicRenderer(_serviceProvider).RenderPage(rri);
    }

    // written as the runtime would draw it, so the first diff after an edit reads
    public String ViewText(NormalEndpointMetadata endpoint, String action) =>
        XamlTextBulder.GetXaml(new XamlBuilder(endpoint, endpoint.PlatformUrl(action)).CreateXamlContainer(action));

    /* The BAKED FORMS, written as text: a shape nothing renders (a set, a numbering) has none. A
     * report's page with no picks - the first open - and not as text: nothing ever writes a report
     * page as text, so serializing it would exercise a road that does not exist.
     */
    public void BuildPages(EndpointMetadata endpoint)
    {
        switch (endpoint)
        {
            case NormalEndpointMetadata normal:
                foreach (var action in normal.Declaration.BakedForms.Keys)
                    ViewText(normal, action);
                break;
            case ReportEndpointMetadata report:
                ReportPage.Create(report, new ExpandoObject()).CreatePage();
                break;
            default:
                throw new NotSupportedException($"{endpoint.Path}: no pages to build for {endpoint.GetType().Name}");
        }
    }

    // the action is checked by the builder before the load; the card has a tab per dimension of app.json 'boundary'
    async Task<UIElement> UsersPageAsync(UserAdminEndpointMetadata endpoint, PageRequest request)
    {
        var xaml = new XamlUsersBuilder(endpoint.Path);
        return request.PlatformUrl.Action.ToLowerInvariant() switch
        {
            UserAdminEndpointMetadata.IndexAction => xaml.IndexPage(),
            UserAdminEndpointMetadata.EditAction => xaml.EditDialog(await _metadataProvider.BoundaryAsync(request.DataSource)),
            UserAdminEndpointMetadata.CreateAction => xaml.CreateDialog(),
            _ => xaml.PasswordDialog()
        };
    }

    UIElement EndpointPage(NormalEndpointMetadata endpoint, PageRequest request)
    {
        // written in model.json, the file; absent, generated - see CLAUDE.md, "Two files in one folder"
        var rawView = request.View.GetRawView(false);
        if (!String.IsNullOrEmpty(rawView))
            return new CodeLoader(_serviceProvider).LoadPage(request.View, rawView);

        var action = request.PlatformUrl.Action.ToLowerInvariant();
        /* The print page is not cached: it depends on '?Form=', which the cache key does not
         * carry - keyed by action, the first blank opened answered for every other.
         */
        if (action == Constants.Print.Action)
            return new XamlBuilder(endpoint, request.PlatformUrl).CreateXamlContainer(action);
        return _metadataProvider.GetOrAddArtifact(request.DataSource, endpoint, action, () =>
        {
            /* Initialized before it is shared: every request renders this one object, and a form
             * other threads could see half-initialized would be initialized by all of them at once.
             */
            var form = new XamlBuilder(endpoint, request.PlatformUrl).CreateXamlContainer(action);
            form.InitComplete();
            return form;
        });
    }
}
