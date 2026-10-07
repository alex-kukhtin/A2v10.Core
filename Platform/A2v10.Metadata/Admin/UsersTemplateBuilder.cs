// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

/* The scripts of /admin/user. The names of the template commands are shared with XamlUsersBuilder,
 * the names of the server's commands with UserAdminBuilder - each spelled once.
 */
internal static class UsersTemplateBuilder
{
    internal const String CreateUser = "createUser";
    internal const String DeleteUser = "deleteUser";
    internal const String Block = "block";
    internal const String Unblock = "unblock";
    internal const String Create = "create";
    internal const String SetPassword = "setPassword";

    /* The card's tabs: the active one on the root, a $$ property, so switching is no edit. A tab is
     * named by the recordset it shows; the boundary's tabs come beside the roles.
     */
    internal const String TabState = "Root.$$Tab";
    internal const String RolesTab = "Roles";

    // the card saved: the list merges the user by Id
    private const String UserChanged = "g.admin.user";

    // a backtick: the translation may carry an apostrophe ("Обов'язкове"), which ends a quoted string
    private const String Required = "`@[Error.Required]`";

    private static String Url(String action) => $"/{Constants.SchemaNames.Admin}/{UserAdminEndpointMetadata.ScreenName}/{action}";

    internal static String IndexTemplate => $$"""
        const template = {
            options: {
                persistSelect: ['Users']
            },
            events: {
                '{{UserChanged}}': userChanged
            },
            commands: {
                {{CreateUser}},
                {{DeleteUser}}: {
                    exec: {{DeleteUser}},
                    confirm: '@[Admin.Confirm.Delete]'
                },
                {{Block}}: {
                    exec: {{Block}},
                    canExec: user => !!user && !user.IsBlocked,
                    confirm: '@[Admin.Confirm.Block]'
                },
                {{Unblock}}: {
                    exec: {{Unblock}},
                    canExec: user => !!user && user.IsBlocked
                }
            }
        };

        module.exports = template;

        function userChanged(root) {
            const user = root.User;
            const found = this.Users.$find(u => u.Id === user.Id);
            if (found)
                found.$merge(user);
        }

        // the roles are given on the card, so the card opens on the user just created
        async function {{CreateUser}}() {
            const ctrl = this.$ctrl;
            const created = await ctrl.$showDialog('{{Url(UserAdminEndpointMetadata.CreateAction)}}');
            if (!created)
                return;
            const user = this.Users.$append(created);
            user.$select();
            await ctrl.$showDialog('{{Url(UserAdminEndpointMetadata.EditAction)}}', user);
        }

        async function {{DeleteUser}}(user) {
            await this.$ctrl.$invoke('{{UserAdminBuilder.DeleteCommand}}', { Id: user.Id });
            user.$remove();
        }

        function {{Block}}(user) {
            return setBlocked(this, user, true);
        }

        function {{Unblock}}(user) {
            return setBlocked(this, user, false);
        }

        async function setBlocked(root, user, blocked) {
            await root.$ctrl.$invoke('{{UserAdminBuilder.SetBlockedCommand}}', { Id: user.Id, Blocked: blocked });
            user.IsBlocked = blocked;
        }
        """;

    // the boundary does not hold for an admin: the card hides its tabs while the role is ticked
    internal const String IsAdminProperty = "$IsAdmin";

    /* The rows of one dimension, for its tab - by the index in app.json and not by Model: a Model may
     * repeat across aliased folders. The elements are the recordset's own, so a tick reaches the save.
     */
    internal static String DimensionProperty(Int32 index) => $"$Boundary{index}";

    internal static String EditTemplate(IReadOnlyList<BoundaryDimension> boundary)
    {
        IEnumerable<String> properties()
        {
            yield return $"'TRoot.$$Tab': {{ type: String, value: '{RolesTab}' }}";
            yield return $"'TRoot.{IsAdminProperty}'() {{ return this.Roles.some(r => r.Id === '{AppRoles.Admin}' && r.Checked); }}";
            foreach (var (d, i) in boundary.Select((d, i) => (d, i)))
                yield return $"'TRoot.{DimensionProperty(i)}'() {{ return this.{SqlBuilderUsers.BoundaryRows}.filter(b => b.Boundary === '{d.Path}'); }}";
        }

        return $$"""
        const template = {
            options: {
                globalSaveEvent: '{{UserChanged}}'
            },
            properties: {
                {{String.Join(",\n        ", properties())}}
            },
            validators: {
                'User.PersonName': {{Required}}
            }
        };

        module.exports = template;
        """;
    }

    internal static String CreateTemplate => $$"""
        const template = {
            validators: {
                'User.UserName': {{Required}},
                'User.PersonName': {{Required}},
                'User.Password': {{Required}},
                'User.Confirm': confirmValid
            },
            commands: {
                {{Create}}
            }
        };

        module.exports = template;

        function confirmValid(user, confirm) {
            return user.Password === confirm ? '' : '@[MatchError]';
        }

        async function {{Create}}() {
            const ctrl = this.$ctrl;
            const user = this.User;
            const result = await ctrl.$invoke('{{UserAdminBuilder.CreateCommand}}',
                { UserName: user.UserName, Password: user.Password, PersonName: user.PersonName });
            ctrl.$modalClose(result.User);
        }
        """;

    internal static String PasswordTemplate => $$"""
        const template = {
            validators: {
                'User.Password': {{Required}},
                'User.Confirm': confirmValid
            },
            commands: {
                {{SetPassword}}
            }
        };

        module.exports = template;

        function confirmValid(user, confirm) {
            return user.Password === confirm ? '' : '@[MatchError]';
        }

        async function {{SetPassword}}() {
            const ctrl = this.$ctrl;
            await ctrl.$invoke('{{UserAdminBuilder.SetPasswordCommand}}', { Id: this.User.Id, Password: this.User.Password });
            ctrl.$modalClose(true);
        }
        """;
}
