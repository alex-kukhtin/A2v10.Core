// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* The generated screen, as text. The materializer builds it with the same XamlBuilder the runtime
 * uses and holds no IDbContext, so this needs no database - TESTS.md, layer D.
 *
 * What it pins is the shape of the state control in each of its three reaches. Every one of them
 * fails silently: a badge with no colour reads as a plain grey status, and a picker bound to the
 * wrong half writes a code where the model keeps an object.
 */
public class ViewXamlTests
{
    static async Task<String> ViewOf(String endpoint, String action)
    {
        var mat = new EndpointMaterializer(TestHost.GetService<DatabaseMetadataProvider>());
        var res = await mat.MaterializeAsync(endpoint, action, MaterializeWhat.View);
        return res.Files[0].Text;
    }

    // drawn, not spelled: the row's own colour is a class name, so Style is a binding
    [Fact]
    public async Task A_state_is_a_badge_in_the_grid()
    {
        var xaml = await ViewOf("/document/waybillin", "index");

        Assert.Contains("""<TagLabel Content="{Bind State.Name}" Style="{Bind State.Color}" />""", xaml);
    }

    /* A column of a set offers no sort: neither the set's Order nor its Name is the alphabet the
     * cell shows, so the builder writes Sort = false (IndexPageXaml).
     *
     * Written red: the attribute was lost on the way to the text - the object carried it, the
     * runtime honoured it, and a page ejected as text sorted where the runtime refuses to, which is
     * the rule ISSUES 6.9 exists for. The loss was in the writer, and it is green since
     * A2v10.System.Xaml 10.1.8057. It stays because nothing else notices the difference: both
     * halves render, and only one of them obeys.
     */
    [Fact]
    public async Task The_ban_on_sorting_a_set_reaches_the_materialized_text()
    {
        var xaml = await ViewOf("/document/waybillin", "index");

        // by the header, and on one line - the attribute's position is the writer's business
        var column = xaml.Split('\n').Single(l => l.Contains("@[State]"));

        Assert.Contains("""Sort="False" """.TrimEnd(), column);
    }

    /* The panel shows both questions, each drawn by what its candidates ARE: the states come from
     * the set and carry colours, the roles are a closed set of the platform and are written out -
     * 'All' with them, since there is no deploy here to add that row.
     */
    [Fact]
    public async Task The_panel_filters_by_state_and_by_role()
    {
        var xaml = await ViewOf("/document/waybillin", "index");

        Assert.Contains("""<ColorComboBox ItemsSource="{Bind OrderStates}" Value="{Bind Parent.Filter.State}" """, xaml);
        Assert.Contains("""<ComboBox Highlight="True" Value="{Bind Parent.Filter.StateRole}" """, xaml);
        foreach (var role in Enum.GetNames<StateRole>())
            Assert.Contains($"""<ComboBoxItem Value="{role}">@[StateRole.{role}]</ComboBoxItem>""", xaml);
    }

    /* The card holds the ELEMENT resolved through the map, so the item binds to it and the save
     * reads its Id like any other reference; the filter holds the bare CODE, which is what the
     * WHERE compares and what lets the set's own 'All' row be a value like any other.
     */
    [Fact]
    public async Task The_picker_binds_to_the_element_in_the_card_and_to_the_code_in_the_filter()
    {
        var card = await ViewOf("/document/waybillin", "edit");
        var index = await ViewOf("/document/waybillin", "index");

        Assert.Contains("""<ColorComboBoxItem Content="{Bind Name}" Value="{Bind}" Color="{Bind Color}" />""", card);
        Assert.Contains("""<ColorComboBoxItem Content="{Bind Name}" Value="{Bind Id}" Color="{Bind Color}" />""", index);
    }
}
