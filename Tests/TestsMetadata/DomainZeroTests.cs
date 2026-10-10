// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* NOT NULL comes from the domain and from nothing else: a magnitude and a flag have a zero, the rest
 * carry 'nothing' as NULL. See TableColumn.HasZero.
 */
public class DomainZeroTests
{
    public static TheoryData<ColumnType> AllTypes => [.. Enum.GetValues<ColumnType>()];

    static async Task<NormalEndpointMetadata> LoadNormalAsync(String schema, String table) =>
        Assert.IsType<NormalEndpointMetadata>(
            await TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, schema, table));

    [Theory]
    [InlineData(ColumnType.Amount)]
    [InlineData(ColumnType.Qty)]
    [InlineData(ColumnType.Price)]
    [InlineData(ColumnType.Percent)]
    [InlineData(ColumnType.Factor)]
    [InlineData(ColumnType.Money)]
    [InlineData(ColumnType.Decimal)]
    [InlineData(ColumnType.Float)]
    [InlineData(ColumnType.Boolean)]
    public void A_domain_with_a_zero_is_not_null_default_0(ColumnType type)
    {
        var column = new TableColumn("X", type);

        Assert.False(column.DeployNullable());
        Assert.Equal("0", column.DeployDefault());
        Assert.Equal("0", column.EmptyLiteral());
    }

    // a key, a code, a reference, a date, a string: 'nothing' is an absence
    [Theory]
    [InlineData(ColumnType.Integer)]
    [InlineData(ColumnType.BigInt)]
    [InlineData(ColumnType.Ref)]
    [InlineData(ColumnType.Date)]
    [InlineData(ColumnType.String)]
    public void A_domain_without_one_is_nullable(ColumnType type)
    {
        var column = new TableColumn("X", type);

        Assert.True(column.DeployNullable());
        Assert.Null(column.DeployDefault());
        Assert.Equal("null", column.EmptyLiteral());
    }

    // the literal is a bare 0, so every type that takes it must be one a 0 is valid in
    [Theory]
    [MemberData(nameof(AllTypes))]
    public void A_zero_is_written_only_into_a_number_or_a_bit(ColumnType type)
    {
        if (!new TableColumn("X", type).HasZero)
            return;
        Assert.Contains(type.ToSqlDbTypeInfo().SqlName, new[] { "decimal", "money", "float", "bit" });
    }

    /* The debit leg of waybillout names Agent, the credit leg Item and Qty. Each leg writes the other's
     * columns empty: the analytic null, the measure 0 - Qty is NOT NULL and null would fail the post.
     */
    [Fact]
    public async Task A_ledger_leg_writes_the_zero_of_a_measure_it_does_not_name()
    {
        var waybill = await LoadNormalAsync("document", "waybillout");

        var post = new PostStatements(waybill).Post;

        Assert.Contains("[Dt$Qty] = 0", post);
        Assert.Contains("[Dt$Item] = null", post);
        Assert.Contains("[Ct$Agent] = null", post);
    }

    /* Quantitative (Boolean) is named by 281 only. The insert arm writes the column for every row, so
     * 28 gets 0 there - null would fail the deploy; the '$'-bit still says 'not named'.
     */
    [Fact]
    public async Task A_seed_row_that_does_not_name_a_column_writes_its_zero()
    {
        var chart = (await LoadNormalAsync("accplan", "national")).Storage;

        var sql = SqlDbGenerator.CreateRowsScript([chart]);

        var row28 = sql.Split('\n').Single(l => l.TrimStart().StartsWith("(N'28',"));
        var row281 = sql.Split('\n').Single(l => l.TrimStart().StartsWith("(N'281',"));
        Assert.Contains(", 0, 0)", row28);
        Assert.Contains(", 1, 1)", row281);
    }
}
