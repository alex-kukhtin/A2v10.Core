// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Dynamic;
using System.Text.RegularExpressions;

using Newtonsoft.Json;

using A2v10.Metadata;
using A2v10.Services;

namespace A2v10.Metadata.Tests;

/* 'lookup' on the target: the other columns a row is chosen by. TestApp: catalog/item is chosen by
 * Article (contains) and Barcode (equal, unique); the rows of /document reference it. A channel is a
 * column of input of the reference - a picker showing it, or a text box and an exact search. See
 * a2v10-md-skill, metadata.md -> lookup.
 */
public class LookupTests
{
    static TableMetadata Declared(String schema, String json)
    {
        var table = JsonConvert.DeserializeObject<TableMetadata>(json, JsonSettings.CamelCaseSerializerSettings)!;
        table.SetDefaults(schema.ToTableKind()!.Value, schema, "x");
        return table;
    }

    static async Task<NormalEndpointMetadata> EndpointAsync(String schema, String table) =>
        await TestHost.GetService<DatabaseMetadataProvider>().GetNormalEndpointAsync(null, schema, table);

    static async Task<SqlBuilder> BuilderAsync(NormalEndpointMetadata endpoint, String url)
    {
        var provider = TestHost.GetService<DatabaseMetadataProvider>();
        return new SqlBuilder(new BuilderDescriptor()
        {
            Endpoint = endpoint,
            PlatformUrl = new PlatformUrl(url),
            PlatformId = await provider.GetPlatformIdAsync(null),
            UseGrants = false,
        }, TestHost.Services);
    }

    static async Task<String> Materialized(String endpoint, MaterializeWhat what, String file)
    {
        var mat = TestHost.GetService<EndpointMaterializer>();
        return (await mat.MaterializeAsync(endpoint, "edit", what)).Files.First(f => f.Path.EndsWith(file)).Text;
    }

    [Fact]
    public void The_channels_are_columns_of_the_target_in_the_order_written()
    {
        var item = Declared("catalog", """
            { "fields": { "Article": { "type": "String" }, "Barcode": { "type": "String", "unique": true } },
              "lookup": { "Barcode": "equal", "Article": "contains" } }
            """);

        Assert.Equal([("Barcode", LookupMatch.Equal), ("Article", LookupMatch.Contains)],
            item.Lookup.Select(l => (l.Column.Name, l.Match)));
    }

