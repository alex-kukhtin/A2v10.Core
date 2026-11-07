// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Data;

using Microsoft.Data.SqlClient;
using Newtonsoft.Json;

using A2v10.Metadata;
using A2v10.Services;

namespace A2v10.Metadata.Tests;

/* A 'user' column is a reference to a login: a fixed target (/user) over a2security.Users, read through
 * a2meta.[Users$Ref], keyed bigint whatever the application's platformid is. TestApp catalog/manager -
 * a person of the application with a link to the login. No database.
 */
public class UserRefTests
{
    static async Task<NormalEndpointMetadata> ManagerAsync() =>
        (NormalEndpointMetadata)await TestHost.GetService<DatabaseMetadataProvider>()
            .GetEndpointAsync(null, "catalog", "manager");

    [Fact]
    public async Task A_user_column_resolves_to_the_logins()
    {
        var column = (await ManagerAsync()).Storage.Columns.First(c => c.Name == "User");

        var target = Assert.IsType<UserEndpointMetadata>(column.RefTable);
        Assert.Equal("/user", target.Path);
        Assert.Equal("a2security.[Users]", target.Storage.SqlTableName);
        Assert.Equal("bigint", target.Storage.KeyColumn.ToSqlDbTypeInfo().SqlName);
    }

    [Fact]
    public void A_user_column_writes_no_target()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckUserColumn(
            "/catalog/manager", new TableColumn("User", ColumnType.User) { Target = "/catalog/agent" }));
        Assert.Contains("Write no 'target'", ex.Message);
    }

    [Fact]
    public async Task The_foreign_key_and_the_seed_name_the_logins_table()
    {
        var storage = (await ManagerAsync()).Storage;

        var fk = SqlDbGenerator.CreateForeignKeysScript([storage]);
        Assert.Contains("foreign key ([User]) references a2security.[Users]([Id])", fk);

        var seed = await SqlDbGenerator.GenerateMetadataSeedAsync([storage]);
        Assert.NotNull(seed);
        var line = Assert.Single(seed.Split('\n'), l => l.Contains("N'Managers', N'User', "));
        Assert.Contains("N'bigint'", line);
        Assert.Contains("N'a2security', N'Users'", line);
    }

    [Fact]
    public async Task The_card_shows_the_login_by_its_name()
    {
        var provider = TestHost.GetService<DatabaseMetadataProvider>();
        var descriptor = new BuilderDescriptor()
        {
            Endpoint = await ManagerAsync(),
            PlatformUrl = new PlatformUrl("_page/catalog/manager/edit/1"),
            PlatformId = await provider.GetPlatformIdAsync(null),
            UseGrants = false,
        };
        var sql = new SqlBuilder(descriptor, TestHost.Services).BuildLoadPlainSqlText();

        Assert.Contains("[User!TRUser!RefId]", sql);
        Assert.Contains("from a2meta.[Users$Ref] a", sql);
        Assert.Contains("[User] bigint", sql);
    }

    /* A filter value arrives as text and is typed as the key it points at. On a guid base a login is
     * still bigint: typed as platformid it parsed to null - 'not picked' - and the filter went silent.
     */
    [Theory]
    [InlineData(ColumnType.User, "42", SqlDbType.BigInt, 42L)]
    [InlineData(ColumnType.Ref, "7", SqlDbType.UniqueIdentifier, null)]
    [InlineData(ColumnType.Account, "361", SqlDbType.NVarChar, "361")]
    [InlineData(ColumnType.Account, "", SqlDbType.NVarChar, null)]
    [InlineData(ColumnType.User, "", SqlDbType.BigInt, null)]
    public void A_reference_parameter_is_typed_as_its_key(ColumnType type, String value, SqlDbType dbType, Object? expected)
    {
        var prms = new SqlCommand().Parameters;

        prms.AddReference("@$X", type, new AppPlatformId(typeof(Guid)), value);

        var p = Assert.Single(prms.Cast<SqlParameter>());
        Assert.Equal(dbType, p.SqlDbType);
        Assert.Equal(expected ?? DBNull.Value, p.Value);
    }

    [Fact]
    public void A_column_that_is_no_reference_key_has_no_reference_parameter() =>
        Assert.Throws<InvalidOperationException>(() =>
            new SqlCommand().Parameters.AddReference("@$X", ColumnType.Date, new AppPlatformId(typeof(Int64)), "1"));

    // a new manager starts on the login that creates it: the defaults and the map read one parameter
    [Fact]
    public async Task A_new_record_starts_on_the_current_login()
    {
        var provider = TestHost.GetService<DatabaseMetadataProvider>();
        var descriptor = new BuilderDescriptor()
        {
            Endpoint = await ManagerAsync(),
            PlatformUrl = new PlatformUrl("_page/catalog/manager/edit/new"),
            PlatformId = await provider.GetPlatformIdAsync(null),
            UseGrants = false,
        };
        var sql = new SqlBuilder(descriptor, TestHost.Services).BuildLoadPlainSqlText();

        Assert.Contains("[Manager.User!TRUser!RefId] = @UserId", sql);
        Assert.Contains("insert into @map([User]) values (@UserId);", sql);
    }

    // a login is no person: into another column it would be a key of the wrong table
    [Theory]
    [InlineData("""{ "initialValues": { "Agent": { "source": "context", "value": "currentUser" } } }""", "a 'user' column only")]
    [InlineData("""{ "initialValues": { "Memo": { "source": "context", "value": "now" } } }""", "Known: today, currentUser")]
    public async Task The_current_login_starts_a_user_column_only(String json, String message)
    {
        var storage = (await TestHost.GetService<DatabaseMetadataProvider>().GetNormalEndpointAsync(null, "document", "payment")).Storage;
        var declaration = JsonConvert.DeserializeObject<DeclarationMetadata>(json, JsonSettings.CamelCaseSerializerSettings)!;

        var ex = Assert.Throws<InvalidOperationException>(() => declaration.Bake(storage, new AppPlatformId(typeof(Int64))));
        Assert.Contains(message, ex.Message);
    }

    // picked like any reference: the selector browses and fetches at the target's address
    [Fact]
    public async Task The_card_picks_a_login_at_the_target()
    {
        var view = (await TestHost.GetService<EndpointMaterializer>()
            .MaterializeAsync("/catalog/manager", "edit", MaterializeWhat.View)).Files[0].Text;

        Assert.Contains("<SelectorSimple", view);
        Assert.Contains("Url=\"/user\"", view);
    }

    // the dialog the selector opens: the login beside the name, the same collection the resolve fills
    [Fact]
    public void The_browse_shows_the_login_beside_the_name()
    {
        var xaml = XamlTextBulder.GetXaml(new XamlOperationsBuilder().RenderUsersDialog());

        Assert.Contains("""ItemsSource="{Bind Users}""", xaml);
        Assert.Contains("@[User.Browse]", xaml);
        Assert.Contains("{Bind UserName}", xaml);
    }

    // the key of the target, read by the map's type: bigint is a number whatever platformid is
    [Fact]
    public async Task The_reference_is_typed_by_the_login_key()
    {
        var files = await TestHost.GetService<EndpointMaterializer>()
            .MaterializeAsync("/catalog/manager", "edit", MaterializeWhat.Template);
        var map = files.Files.Single(f => f.Path.EndsWith(".d.ts")).Text;

        var user = map[map.IndexOf("export interface TRUser extends IElement")..];
        Assert.Contains("readonly Id: number;", user[..user.IndexOf('}')]);
    }
}
