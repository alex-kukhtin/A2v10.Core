// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* 'when': rules under a condition, and the rules of an operation, which are 'when' under its code.
 * TestApp: document/receipt requires Agent and StoreTo when Sum > 1000; its operation 'supplier'
 * requires Agent, and Contract on Stock rows. See a2v10-md-skill, rules.md -> when.
 */
public class WhenTests
{
    static readonly AppPlatformId PlatformId = new(typeof(Int64));

    static async Task<NormalEndpointMetadata> LoadNormalAsync(String schema, String table) =>
        Assert.IsType<NormalEndpointMetadata>(
            await TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, schema, table));

    static async Task<String> EditTemplate(String endpoint)
    {
        var mat = new EndpointMaterializer(TestHost.GetService<DatabaseMetadataProvider>());
        return (await mat.MaterializeAsync(endpoint, "edit", MaterializeWhat.Template)).Files[0].Text;
    }

    const String Supplier = "this.$root.Document.Operation.Id === 'receipt.supplier'";

    // one requirement per field: the conditions of one field join, an unconditional one takes them all
    [Fact]
    public async Task Required_under_conditions_is_one_rule_whose_when_holds_if_any_does()
    {
        var template = await EditTemplate("/document/receipt");

        Assert.Contains("'Document.StoreTo': `@[Error.Required]`", template);
        Assert.Contains($$$"""'Document.Agent': {valid: 'notBlank', msg: `@[Error.Required]`, when(this: TDocument) { return (this.Sum > 1000) || ({{{Supplier}}}); }}""", template);
    }

    // the test of an operation is derived, and read through $root so the rows spell it the same
    [Fact]
    public async Task The_rules_of_an_operation_hold_under_its_code()
    {
        var template = await EditTemplate("/document/receipt");

        Assert.Contains($$$"""'Document.StockRows[].Contract': {valid: 'notBlank', msg: `@[Error.Required]`, when(this: TStockRow) { return {{{Supplier}}}; }}""", template);
        Assert.DoesNotContain("'Document.ServiceRows[].Contract'", template);
    }

    [Fact]
    public void When_concatenates_over_the_storage()
    {
        var storage = new RuleMetadata() { When = [new() { Test = "a" }] };
        var own = new RuleMetadata() { When = [new() { Test = "b" }] };

        Assert.Equal(["a", "b"], RuleMetadata.Merge(own, storage).When.Select(w => w.Test));
    }

    [Theory]
    [InlineData("""{ "rules": { "when": [ { "required": [ "Agent" ] } ] } }""", "declares no 'test'")]
    [InlineData("""{ "rules": { "when": [ { "test": "this.Sum > 0", "required": [ "Agnet" ] } ] } }""", "field 'Agnet' not found")]
    [InlineData("""{ "rules": { "when": [ { "test": "this.Sum > 0", "inherit": { "Contract": { "ref": "Agent", "field": "Contract" } } } ] } }""", "declares 'inherit'")]
    [InlineData("""{ "details": { "Rows": { "rules": { "when": [ { "test": "this.Qty > 0", "total": [ "Sum" ] } ] } } } }""", "declares 'total'")]
    public async Task A_when_that_cannot_hold_is_refused(String json, String message)
    {
        var storage = (await LoadNormalAsync("document", String.Empty)).Storage;
        var declaration = JsonConvert.DeserializeObject<DeclarationMetadata>(json, JsonSettings.CamelCaseSerializerSettings)!;

        var ex = Assert.Throws<InvalidOperationException>(() => declaration.Bake(storage, PlatformId));
        Assert.Contains(message, ex.Message);
    }

    // an operation's 'inherit' becomes an inherit under a condition, which nothing generates
    [Fact]
    public async Task An_operation_declaring_inherit_is_refused()
    {
        var storage = (await LoadNormalAsync("document", String.Empty)).Storage;
        var operation = JsonConvert.DeserializeObject<OperationFileMetadata>("""
            { "post": [ { "journal": "/journal/stock", "dir": "in" } ],
              "details": { "Rows": { "rules": { "inherit": { "Price": { "ref": "Item", "field": "Price" } } } } } }
            """, JsonSettings.CamelCaseSerializerSettings)!;
        var declaration = new DeclarationMetadata()
        {
            Storage = "/document",
            Operations = ["x"],
            OperationDeclarations = [DatabaseMetadataProvider.ToOperationDeclaration("x.operation.json", "receipt", "x", operation)]
        };

        var ex = Assert.Throws<InvalidOperationException>(() => declaration.Bake(storage, PlatformId));
        Assert.Contains("declares 'inherit'", ex.Message);
        Assert.Contains("'receipt.x'", ex.Message);
    }
}