    [Theory]
    [InlineData("catalog", """{ "lookup": { "Code": "contains" } }""", "lookup 'Code' is not a column")]
    [InlineData("catalog", """{ "fields": { "Unit": { "type": "Ref", "target": "/catalog/unit" } }, "lookup": { "Unit": "contains" } }""", "is a reference")]
    [InlineData("catalog", """{ "lookup": { "Name": "contains" } }""", "is displayAs")]
    [InlineData("catalog", """{ "fields": { "Barcode": { "type": "String" } }, "lookup": { "Barcode": "equal" } }""", "without 'unique'")]
    [InlineData("journal", """{ "fields": { "Code": { "type": "String" } }, "lookup": { "Code": "contains" } }""", "'lookup' on a Journal")]
    public void A_channel_that_cannot_find_a_record_is_refused(String schema, String json, String message)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Declared(schema, json));
        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public void A_match_is_contains_or_equal()
    {
        Assert.ThrowsAny<JsonException>(() => Declared("catalog", """
            { "fields": { "Code": { "type": "String" } }, "lookup": { "Code": "like" } }
            """));
    }

    // one fetch for every place of choice: the selector of any channel types into it
    [Fact]
    public async Task The_fetch_searches_by_every_channel_and_sends_them()
    {
        var item = await EndpointAsync("catalog", "item");
        dynamic prms = new ExpandoObject();
        prms.inherit = "Unit,Article";

        String sql = (await BuilderAsync(item, "_page/catalog/item/index/0")).FetchSqlText(prms);

        Assert.Contains("(a.[Name] like @fr or a.[Article] like @fr or a.[Barcode] = @Text)", sql);
        // an inherit reading a channel does not send it twice
        Assert.Contains("[Name!!Name] = a.[Name], a.[Article], a.[Barcode],\n    [Unit!TRUnit!RefId] = a.[Unit]\n", sql);
    }

    [Fact]
    public async Task The_exact_search_finds_one_record_or_none()
    {
        var item = await EndpointAsync("catalog", "item");
        var builder = await BuilderAsync(item, "_page/catalog/item/index/0");
        dynamic prms = new ExpandoObject();
        prms.Channel = "Barcode";

        String sql = builder.LookupSqlText(prms);

        Assert.Contains("select top(2) a.Id", sql);
        Assert.Contains("(a.[Barcode] = @Text)", sql);
        Assert.Contains("if (select count(*) from @map) > 1\n    throw 60000, N'UI:@[Error.Lookup.NotUnique]', 0;", sql);

        dynamic contains = new ExpandoObject();
        contains.Channel = "Article";
        var ex = Assert.Throws<InvalidOperationException>(() => (String)builder.LookupSqlText(contains));
        Assert.Contains("is no 'equal' channel of /catalog/item", ex.Message);
    }

    // the card is where the target is chosen: its map sends the channels, its type promises them
    [Fact]
    public async Task The_card_carries_the_channels_of_its_references()
    {
        var waybill = await EndpointAsync("document", "waybillin");

        var load = (await BuilderAsync(waybill, "_page/document/waybillin/edit/1")).BuildLoadPlainSqlText();
        var map = await Materialized("/document/waybillin", MaterializeWhat.Template, "edit.d.ts");

        Assert.Contains("select [!TRItem!Map] = null, [Id!!Id] = a.Id, [Name!!Name] = a.[Name], a.[Article], a.[Barcode], [Unit!TRUnit!RefId]", load);
        Assert.Contains("\treadonly Article: string;\n\tBarcode: string;\n", map);
    }

    // an index shows a reference: no channel in its map or its type
    [Fact]
    public async Task An_index_does_not_carry_them()
    {
        var map = await TestHost.GetService<EndpointMaterializer>().MaterializeAsync("/document/waybillin", "index", MaterializeWhat.Template);

        Assert.All(map.Files, f => Assert.DoesNotContain("Barcode", f.Text));
    }

    [Fact]
    public async Task The_default_form_puts_the_channels_after_the_reference()
    {
        var waybill = await EndpointAsync("document", "waybillin");

        var rows = waybill.Form(Constants.FormNames.Edit).TabStrips().Single().Elements[0].Members.Select(m => m.Name).ToList();

        Assert.Equal(["Item", "Item.Article", "Item.Barcode"], rows.SkipWhile(n => n != "Item").Take(3));
    }

    [Fact]
    public async Task A_channel_is_a_picker_or_a_text_box()
    {
        var view = await Materialized("/document/waybillin", MaterializeWhat.View, "edit.view.vxaml");

        Assert.Contains("""<SelectorSimple Url="/catalog/item?inherit=Unit,VatRate" DisplayProperty="Article" Value="{Bind Item}">""", view);
        Assert.Contains("""<TextBox Value="{Bind Item.Barcode}" />""", view);
        // the candidates: what the target is chosen by, then what it is shown by
        Assert.Contains("""<DataGrid Compact="True">"""
            + """<DataGridColumn Content="{Bind Article}" Header="@[Article]" />"""
            + """<DataGridColumn Content="{Bind Barcode}" Header="@[Barcode]" />"""
            + """<DataGridColumn Content="{Bind Name}" Header="@[Name]" />"""
            + "</DataGrid>", Regex.Replace(view, @">\s+<", "><"));
    }

    [Fact]
    public async Task An_equal_channel_is_searched_on_its_change()
    {
        var template = await Materialized("/document/waybillin", MaterializeWhat.Template, "edit.template.ts");

        Assert.Contains("""
            async 'Document.StockRows[].Item.Barcode.change'(this: TRoot, item: TRItem, val: string) {
            			if (!val) { item.$empty(); return; }
            			const r = await this.$ctrl.$invoke('lookup', { Channel: 'Barcode', Text: val, inherit: 'Unit,VatRate' }, '/catalog/item');
            			const found = r?.Items;
            			found?.length ? item.$merge(found[0]) : item.$empty();
            		}
            """, template);
        Assert.DoesNotContain("'Document.StockRows[].Item.Article.change'", template);
    }

    [Fact]
    public async Task A_form_may_name_a_channel_and_nothing_else_of_the_target()
    {
        var storage = (await EndpointAsync("document", String.Empty)).Storage;
        DeclarationMetadata Form(String field) => JsonConvert.DeserializeObject<DeclarationMetadata>($$"""
            { "forms": { "edit": { "is": "page", "body": [ { "is": "tabs", "elements": [
                { "is": "tab", "scope": "Rows", "kind": "Stock", "fields": [ "Item", "{{field}}" ] } ] } ] } } }
            """, JsonSettings.CamelCaseSerializerSettings)!;

        var form = Form("Item.Barcode").Bake(storage).BuildForms(storage, new AppPlatformId(typeof(Int64)))[Constants.FormNames.Edit];
        Assert.Equal(MemberKind.Channel, form.Body[0].Elements[0].Members[1].Kind);

        var ex = Assert.Throws<InvalidOperationException>(() => Form("Item.Unit").Bake(storage).BuildForms(storage, new AppPlatformId(typeof(Int64))));
        Assert.Contains("field 'Item.Unit' not found", ex.Message);
    }
}
