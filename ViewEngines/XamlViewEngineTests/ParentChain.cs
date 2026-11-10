// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

namespace XamlViewEngineTests;

// The reader ends only the root; each element hands SetParent to what it holds. A slot that does not
// leaves the whole subtree without Parent and OnEndInit, silently: a Button there loses btn-cb.
// A new slot holding a XAML element gets a row here.
[TestClass]
[TestCategory("Xaml View Engine")]
public class ParentChain
{
    static readonly Dictionary<String, Func<CommandBar, XamlElement>> Slots = new()
    {
        ["Card.Header"] = p => new Card { Header = p },
        ["Card.Footer"] = p => new Card { Footer = p },
        ["KanbanCard.Header"] = p => new KanbanCard { Header = p },
        ["KanbanCard.Footer"] = p => new KanbanCard { Footer = p },
        ["KanbanBoard.Header"] = p => new KanbanBoard { Header = [p] },
        ["KanbanBoard.Card"] = p => new KanbanBoard { Card = [p] },
        ["KanbanBoard.Footer"] = p => new KanbanBoard { Footer = [p] },
        ["StateCard.Header"] = p => new StateCard { Header = p },
        ["StateCard.Text"] = p => new StateCard { Text = p },
        ["StateCard.Footer"] = p => new StateCard { Footer = p },
        ["Lazy.Content"] = p => new Lazy { Content = p },
        ["TabPanel.Header"] = p => new TabPanel { Header = p },
        ["Tab.Header"] = p => new Tab { Header = p },
        ["TabButton.Content"] = p => new TabButton { Content = p },
        ["TabButton.Description"] = p => new TabButton { Description = p },
        ["Calendar.MonthEventTemplate"] = p => new Calendar { MonthEventTemplate = p },
        ["Calendar.WeekEventTemplate"] = p => new Calendar { WeekEventTemplate = p },
        ["Calendar.ButtonsTemplate"] = p => new Calendar { ButtonsTemplate = [p] },
        ["Calendar.ToolbarTemplate"] = p => new Calendar { ToolbarTemplate = [p] },
        ["Dashboard.ItemTemplate"] = p => new Dashboard { ItemTemplate = p },
        ["Dashboard.ListTemplate"] = p => new Dashboard { ListTemplate = p },
        ["Dashboard.EmptyPanel"] = p => new Dashboard { EmptyPanel = p },
        ["DataGrid.RowDetails"] = p => new DataGrid { RowDetails = new DataGridRowDetails { Content = p } },
        ["DataGrid.EmptyPanel"] = p => new DataGrid { EmptyPanel = p },
        ["List.EmptyPanel"] = p => new List { EmptyPanel = p },
        ["ListItem.Content"] = p => new ListItem { Content = p },
        ["ListItem.Header"] = p => new ListItem { Header = p },
        ["ListItem.Footer"] = p => new ListItem { Footer = p },
        ["ListItem.CommandBar"] = p => new ListItem { CommandBar = p },
        ["ListItemSimple.CommandBar"] = p => new ListItemSimple { CommandBar = p },
        ["Selector.NewPane"] = p => new Selector { NewPane = p },
        ["Dialog.HeaderMenu"] = p => new Dialog { HeaderMenu = new DropDownMenu { Children = [p] } },
        ["EUSignFrame.Content"] = p => new EUSignFrame { Content = p },
        ["PropertyGridItem.Content"] = p => new PropertyGridItem { Content = p },
        ["Hyperlink.Content"] = p => new Hyperlink { Content = p },
        ["Hyperlink.DropDown"] = p => new Hyperlink { DropDown = p },
        ["SpanSum.Content"] = p => new SpanSum { Content = p },
    };

    public static IEnumerable<Object[]> SlotNames => Slots.Keys.Select(k => new Object[] { k });

    [TestMethod]
    [DynamicData(nameof(SlotNames))]
    public void SlotPassesParentDown(String slot)
    {
        var button = new Button();
        var holder = Slots[slot](new CommandBar { Children = [button] });
        holder.InitComplete();
        Assert.IsTrue(button.IsParentCommandBar, slot);
    }

    // the same through the reader, which is what a view goes through
    [TestMethod]
    public void CommandBarInListItemFromXaml()
    {
        var list = SingleContent.Load("""
            <List xmlns="clr-namespace:A2v10.Xaml;assembly=A2v10.Xaml">
                <ListItem>
                    <CommandBar Visibility="Hover" Float="Right">
                        <Button Icon="Rename"/>
                    </CommandBar>
                </ListItem>
            </List>
            """) as List;
        Assert.IsNotNull(list);
        var item = list.Content[0] as ListItem;
        Assert.IsNotNull(item);
        var bar = item.Content as CommandBar;
        Assert.IsNotNull(bar);
        var button = bar.Children[0] as Button;
        Assert.IsNotNull(button);
        Assert.IsTrue(button.IsParentCommandBar);
    }
}
