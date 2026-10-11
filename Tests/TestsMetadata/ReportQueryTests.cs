// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Dynamic;

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* A value from the browser is a parameter, never text of the batch. The report's query is the
 * browser's: what it picks is resolved against the declared items, and only the resolved names
 * reach the SQL.
 */
public class ReportQueryTests
{
    static async Task<TurnoverReportBuilder> TurnoverAsync(ExpandoObject query)
    {
        var report = Assert.IsType<ReportEndpointMetadata>(
            await TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, "report", "stockturnover"));
        var builder = Assert.IsType<TurnoverReportBuilder>(
            BaseReportBuilder.Create(TestHost.Services, report, new AppPlatformId(typeof(Int64)), useGrants: false));
        builder.SetGrouping(query);
        return builder;
    }

    static ExpandoObject Query(String group)
    {
        dynamic query = new ExpandoObject();
        query.Group = group;
        return query;
    }

    [Fact]
    public async Task The_group_the_browser_sent_never_enters_the_text()
    {
        const String forged = "Store!x';exec Forged;--";
        var builder = await TurnoverAsync(Query(forged));

        var sql = await builder.CreateSqlTextAsync();

        Assert.DoesNotContain("Forged", sql);
        Assert.DoesNotContain("x'", sql);
        Assert.Contains("[Group] = N'Store'", sql);
    }

    [Fact]
    public async Task The_group_is_echoed_as_resolved_in_the_order_picked()
    {
        var builder = await TurnoverAsync(Query("Item!Agent"));

        var sql = await builder.CreateSqlTextAsync();

        Assert.Contains("[Group] = N'Item!Agent'", sql);
    }
}
