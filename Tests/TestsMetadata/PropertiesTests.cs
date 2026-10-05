// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* 'properties': members of the model whose value is JS - a getter, and a setter when it can be typed
 * into. Over a field, or a '$' name with no column. TestApp: /document - the header's Sum over both
 * kinds; Stock computes Sum, Service computes it and takes it back into Qty, and holds $Base.
 * See a2v10-md-skill, rules.md -> properties.
 */
public class PropertiesTests
{
    static readonly AppPlatformId PlatformId = new(typeof(Int64));

    static async Task<NormalEndpointMetadata> LoadNormalAsync(String schema, String table) =>
        Assert.IsType<NormalEndpointMetadata>(
            await TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, schema, table));

    static async Task<IReadOnlyList<MaterializedFile>> Materialize(String endpoint, String action, MaterializeWhat what)
    {
        var mat = new EndpointMaterializer(TestHost.GetService<DatabaseMetadataProvider>());
        return (await mat.MaterializeAsync(endpoint, action, what)).Files;
    }

    // a kind is its own type: a getter in one, a getter with a setter in the other
    [Fact]
    public async Task A_property_lands_on_the_type_of_its_row_set()
    {
        var template = (await Materialize("/document/waybillin", "edit", MaterializeWhat.Template))[0].Text;

        Assert.Contains("'TDocument.Sum'(this: TDocument) { return this.StockRows.$sum(r => r.Sum) + this.ServiceRows.$sum(r => r.Sum); }", template);
        Assert.Contains("'TStockRow.Sum'(this: TStockRow) { return this.Qty * this.Price; }", template);
        Assert.Contains("'TServiceRow.Sum': { get(this: TServiceRow) { return this.Qty * this.Price; }, set(this: TServiceRow, value: any) { this.Qty = value / this.Price; } }", template);
        Assert.Contains("'TServiceRow.$Base'(this: TServiceRow) { return this.Qty * this.Price; }", template);
        Assert.DoesNotContain("'TStockRow.$Base'", template);
    }

    // a column is already a member with its own type; a '$' name is one only here
    [Fact]
    public async Task The_map_declares_a_dollar_name_and_not_a_column_twice()
    {
        var map = (await Materialize("/document/waybillin", "edit", MaterializeWhat.Template))
            .Single(f => f.Path.EndsWith(".d.ts")).Text;

        Assert.Contains("readonly $Base: any;", map);
        Assert.DoesNotContain("readonly Sum: any;", map);
    }

    // what is typed into a getter would not be kept, so it is shown; a setter takes it
    [Fact]
    public async Task A_getter_is_shown_and_a_setter_is_typed_into()
    {
        var endpoint = await LoadNormalAsync("document", "waybillin");
        var tabs = endpoint.Declaration.Form(Constants.FormNames.Edit).TabStrips().Single().Elements;

        var stock = tabs.Single(t => t.Kind == "Stock").Members.Single(m => m.Name == "Sum");
        var service = tabs.Single(t => t.Kind == "Service").Members.Single(m => m.Name == "Sum");

        Assert.True(stock.IsShownOnly);
        Assert.False(service.IsShownOnly);
        Assert.Equal(MemberKind.Column, service.Kind);
    }

    // the edit form names a '$' property like a field, from the scope it stands in
    [Fact]
    public async Task A_form_names_a_dollar_property()
    {
        var storage = (await LoadNormalAsync("document", String.Empty)).Storage;
        var declaration = JsonConvert.DeserializeObject<DeclarationMetadata>("""
            {
              "properties": { "$Due": { "type": "Amount", "get": "this.Sum" } },
              "forms": { "edit": { "is": "page", "body": [
                { "is": "group", "fields": [ "Number", "$Due" ] },
                { "is": "tabs", "elements": [ { "is": "tab", "scope": "Rows", "kind": "Service", "fields": [ "Item", "$Base" ] } ] }
              ] } },
              "details": { "Rows": { "kinds": { "Service": { "properties": {
                "$Base": { "type": "Amount", "get": "this.Qty * this.Price" } } } } } }
            }
            """, JsonSettings.CamelCaseSerializerSettings)!;

        var form = declaration.Bake(storage, PlatformId).BakedForms[Constants.FormNames.Edit];

        var due = form.Body[0].Members.Single(m => m.Name == "$Due");
        Assert.Equal(MemberKind.Property, due.Kind);
        Assert.Equal(ColumnType.Amount, due.Type);
        Assert.True(due.IsShownOnly);
        Assert.Equal("@[Due]", due.Header);
        Assert.Contains(form.Body[1].Elements[0].Members, m => m.Name == "$Base");
    }

    // a list has no template that carries properties, so it cannot name one
    [Fact]
    public async Task An_index_form_cannot_name_a_property()
    {
        var storage = (await LoadNormalAsync("document", String.Empty)).Storage;
        var declaration = JsonConvert.DeserializeObject<DeclarationMetadata>("""
            {
              "properties": { "$Due": { "type": "Amount", "get": "this.Sum" } },
              "forms": { "index": { "is": "page", "body": [ { "is": "dataGrid", "fields": [ "Number", "$Due" ] } ] } }
            }
            """, JsonSettings.CamelCaseSerializerSettings)!;

        var ex = Assert.Throws<InvalidOperationException>(() => declaration.Bake(storage, PlatformId));
        Assert.Contains("field '$Due' not found", ex.Message);
    }

    [Theory]
    [InlineData("""{ "properties": { "$Due": { "get": "this.Sum" } } }""", "'$Due' has no column, so 'type' says what it is")]
    [InlineData("""{ "properties": { "$Who": { "type": "Ref", "get": "this.Agent" } } }""", "'$Who' is of type 'Ref'")]
    [InlineData("""{ "properties": { "Total": { "get": "1" } } }""", "field 'Total' not found")]
    [InlineData("""{ "properties": { "Sum": { "type": "Amount", "get": "1" } } }""", "'Sum' declares 'type'")]
    [InlineData("""{ "properties": { "Sum": { "set": "this.Qty = value" } } }""", "'Sum' declares no 'get'")]
    [InlineData("""{ "rules": { "inherit": { "Contract": { "ref": "Agent", "field": "Contract" } } }, "properties": { "Contract": { "get": "null" } } }""",
        "'Contract' is also in 'inherit'")]
    public async Task A_property_that_cannot_hold_is_refused(String json, String message)
    {
        var storage = (await LoadNormalAsync("document", String.Empty)).Storage;
        var declaration = JsonConvert.DeserializeObject<DeclarationMetadata>(json, JsonSettings.CamelCaseSerializerSettings)!;

        var ex = Assert.Throws<InvalidOperationException>(() => declaration.Bake(storage, PlatformId));
        Assert.Contains(message, ex.Message);
    }

    // one type serves every operation of a document, so a property of one is a test nobody writes yet
    [Fact]
    public void An_operation_declaring_properties_is_refused()
    {
        var operation = JsonConvert.DeserializeObject<OperationFileMetadata>("""
            { "post": [ { "journal": "/journal/stock", "dir": "in" } ], "properties": { "$X": { "type": "Amount", "get": "1" } } }
            """, JsonSettings.CamelCaseSerializerSettings)!;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            DatabaseMetadataProvider.ToOperationDeclaration("x.operation.json", "receipt", "x", operation));
        Assert.Contains("declares 'properties'", ex.Message);
    }

    // the model has no browser for the setter to run in: it sends the primary fields itself
    [Fact]
    public async Task For_the_model_a_column_under_a_property_is_computed()
    {
        var endpoint = await LoadNormalAsync("document", "waybillin");
        var entity = new McpEntity("waybillin", endpoint.Path, "document", "waybillin", "document", null, null);

        var info = McpProjection.Describe(endpoint, entity, _ => null);

        Assert.True(info.Fields.Single(f => f.Name == "Sum").Computed);
        Assert.True(info.Collections.Single(c => c.Name == "ServiceRows").Fields.Single(f => f.Name == "Sum").Computed);
    }
}
