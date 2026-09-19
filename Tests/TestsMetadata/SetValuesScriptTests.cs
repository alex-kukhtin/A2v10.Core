// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* The statement that puts the declared values into the database. It is the artifact the customer
 * gets, not a side effect (SqlDbGenerator's header says so), and nothing read it back until now -
 * which is how a merge spelled out by hand could go on writing five columns of seven.
 *
 * No database: the generator turns metadata into text. See TESTS.md, layer D.
 */
public class SetValuesScriptTests
{
    static async Task<TableMetadata> SetAsync(String schema, String table) =>
        ((NormalEndpointMetadata)await TestHost.GetService<DatabaseMetadataProvider>()
            .GetEndpointAsync(null, schema, table)).Storage;

    static String ScriptOf(params TableMetadata[] sets) =>
        SqlDbGenerator.CreateSetValuesScript(sets);

    /* Every column of the set's own baseline, on both arms of the merge. The colour and the role
     * are nullable, so a statement that forgot them would deploy without a word and leave every
     * badge grey and every cycle roleless.
     */
    [Fact]
    public async Task The_merge_carries_every_column_the_set_declares()
    {
        var states = await SetAsync("state", "order");

        var sql = ScriptOf(states);

        foreach (var column in states.AllColumns())
        {
            Assert.Contains($"[{column.Name}] {column.SqlDataType()}", sql);   // the table variable
            if (!column.IsKey)
                Assert.Contains($"t.[{column.Name}] = s.[{column.Name}]", sql); // when matched
        }
        Assert.Contains("N'delivered'", sql);
        Assert.Contains("N'green'", sql);
        Assert.Contains("N'Success'", sql);
    }

    /* The 'All' row is the platform's, never declared, and it is not a state: a role on it would be
     * a lie whichever of the four it was. It comes first, by an order below every declared one.
     */
    [Fact]
    public async Task The_All_row_is_added_with_no_role_and_no_colour()
    {
        var states = await SetAsync("state", "order");

        var sql = ScriptOf(states);
        var all = sql.Split('\n').Single(l => l.Contains("N''"));

        Assert.Contains("@[OrderState.All]", all);
        Assert.Contains("-1", all);
        Assert.Equal(3, all.Split("null").Length - 1);   // memo, colour, role
    }

    // an enum has five columns and a set of states seven, from one walk over the shape
    [Fact]
    public async Task An_enum_gets_the_same_statement_without_the_two()
    {
        var rates = await SetAsync("enum", "vatrate");

        var sql = ScriptOf(rates);

        Assert.DoesNotContain("[Color]", sql);
        Assert.DoesNotContain("[Role]", sql);
        Assert.Contains("[Order]", sql);
    }
}
