// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Text.RegularExpressions;

using Newtonsoft.Json;

using A2v10.Metadata;
using A2v10.Services;

namespace A2v10.Metadata.Tests;

/* An initial value of a reference goes into '@map', or the card opens on an empty control. The slots
 * of '@map' are named after the widest source: TestApp document/payment has one account in the header
 * and two in its rows, so the slots are the rows' and the header's own name is none of them.
 */
public partial class InitialRefTests
{
    static async Task<SqlBuilder> BuilderOf(String endpointPath)
    {
        var provider = TestHost.GetService<DatabaseMetadataProvider>();
        var (schema, table) = DatabaseMetadataProvider.ParsePath(endpointPath);
        var descriptor = new BuilderDescriptor()
        {
            Endpoint = await provider.GetNormalEndpointAsync(null, schema, table),
            PlatformUrl = new PlatformUrl($"_page{endpointPath}/edit/new"),
            PlatformId = await provider.GetPlatformIdAsync(null),
            UseGrants = false,
        };
        return new SqlBuilder(descriptor, TestHost.Services);
    }

    [GeneratedRegex(@"declare @map table\(([^;]*)\);")]
    private static partial Regex MapDeclare();

    [GeneratedRegex(@"insert into @map\(\[(\w+)\]\) values")]
    private static partial Regex MapInitialInsert();

    [Fact]
    public async Task An_initial_reference_goes_into_a_declared_slot()
    {
        var sql = (await BuilderOf("/document/payment")).BuildLoadPlainSqlText();

        var slots = MapDeclare().Match(sql).Groups[1].Value;
        var inserts = MapInitialInsert().Matches(sql).Select(m => m.Groups[1].Value).ToList();

        Assert.NotEmpty(inserts);
        Assert.All(inserts, slot => Assert.Contains($"[{slot}]", slots));
        Assert.Contains("N'361'", sql);
    }

    /* 'sql' takes the road of a url value: a variable typed by the field, read by the defaults and,
     * for a reference, by the map. It had no branch at all and failed the first 'edit/new'.
     */
    [Fact]
    public async Task An_sql_initial_is_a_variable_the_defaults_and_the_map_read()
    {
        var sql = (await BuilderOf("/document/payment")).BuildLoadPlainSqlText();

        Assert.Contains("declare @InitDueDate date = (dateadd(day, 30, getdate()));", sql);
        Assert.Contains("[Payment.DueDate] = @InitDueDate", sql);
        Assert.Contains("= (select min(Id) from cat.Agents);", sql);
        Assert.Contains("!RefId] = @InitAgent", sql);
        Assert.Contains("values (@InitAgent);", sql);
    }

    // an account's key is its code: the profile's variable is typed by the field, not as a platformid
    [Fact]
    public async Task A_profile_initial_is_typed_by_its_field()
    {
        var sql = (await BuilderOf("/document/payment")).BuildLoadPlainSqlText();

        Assert.Contains("declare @InitCashAccount nvarchar", sql);
        Assert.DoesNotContain("declare @InitCashAccount platformid", sql);
        Assert.Contains("@MainCashAccount = @InitCashAccount output", sql);
    }

    // a select hands the client the type of its expression: a bare string would reach the defaults as text
    [Theory]
    [InlineData(ColumnType.Date, "cast(N'20261031' as date)")]
    [InlineData(ColumnType.DateTime, "cast(N'20261031' as datetime)")]
    public void A_literal_date_is_cast_to_the_column(ColumnType type, String expected) =>
        Assert.Equal(expected, new TableColumn("Date", type).SqlLiteral("2026-10-31"));

    /* A literal the column cannot hold is refused at the bake - 'a2 meta validate' - and not at the first
     * 'edit/new': it broke the load of every new record, so neither a screen nor document_create could start one.
     */
    [Theory]
    [InlineData("DueDate", "31.10.2026", "is not a date 'yyyy-MM-dd'")]
    [InlineData("Agent", "7", "A literal value is not supported")]
    public async Task A_literal_initial_the_column_cannot_hold_is_refused_at_the_bake(String field, String value, String message)
    {
        var storage = (await TestHost.GetService<DatabaseMetadataProvider>().GetNormalEndpointAsync(null, "document", "payment")).Storage;
        var declaration = JsonConvert.DeserializeObject<DeclarationMetadata>(
            $$"""{ "initialValues": { "{{field}}": { "source": "literal", "value": "{{value}}" } } }""",
            JsonSettings.CamelCaseSerializerSettings)!;

        var ex = Assert.Throws<InvalidOperationException>(() => declaration.Bake(storage, new AppPlatformId(typeof(Int64))));
        Assert.Contains($"initialValues: '{field}'", ex.Message);
        Assert.Contains(message, ex.Message);
    }
}
