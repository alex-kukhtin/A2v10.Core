// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* app.json 'roles' - the rows of a2security.Roles. Everything wrong fails the load, and the deploy
 * writes the rows and never the table. See AppRoles and SqlDbGenerator.CreateRolesScript.
 */
public class AppRolesTests
{
    // the 'roles' value of app.json, as AppJsonMetadata receives it
    static IReadOnlyList<AppRole>? Parse(String json) =>
        AppRoles.From(JsonConvert.DeserializeObject<Dictionary<String, Dictionary<String, JToken?>?>>(json));

    [Fact]
    public void Roles_are_read_ordered_by_key()
    {
        var roles = Parse("""
            { "Storekeeper": { "Name": "Комірник", "Memo": "Склад" }, "Accountant": { "Name": "Бухгалтер" } }
            """)!;

        Assert.Equal([new AppRole("Accountant", "Бухгалтер", null), new AppRole("Storekeeper", "Комірник", "Склад")], roles);
    }

    [Fact]
    public void No_roles_key_is_no_declaration() =>
        Assert.Null(AppRoles.From(null));

    [Theory]
    [InlineData("""{ "Admin": { "Name": "Адмін" } }""", "the platform's own")]
    [InlineData("""{ "Everyone": { "Name": "Усі" } }""", "the platform's own")]
    [InlineData("""{ "accountant": { "Name": "Бухгалтер" } }""", "PascalCase")]
    [InlineData("""{ "Accountant": { "Name": "Бухгалтер", "Color": "red" } }""", "names 'Color'")]
    [InlineData("""{ "Accountant": { "Memo": "без імені" } }""", "has no 'Name'")]
    [InlineData("""{ "Accountant": { "Name": 1 } }""", "a string is expected")]
    [InlineData("""{ "Accountant": null }""", "has no columns")]
    public void A_wrong_role_fails_the_load(String json, String message)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Parse(json));
        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public void A_key_longer_than_the_column_fails_the_load()
    {
        var key = "A" + new String('b', 64);
        var ex = Assert.Throws<InvalidOperationException>(() => Parse($$"""{ "{{key}}": { "Name": "x" } }"""));
        Assert.Contains("longer than 64", ex.Message);
    }

    [Fact]
    public void No_roles_writes_nothing() =>
        Assert.Equal(String.Empty, SqlDbGenerator.CreateRolesScript(null));

    [Fact]
    public void The_merge_writes_the_rows_and_voids_the_dropped_but_the_predefined()
    {
        var sql = SqlDbGenerator.CreateRolesScript([new AppRole("Accountant", "Бухгалтер", null), new AppRole("Cashier", "Кас'ир", "it's")]);

        Assert.Contains("(N'Accountant', N'Бухгалтер', null)", sql);
        Assert.Contains("(N'Cashier', N'Кас''ир', N'it''s')", sql);
        Assert.Contains("merge a2security.Roles as t", sql);
        Assert.Contains("when not matched by source and t.[Id] not in (N'Admin', N'Everyone') then update set", sql);
        Assert.DoesNotContain("create table", sql);
    }

    // an empty map is a declaration - every role but the predefined goes void
    [Fact]
    public void An_empty_map_voids_every_role()
    {
        var sql = SqlDbGenerator.CreateRolesScript([]);

        Assert.DoesNotContain("insert into @Roles", sql);
        Assert.Contains("merge a2security.Roles as t", sql);
    }
}
