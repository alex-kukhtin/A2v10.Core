// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* 'grants' of an endpoint - the rows of a2security.Grants. Read, not checked yet; the deploy merges
 * every endpoint's rows and the table equals the files. See EndpointGrants and
 * SqlDbGenerator.CreateGrantsScript.
 */
public class GrantsTests
{
    static Task<EndpointMetadata> EndpointAsync(String schema, String table) =>
        TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, schema, table);

    [Fact]
    public void Verbs_are_bits_and_post_is_the_apply_bit()
    {
        var grants = EndpointGrants.From("x", new() { ["Accountant"] = ["view", "post", "unpost"] })!;

        Assert.Equal([new EndpointGrant("Accountant", PermissionFlag.CanView | PermissionFlag.CanApply | PermissionFlag.CanUnapply)], grants);
    }

    [Fact]
    public void Not_written_is_not_empty()
    {
        Assert.Null(EndpointGrants.From("x", null));
        Assert.Empty(EndpointGrants.From("x", [])!);
    }

    [Fact]
    public void A_verb_spelled_otherwise_has_no_bit()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => EndpointGrants.From("catalog/agent/metadata.json", new() { ["Accountant"] = ["View"] }));
        Assert.Contains("catalog/agent/metadata.json: 'grants' gives 'Accountant' the verb 'View'", ex.Message);
        Assert.Contains("lower case", ex.Message);
    }

    [Fact]
    public async Task A_catalog_holds_its_grants()
    {
        var agent = await EndpointAsync("catalog", "agent");

        Assert.Equal([
            new EndpointGrant("Everyone", PermissionFlag.CanView),
            new EndpointGrant("Accountant", PermissionFlag.CanView | PermissionFlag.CanCreate | PermissionFlag.CanEdit | PermissionFlag.CanDelete)
        ], agent.Grants);
    }

    // a report is an address, and so is each document over the storage - the storage's file has none to give
    [Fact]
    public async Task The_deploy_collects_a_report_and_a_document_over_a_storage()
    {
        var (_, grants) = await TestHost.GetService<DatabaseMetadataProvider>().AllElementsMetadata(null);

        Assert.Contains(("/report/stockturnover", new EndpointGrant("Accountant", PermissionFlag.CanView)), grants);
        Assert.Contains(("/document/receipt", new EndpointGrant("Accountant", PermissionFlag.CanView | PermissionFlag.CanPost | PermissionFlag.CanUnpost)), grants);
        Assert.DoesNotContain(grants, g => g.Endpoint == "/document");
    }

    [Fact]
    public void The_merge_writes_the_rows_ordered_and_deletes_the_rest()
    {
        var sql = SqlDbGenerator.CreateGrantsScript([
            ("/document/receipt", new EndpointGrant("Storekeeper", PermissionFlag.CanView | PermissionFlag.CanPost)),
            ("/catalog/agent", new EndpointGrant("Everyone", PermissionFlag.CanView))
        ]);

        var agent = sql.IndexOf("(N'/catalog/agent', N'Everyone', 1, 0, 0, 0, 0, 0)");
        var receipt = sql.IndexOf("(N'/document/receipt', N'Storekeeper', 1, 0, 0, 0, 1, 0)");
        Assert.True(agent >= 0 && receipt > agent);
        Assert.Contains("merge a2security.Grants as t", sql);
        Assert.Contains("when not matched by source then delete;", sql);
    }

    // nothing declared is a state of the files as well: the table is emptied, not left behind
    [Fact]
    public void No_grants_still_merge()
    {
        var sql = SqlDbGenerator.CreateGrantsScript([]);

        Assert.DoesNotContain("insert into @Grants", sql);
        Assert.Contains("when not matched by source then delete;", sql);
    }
}
