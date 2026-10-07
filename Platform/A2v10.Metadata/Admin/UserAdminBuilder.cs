// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Services;
using A2v10.Xaml;
using A2v10.Xaml.DynamicRendrer;

namespace A2v10.Metadata;

/* /admin/user. The right is checked before this is built (AdminGate). The acting user is the current
 * one - the metadata path hands the client's data through untouched, so nothing here reads a UserId
 * from it.
 */
internal class UserAdminBuilder(IServiceProvider _serviceProvider, UserAdminEndpointMetadata _endpoint,
    IPlatformUrl _platformUrl, String? _dataSource, AppPlatformId _platformId) : IModelBuilder
{
    // the template's $invoke and the switch below share the names
    internal const String CreateCommand = "create";
    internal const String SetPasswordCommand = "setPassword";
    internal const String SetBlockedCommand = "setBlocked";
    internal const String DeleteCommand = "delete";

    private readonly DynamicRenderer _dynamicRenderer = new(_serviceProvider);
    private readonly SqlBuilderUsers _sqlBuilder = new(_serviceProvider, _platformId);
    private readonly XamlUsersBuilder _xamlBuilder = new(_endpoint.Path);
    private readonly ICurrentUser _currentUser = _serviceProvider.GetRequiredService<ICurrentUser>();
    private readonly DatabaseMetadataProvider _metadataProvider = _serviceProvider.GetRequiredService<DatabaseMetadataProvider>();

    // app.json 'boundary': a tab of the card per dimension
    private Task<IReadOnlyList<BoundaryDimension>> BoundaryAsync() => _metadataProvider.BoundaryAsync(_dataSource);

    public String Path => _endpoint.Path;

    private String Action => _platformUrl.Action.ToLowerInvariant();

    private Int64 Id() => Int64.TryParse(_platformUrl.Id, out var id) ? id
        : throw new InvalidOperationException($"'{Path}/{Action}': the user id is missing");

    public async Task<IAppRuntimeResult> RenderAsync(IModelView view, Boolean isReload)
    {
        IReadOnlyList<BoundaryDimension> boundary = Action == UserAdminEndpointMetadata.EditAction ? await BoundaryAsync() : [];
        var dm = Action switch
        {
            UserAdminEndpointMetadata.IndexAction => await _sqlBuilder.LoadIndexAsync(_dataSource),
            UserAdminEndpointMetadata.EditAction => await _sqlBuilder.LoadCardAsync(_dataSource, Id(), boundary),
            UserAdminEndpointMetadata.CreateAction => await _sqlBuilder.LoadNewAsync(_dataSource),
            UserAdminEndpointMetadata.PasswordAction => await _sqlBuilder.LoadPasswordAsync(_dataSource, Id()),
            _ => throw new InvalidOperationException($"Users. Unsupported action '{Action}'")
        };
        if (isReload)
            return new AppRuntimeResult(dm, null);

        var (page, template) = Action switch
        {
            UserAdminEndpointMetadata.IndexAction => ((UIElement)_xamlBuilder.IndexPage(), UsersTemplateBuilder.IndexTemplate),
            UserAdminEndpointMetadata.EditAction => (_xamlBuilder.EditDialog(boundary), UsersTemplateBuilder.EditTemplate(boundary)),
            UserAdminEndpointMetadata.CreateAction => (_xamlBuilder.CreateDialog(), UsersTemplateBuilder.CreateTemplate),
            _ => (_xamlBuilder.PasswordDialog(), UsersTemplateBuilder.PasswordTemplate)
        };
        if (page is ISupportPlatformUrl supportPlatformUrl)
            supportPlatformUrl.SetPlatformUrl(_platformUrl);

        var rri = new DynamicRenderPageInfo()
        {
            RootId = $"el{Guid.NewGuid()}",
            Page = page,
            ModelView = view,
            PlatformUrl = _platformUrl,
            Template = template,
            Model = dm
        };
        return new AppRuntimeResult(dm, await _dynamicRenderer.RenderPage(rri));
    }

    public async Task<ExpandoObject> SaveModelAsync(ExpandoObject data, ExpandoObject savePrms)
    {
        if (Action != UserAdminEndpointMetadata.EditAction)
            throw new InvalidOperationException($"Users. Nothing to save on '{Action}'");
        var id = Id();
        var user = data.Get<ExpandoObject>("User")
            ?? throw new InvalidOperationException("Users. User is null");
        var roles = (data.Get<List<Object>>("Roles") ?? []).OfType<ExpandoObject>().ToList();
        // the last admin would leave no one to give the role back - only SQL could
        if (id == _currentUser.Identity.Id
            && roles.Any(r => r.Get<String>("Id") == AppRoles.Admin && !r.Get<Boolean>("Checked")))
            throw new InvalidOperationException("UI:@[Admin.Error.SelfAdmin]");
        var boundaryRows = (data.Get<List<Object>>(SqlBuilderUsers.BoundaryRows) ?? []).OfType<ExpandoObject>();
        return await _sqlBuilder.SaveCardAsync(_dataSource, id, user, roles, boundaryRows, await BoundaryAsync());
    }

    public async Task<IInvokeResult> InvokeAsync(IModelCommand cmd, String command, ExpandoObject? prms)
    {
        var args = prms ?? [];
        var admin = _serviceProvider.GetRequiredService<IAppUserAdmin>();
        switch (command)
        {
            case CreateCommand:
                var id = await admin.CreateAsync(args.GetNotNull<String>("UserName"), args.GetNotNull<String>("Password"));
                var created = await _sqlBuilder.SetPersonNameAsync(_dataSource, id, args.Get<String>("PersonName"));
                return created.ToInvokeResult();
            case SetPasswordCommand:
                await admin.SetPasswordAsync(TargetId(args), args.GetNotNull<String>("Password"));
                break;
            case SetBlockedCommand:
                await admin.SetBlockedAsync(NotSelf(TargetId(args)), args.Get<Boolean>("Blocked"));
                break;
            case DeleteCommand:
                await admin.DeleteAsync(NotSelf(TargetId(args)));
                break;
            default:
                throw new NotSupportedException($"'{command}' is not supported for '{Path}'");
        }
        return new InvokeResult(Encoding.UTF8.GetBytes("{}"), MimeTypes.Application.Json);
    }

    private static Int64 TargetId(ExpandoObject args)
    {
        var id = args.Get<Int64>("Id");
        return id != 0 ? id : throw new InvalidOperationException("Users. Id is null");
    }

    // blocking or deleting yourself: the last admin would leave no one to undo it
    private Int64 NotSelf(Int64 id) =>
        id != _currentUser.Identity.Id ? id : throw new InvalidOperationException("UI:@[Admin.Error.Self]");
}
