// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Dynamic;

namespace A2v10.Metadata.Tests;

/* The metadata save builds its TVP rows in its own shape and converts every value by the core's
 * conversion - the one the classic save writes through (A2v10.Data DataModelWriter). Its own conversion
 * wrote '' where the classic one wrote NULL, threw on '' in a date or a number, and parsed '12.5' in the
 * process's culture.
 */
public class TvpConversionTests
{
    static async Task<(TableMetadata Table, AppPlatformId PlatformId)> DocumentAsync()
    {
        var provider = TestHost.GetService<DatabaseMetadataProvider>();
        return ((await provider.GetNormalEndpointAsync(null, "document", "waybillin")).Storage, await provider.GetPlatformIdAsync(null));
    }

    static Object Written(TableMetadata table, AppPlatformId platformId, String column, Object? value, Boolean allowEmptyStrings = false)
    {
        var row = new ExpandoObject();
        ((IDictionary<String, Object?>)row)[column] = value;
        return new DataTableBuilder(table, platformId, allowEmptyStrings).BuildDataTable(row).Rows[0][column];
    }

    [Fact]
    public async Task A_value_is_written_as_the_classic_save_writes_it()
    {
        var (doc, pid) = await DocumentAsync();
        dynamic agent = new ExpandoObject();
        agent.Id = 7L;

        Assert.Equal(DBNull.Value, Written(doc, pid, "Memo", ""));
        Assert.Equal(DBNull.Value, Written(doc, pid, "Date", ""));
        Assert.Equal(DBNull.Value, Written(doc, pid, "Sum", ""));
        Assert.Equal(12.5M, Written(doc, pid, "Sum", "12.5"));
        Assert.Equal(new DateTime(2026, 10, 11), Written(doc, pid, "Date", "2026-10-11T00:00:00"));
        Assert.Equal(7L, Convert.ToInt64(Written(doc, pid, "Agent", (ExpandoObject)agent)));
        Assert.Equal(new Byte[] { 0x0A, 0xFF }, (Byte[])Written(doc, pid, "rv", "0AFF"));
        Assert.Equal(DBNull.Value, Written(doc, pid, "Memo", null));
    }

    [Fact]
    public async Task The_host_may_keep_empty_strings()
    {
        var (doc, pid) = await DocumentAsync();

        Assert.Equal("", Written(doc, pid, "Memo", "", allowEmptyStrings: true));
    }
}
