// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Dynamic;

using Newtonsoft.Json;

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* Belonging: a reference to an owned record picks among those whose owner is the matching field
 * beside it. TestApp: catalog/contract belongs to an agent (Owner) and a company (Company); the
 * document holds a contract in the header and in its rows. See a2v10-md-skill, metadata.md -> Owner.
 */
public class OwnerTests
{
    static async Task<NormalEndpointMetadata> LoadNormalAsync(String schema, String table) =>
        Assert.IsType<NormalEndpointMetadata>(
            await TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, schema, table));

    static async Task<String> Materialize(String endpoint, String action, MaterializeWhat what)
    {
        var mat = new EndpointMaterializer(TestHost.GetService<DatabaseMetadataProvider>());
        return (await mat.MaterializeAsync(endpoint, action, what)).Files[0].Text;
    }

    static TableColumn Column(TableMetadata table, String name) => table.AllColumns().Single(c => c.Name == name);

    // Owner by target, Company by type - both from the record the reference sits in
    [Fact]
    public async Task A_contract_in_the_header_is_filtered_by_its_agent_and_company()
    {
        var documents = (await LoadNormalAsync("document", String.Empty)).Storage;

        var links = Column(documents, "Contract").OwnerLinks(documents, null);

        Assert.Equal(["Agent:Agent", "Company:Company"], links.Select(l => $"{l.Owner.Name}:{l.Field.Name}").Order());
        Assert.All(links, l => Assert.False(l.Header));
    }

    // a row holds neither, so both come from the document
    [Fact]
    public async Task A_contract_in_a_row_takes_the_owners_of_the_header()
    {
        var documents = (await LoadNormalAsync("document", String.Empty)).Storage;
        var rows = documents.Details["Rows"];

        var links = Column(rows, "Contract").OwnerLinks(rows, documents);

        Assert.Equal(2, links.Count);
        Assert.All(links, l => Assert.True(l.Header));
    }

    // no owner column on the target - no filter, and nothing is asked
    [Fact]
    public async Task A_reference_to_a_record_that_belongs_to_nobody_is_not_filtered()
    {
        var documents = (await LoadNormalAsync("document", String.Empty)).Storage;

        Assert.Empty(Column(documents, "Agent").OwnerLinks(documents, null));
    }

    [Fact]
    public async Task Two_fields_that_could_hold_the_owner_are_refused()
    {
        var agent = await LoadNormalAsync("catalog", "agent");
        var contract = await LoadNormalAsync("catalog", "contract");
        var table = JsonConvert.DeserializeObject<TableMetadata>("""
            { "table": "T", "fields": { "Number": { "type": "autonum" },
                "Agent": { "type": "ref", "target": "/catalog/agent" },
                "Payer": { "type": "ref", "target": "/catalog/agent" },
                "Contract": { "type": "ref", "target": "/catalog/contract" } } }
            """, JsonSettings.CamelCaseSerializerSettings)!;
        table.SetDefaults(TableKind.Document, "document", "x");
        foreach (var c in table.Columns.Where(c => c.IsRef))
            c.RefTable = c.Name == "Contract" ? contract : agent;

        var ex = Assert.Throws<InvalidOperationException>(() => Column(table, "Contract").OwnerLinks(table, null));
        Assert.Contains("[Agent], [Payer]", ex.Message);
    }

    // the picker sends its owners to both roads: the browse url and the fetch
    [Fact]
    public async Task The_picker_sends_the_owners_of_its_record()
    {
        var view = await Materialize("/document/waybillin", "edit", MaterializeWhat.View);
        var template = await Materialize("/document/waybillin", "edit", MaterializeWhat.Template);

        Assert.Contains("""Data="{Bind Document.$ContractOwners}""", view);
        Assert.Contains("""Data="{Bind $ContractOwners}""", view);
        Assert.Contains("'TDocument.$ContractOwners'", template);
        Assert.Contains("Agent: this.Agent.$id, Company: this.Company.$id", template);
        Assert.Contains("Agent: this.$root.Document.Agent.$id", template);
    }

    // an id or nothing: an empty one filters nothing, and a column that is not an owner is never read
    [Fact]
    public async Task The_fetch_reads_owner_columns_only_and_skips_an_empty_one()
    {
        var contracts = (await LoadNormalAsync("catalog", "contract")).Storage;
        var prms = new ExpandoObject();
        var d = (IDictionary<String, Object?>)prms;
        d["Agent"] = 5L;
        d["Company"] = 0L;
        d["Name"] = "x";

        var owners = SqlBuilder.FetchOwners(contracts, new AppPlatformId(typeof(Int64)), prms);

        var (column, id) = Assert.Single(owners);
        Assert.Equal("Agent", column.Name);
        Assert.Equal(5L, id);
    }

    // the header against its own fields, a row against the header's
    [Fact]
    public async Task The_save_holds_the_contract_against_its_owners()
    {
        var documents = (await LoadNormalAsync("document", String.Empty)).Storage;

        var sql = SqlBuilder.OwnerCheck(documents);

        Assert.Contains("from @Document s\n", sql.Replace("\r", ""));
        Assert.Contains("s.[Agent] is not null and (r.[Agent] is null or r.[Agent] <> s.[Agent])", sql);
        Assert.Contains("cross join @Document h", sql);
        Assert.Contains("h.[Company] is not null and (r.[Company] is null or r.[Company] <> h.[Company])", sql);
        Assert.Contains("N'UI:@[Error.Owner]'", sql);
    }
}
