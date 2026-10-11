// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Metadata;
using A2v10.Services;

namespace A2v10.Metadata.Tests;

/* The save is one transaction: a row that fails leaves neither the header nor a spent number, and the
 * number is issued last, so the counter's lock is held to the commit only. See CLAUDE.md, "Autonums".
 */
public class SaveTransactionTests
{
    static async Task<String> SaveTextAsync(String url)
    {
        var provider = TestHost.GetService<DatabaseMetadataProvider>();
        var platformUrl = new PlatformUrl($"_page{url}");
        var (schema, table) = DatabaseMetadataProvider.ParsePath(platformUrl.LocalPath);
        var builder = new SqlBuilder(new BuilderDescriptor()
        {
            Endpoint = await provider.GetNormalEndpointAsync(null, schema, table),
            PlatformUrl = platformUrl,
            PlatformId = await provider.GetPlatformIdAsync(null),
            UseGrants = false,
        }, TestHost.Services);
        return builder.SaveSqlText();
    }

    [Fact]
    public async Task The_save_of_a_numbered_document_is_one_transaction_with_the_number_last()
    {
        var sql = await SaveTextAsync("/document/receipt/edit/new");

        var begin = sql.IndexOf("begin tran;");
        var rv = sql.IndexOf("with (updlock)");
        var firstMerge = sql.IndexOf("merge ");
        var lastMerge = sql.LastIndexOf("merge ");
        var autonum = sql.IndexOf("-- autonum");
        var commit = sql.IndexOf("commit tran;");
        var select = sql.IndexOf("read uncommitted", commit);

        Assert.True(begin >= 0 && rv >= 0 && autonum >= 0 && commit >= 0 && select >= 0, sql);
        Assert.True(begin < rv, "the row version is read inside the transaction");
        Assert.True(rv < firstMerge, "the row version is checked before anything is written");
        Assert.True(lastMerge < autonum, "the number is issued after the last merge");
        Assert.True(autonum < commit, "the number is issued inside the transaction");
        Assert.True(commit < select, "the select reads what was committed");
        Assert.Equal(1, sql.Split("begin tran;").Length - 1);
    }
}
