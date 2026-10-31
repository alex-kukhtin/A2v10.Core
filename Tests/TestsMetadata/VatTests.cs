// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* 'vat': role -> path, on the rules of a collection. TestApp: /document - the header's IsVat,
 * VatIncluded, Manual; the rows' Qty, Price, VatRate (a set carrying Rate), Sum, Vat, Total, and Cost
 * under 'sqlAs'. See a2v10-md-skill, concepts/vat.
 */
public class VatTests
{
    static readonly AppPlatformId PlatformId = new(typeof(Int64));

    static async Task<TableMetadata> StorageAsync() =>
        Assert.IsType<NormalEndpointMetadata>(
            await TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, "document", String.Empty)).Storage;

    const String Roles = """
        "Qty": "Qty", "Price": "Price", "Rate": "VatRate.Rate", "Sum": "Sum", "Vat": "Vat", "Total": "Total",
        "IsVat": "$root.Document.IsVat", "Included": "$root.Document.VatIncluded", "Manual": "$root.Document.Manual"
        """;

    // 'rules' and 'rows' add to the rules and to the collection; 'root' to the record
    static DeclarationMetadata OnRows(String vat, String rules = "", String rows = "", String root = "") =>
        JsonConvert.DeserializeObject<DeclarationMetadata>(
            $$"""{ {{root}} "details": { "Rows": { "rules": { "vat": { {{vat}} } {{rules}} } {{rows}} } } }""",
            JsonSettings.CamelCaseSerializerSettings)!;

    [Fact]
    public async Task A_path_resolves_in_the_row_in_the_root_and_through_a_reference()
    {
        var baked = OnRows(Roles).Bake(await StorageAsync(), PlatformId);

        var vat = baked.AllRowSets().Single(rs => rs.Kind == "Stock").Vat!.Roles;
        Assert.Equal(("Qty", false, null), (vat["Qty"].Column.Name, vat["Qty"].IsRoot, vat["Qty"].RefField));
        Assert.Equal(("VatRate", false, "Rate"), (vat["Rate"].Column.Name, vat["Rate"].IsRoot, vat["Rate"].RefField));
        Assert.Equal(("VatIncluded", true, null), (vat["Included"].Column.Name, vat["Included"].IsRoot, vat["Included"].RefField));
    }

    // by key a kind could not drop a role: the service's map has no Qty and Price
    [Fact]
    public async Task A_kind_replaces_the_map_of_the_collection_whole()
    {
        var declaration = OnRows(Roles, rows: """
            , "kinds": { "Service": { "rules": { "vat": { "Rate": "VatRate.Rate", "Sum": "Sum", "Vat": "Vat", "Total": "Total" } } } }
            """);

        var rowSets = declaration.Bake(await StorageAsync(), PlatformId).AllRowSets().ToList();

        Assert.Contains("Qty", rowSets.Single(rs => rs.Kind == "Stock").Vat!.Roles.Keys);
        Assert.Equal(["Rate", "Sum", "Vat", "Total"], rowSets.Single(rs => rs.Kind == "Service").Vat!.Roles.Keys);
    }

    [Theory]
    [InlineData(""" "Rate": "VatRate.Rate", "Sum": "Sum", "Vat": "Vat", "Total": "Total", "Amount": "Sum" """, "[Amount] declared, and the roles are")]
    [InlineData(""" "Rate": "VatRate.Rate", "Sum": "Sum", "Vat": "Vat" """, "[Total] not named")]
    [InlineData(""" "Qty": "Qty", "Rate": "VatRate.Rate", "Sum": "Sum", "Vat": "Vat", "Total": "Total" """, "named together or not at all")]
    [InlineData(""" "Rate": "VatRate.Rate", "Sum": "$root.Document.Sum", "Vat": "Vat", "Total": "Total" """, "names a field of the row itself")]
    [InlineData(""" "Rate": "VatRate.Rate", "Sum": "Item.Sum", "Vat": "Vat", "Total": "Total" """, "names a field of the row itself")]
    [InlineData(""" "Rate": "VatRate.Rate", "Sum": "Sum", "Vat": "Vat", "Total": "Total", "IsVat": "$root.Doc.IsVat" """, "the root is 'Document'")]
    [InlineData(""" "Rate": "VatRate.Rate", "Sum": "Sum", "Vat": "Vat", "Total": "Total", "IsVat": "$root.Document" """, "a path is a field")]
    [InlineData(""" "Rate": "VatRate.Rate.Value", "Sum": "Sum", "Vat": "Vat", "Total": "Total" """, "a path is a field")]
    [InlineData(""" "Rate": "VatRate.Rate", "Sum": "Sum", "Vat": "Vat", "Total": "Totl" """, "field 'Totl' not found")]
    [InlineData(""" "Rate": "Qty.Rate", "Sum": "Sum", "Vat": "Vat", "Total": "Total" """, "'Qty' is not a reference")]
    [InlineData(""" "Rate": "VatRate", "Sum": "Sum", "Vat": "Vat", "Total": "Total" """, "'Rate' is 'Percent', and 'VatRate' is 'Enum'")]
    [InlineData(""" "Rate": "VatRate.Rate", "Sum": "Price", "Vat": "Vat", "Total": "Total" """, "'Sum' is 'Amount', and 'Price' is 'Price'")]
    [InlineData(""" "Rate": "VatRate.Rate", "Sum": "Sum", "Vat": "Vat", "Total": "Cost" """, "'Cost' has 'sqlAs'")]
    public async Task A_map_that_cannot_hold_is_refused(String vat, String message)
    {
        var storage = await StorageAsync();

        var ex = Assert.Throws<InvalidOperationException>(() => OnRows(vat).Bake(storage, PlatformId));
        Assert.Contains(message, ex.Message);
    }

    [Theory]
    // an output has one source of value
    [InlineData(""", "inherit": { "Total": { "ref": "Item", "field": "Price" } }""", "", "'Total' is also in 'inherit'")]
    [InlineData("", """, "properties": { "Sum": { "get": "this.Qty * this.Price" } }""", "'Sum' is written by 'vat' as 'Sum'")]
    // a getter emits no '.change'
    [InlineData("", """, "kinds": { "Stock": { "properties": { "Price": { "get": "1" } } } }""", "'Price' is read by 'vat' as 'Price'")]
    public async Task A_field_of_the_rule_with_a_second_source_is_refused(String rules, String rows, String message)
    {
        var storage = await StorageAsync();

        var ex = Assert.Throws<InvalidOperationException>(() => OnRows(Roles, rules, rows).Bake(storage, PlatformId));
        Assert.Contains(message, ex.Message);
    }

    // the root holds its properties, so it asks for the '$root' roles
    [Fact]
    public async Task A_header_field_of_the_rule_under_a_property_is_refused()
    {
        var storage = await StorageAsync();
        var declaration = OnRows(Roles, root: """ "properties": { "IsVat": { "get": "this.Agent != null" } }, """);

        var ex = Assert.Throws<InvalidOperationException>(() => declaration.Bake(storage, PlatformId));
        Assert.Contains("'IsVat' is read by 'vat' as 'IsVat'", ex.Message);
    }

    [Fact]
    public async Task Vat_on_the_record_is_refused()
    {
        var storage = await StorageAsync();
        var declaration = JsonConvert.DeserializeObject<DeclarationMetadata>(
            $$"""{ "rules": { "vat": { {{Roles}} } } }""", JsonSettings.CamelCaseSerializerSettings)!;

        var ex = Assert.Throws<InvalidOperationException>(() => declaration.Bake(storage, PlatformId));
        Assert.Contains("which is a record", ex.Message);
    }

    // an operation's 'vat' becomes a vat under a condition, which nothing generates
    [Fact]
    public async Task An_operation_declaring_vat_is_refused()
    {
        var storage = await StorageAsync();
        var operation = JsonConvert.DeserializeObject<OperationFileMetadata>($$"""
            { "post": [ { "journal": "/journal/stock", "dir": "in" } ],
              "details": { "Rows": { "rules": { "vat": { {{Roles}} } } } } }
            """, JsonSettings.CamelCaseSerializerSettings)!;
        var declaration = new DeclarationMetadata()
        {
            Storage = "/document",
            Operations = ["x"],
            OperationDeclarations = [DatabaseMetadataProvider.ToOperationDeclaration("x.operation.json", "receipt", "x", operation)]
        };

        var ex = Assert.Throws<InvalidOperationException>(() => declaration.Bake(storage, PlatformId));
        Assert.Contains("declares 'vat'", ex.Message);
    }

    // phase 2: the far half is read off the linked target - what the reference carries
    [Fact]
    public async Task A_hop_reads_a_field_the_reference_carries()
    {
        var baked = OnRows(Roles).Bake(await StorageAsync(), PlatformId);

        DatabaseMetadataProvider.CheckVatHops("/document", baked);
    }

    [Fact]
    public async Task A_hop_to_a_field_the_reference_does_not_carry_is_refused()
    {
        var baked = OnRows(Roles.Replace("VatRate.Rate", "Item.Rate")).Bake(await StorageAsync(), PlatformId);

        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckVatHops("/document", baked));
        Assert.Contains("reads 'Rate' through [Item]", ex.Message);
    }
}
