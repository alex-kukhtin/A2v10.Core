// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using A2v10.Platform.Web;

namespace A2v10.Metadata.Tests;

/* menu.json under 'useGrants': what a user sees of it. A leaf by CanView on its url, 'create' by
 * CanCreate, a node while a child that is not a spacer lives, a spacer always. See JsonMenuRoot.Transform.
 */
public class MenuGrantsTests
{
    static readonly Dictionary<String, PermissionFlag> _grants = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/catalog/agent"] = PermissionFlag.CanView,
        ["/document/waybillin"] = PermissionFlag.CanView | PermissionFlag.CanCreate,
        ["/catalog/unit"] = PermissionFlag.CanView,
        ["/report/margin"] = PermissionFlag.CanCreate
    };

    // the roles the procedure returns: assigned ones and Everyone
    static readonly HashSet<String> _roles = new(StringComparer.Ordinal) { "Storekeeper", "Everyone" };

    static JsonMenu Leaf(String url, Boolean create = false) => new() { Title = url, Url = url, Create = create };
    static JsonMenu Node(String title, params JsonMenu[] items) => new() { Title = title, Items = [.. items] };

    [Fact]
    public void A_leaf_is_seen_by_view_on_its_url()
    {
        var menu = JsonMenuRoot.Transform([Node("Sales", Node("Catalogs", Leaf("/catalog/agent"), Leaf("/catalog/contract")))], _grants, _roles)!;

        Assert.Equal(["/catalog/agent"], menu[0].Items![0].Items!.Select(i => i.Url));
    }

    [Fact]
    public void Create_without_view_hides_the_leaf()
    {
        Assert.Empty(JsonMenuRoot.Transform([Node("Reports", Node("All", Leaf("/report/margin")))], _grants, _roles)!);
    }

    [Fact]
    public void Create_goes_out_without_the_right_and_the_leaf_stays()
    {
        var menu = JsonMenuRoot.Transform([Node("Stock", Node("Docs", Leaf("/catalog/agent", create: true), Leaf("/document/waybillin", create: true)))], _grants, _roles)!;

        var leaves = menu[0].Items![0].Items!;
        Assert.False(leaves[0].Create);
        Assert.True(leaves[1].Create);
    }

    // a section, a subsection and an aux live while something under them does
    [Fact]
    public void An_emptied_node_goes_up_the_tree()
    {
        var aux = new JsonMenu() { Title = "Misc", Id = "misc", Items = [Leaf("/catalog/unit"), Leaf("/catalog/country")] };
        var menu = JsonMenuRoot.Transform([
            Node("Closed", Node("Nothing", Leaf("/catalog/contract")), Node("Empty")),
            Node("Open", Node("Group", aux))
        ], _grants, _roles)!;

        Assert.Equal(["Open"], menu.Select(m => m.Title));
        Assert.Equal(["/catalog/unit"], menu[0].Items![0].Items![0].Items!.Select(i => i.Url));
    }

    [Fact]
    public void A_spacer_stays_and_does_not_keep_a_node_alive()
    {
        var spacer = new JsonMenu() { Title = String.Empty, Grow = true };
        var menu = JsonMenuRoot.Transform([Node("Agents", Node("G", Leaf("/catalog/agent"))), spacer, Node("Only", spacer)], _grants, _roles)!;

        Assert.Equal(["Agents", String.Empty], menu.Select(m => m.Title));
        Assert.True(menu[1].Grow);
    }

    [Fact]
    public void The_url_is_matched_ignoring_case()
    {
        Assert.Single(JsonMenuRoot.Transform([Leaf("/Catalog/Agent")], _grants, _roles)!);
    }

    // a catalog everyone may read does not make the accounting section the storekeeper's
    [Fact]
    public void A_section_of_other_roles_is_gone_whatever_is_in_it()
    {
        var accounting = Node("Accounting", Node("Catalogs", Leaf("/catalog/agent"))) with { Roles = ["Accountant"] };
        var stock = Node("Stock", Node("Docs", Leaf("/document/waybillin"))) with { Roles = ["Storekeeper", "Accountant"] };

        Assert.Equal(["Stock"], JsonMenuRoot.Transform([accounting, stock], _grants, _roles)!.Select(m => m.Title));
    }

    [Fact]
    public void A_role_of_the_section_still_needs_an_entry()
    {
        var stock = Node("Stock", Node("Docs", Leaf("/catalog/contract"))) with { Roles = ["Storekeeper"] };

        Assert.Empty(JsonMenuRoot.Transform([stock], _grants, _roles)!);
    }

    // a key off its level is not read, as every key of menu.json
    [Fact]
    public void Roles_below_a_section_are_not_read()
    {
        var group = Node("Docs", Leaf("/document/waybillin")) with { Roles = ["Accountant"] };

        Assert.Single(JsonMenuRoot.Transform([Node("Stock", group)], _grants, _roles)!);
    }
}
