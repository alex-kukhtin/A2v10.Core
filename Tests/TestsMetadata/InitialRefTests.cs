// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Text.RegularExpressions;

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
}
