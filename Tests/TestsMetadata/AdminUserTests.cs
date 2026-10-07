// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* /admin/user - the users screen, a system endpoint of the 'admin' namespace. The right (AdminGate) and
 * the SQL need a database; what is pinned here is the address and the screens as text - TESTS.md, layer D.
 */
public class AdminUserTests
{
    static Task<EndpointMetadata> LoadAsync(String schema, String table) =>
        TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, schema, table);

    static String Xaml(Func<XamlUsersBuilder, Object> screen) =>
        XamlTextBulder.GetXaml(screen(new XamlUsersBuilder("/admin/user")));

    [Fact]
    public async Task The_screen_is_a_type_of_the_admin_namespace()
    {
        var endpoint = Assert.IsType<UserAdminEndpointMetadata>(await LoadAsync("admin", "user"));

        Assert.Equal("/admin/user", endpoint.Path);
        Assert.Equal(EndpointKind.Admin, endpoint.Kind);
    }

    // an unknown name is no endpoint with nothing behind it
    [Fact]
    public async Task An_unknown_admin_screen_fails_the_load()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => LoadAsync("admin", "nope"));

        Assert.Contains("'/admin/nope' is not an admin screen", ex.Message);
    }

    [Fact]
    public void The_list_opens_the_card_of_the_selected_user()
    {
        var xaml = Xaml(b => b.IndexPage());

        Assert.Contains("""ItemsSource="{Bind Users}""", xaml);
        Assert.Contains("Url='/admin/user/edit'", xaml);
    }

    // the roles are a checklist over the root's Roles; the user's own Roles is the text the list shows
    [Fact]
    public void The_card_ticks_the_roles()
    {
        var xaml = Xaml(b => b.EditDialog([]));

        Assert.Contains("""ItemsSource="{Bind Roles}""", xaml);
        Assert.Contains("""Value="{Bind Checked}""", xaml);
        Assert.Contains("""Value="{Bind User.PersonName}""", xaml);
    }

    static async Task<BoundaryDimension> StoreAsync() =>
        new("/catalog/store", (await TestHost.GetService<DatabaseMetadataProvider>().GetNormalEndpointAsync(null, "catalog", "store")).Storage);

    // the table is named as the tag entries are, its key is the pair - as in UserRoles
    [Fact]
    public async Task A_dimension_is_a_table_of_the_users_values()
    {
        var script = SqlDbGenerator.CreateBoundaryScript([await StoreAsync()]);

        Assert.Contains("create table cat.[Store$Boundary]", script);
        Assert.Contains("constraint PK_Store$Boundary primary key (UserId, [Store])", script);
        Assert.Contains("foreign key references cat.[Stores](Id)", script);
        Assert.Contains("foreign key references a2security.Users(Id)", script);
    }

    [Fact]
    public void No_boundary_no_script() =>
        Assert.Equal(String.Empty, SqlDbGenerator.CreateBoundaryScript([]));

    // a tab per dimension, keyed by its path; the tab reads the dimension's rows of the one recordset
    [Fact]
    public async Task The_card_has_a_tab_per_dimension()
    {
        var store = await StoreAsync();
        var xaml = Xaml(b => b.EditDialog([store]));
        var template = UsersTemplateBuilder.EditTemplate([store]);

        Assert.Contains("ActiveValue=\"/catalog/store\"", xaml);
        Assert.Contains("""ItemsSource="{Bind Root.$Boundary0}""", xaml);
        Assert.Contains("'TRoot.$Boundary0'() { return this.Boundary.filter(b => b.Boundary === '/catalog/store'); }", template);
    }
}
