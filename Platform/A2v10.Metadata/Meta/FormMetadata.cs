// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Xaml;

namespace A2v10.Metadata;

public enum FormItemIs 
{
    Unknown,
    // root
    Page, 
    Dialog,
    Grid,
    Popup,
    Tabs,
    Tab,
    // layouts
    Pager,
    DataGrid,
    DataGridColumn,
    Toolbar,
    Table,
    TableCell,
    TabBar,
    Taskpad,
    Panel,
    Aligner,
    // controls
    Button,
    TextBox,
    SearchBox,
    Selector,
    DatePicker,
    PeriodPicker,
    CheckBox,
    Label,
    Header
}

public enum FormCommand
{
    Reload,
    Create, // page
    Open,   // page
    Edit,
    EditSelected, // or page
    Delete,
    Copy,
    Apply,
    Unapply,
    Save,
    SaveAndClose,
    Select,
    Close,
    Append,
    Remove
}

public enum ItemDataType
{
    _,
    String,
    Id,
    Date,
    DateTime,
    Currency,
    Number,
    Boolean
}

public record FormItemGrid
{
    public FormItemGrid() { }
    public FormItemGrid(Int32 row, Int32 column)
    {
        Row = row;
        Col = column;
    }

    public Int32 Row { get; init; }
    public Int32 Col { get; init; }
    public Int32 RowSpan { get; init; }
    public Int32 ColSpan { get; init; }
}

public record FormItemCommand
{
    public FormItemCommand() { }
    public FormItemCommand(FormCommand cmd, String? arg = null) 
    { 
        Command = cmd;
        Argument = arg;
    }
    public FormCommand Command { get; init; }
    public String? Argument { get; init; }
    public String? Url { get; init; }
}

public enum ItemStyle
{
    Default,
    Primary,
}

public record FormItemProps
{
    public String? Rows { get; init; }
    public String? Columns { get; init; }
    public String? Url { get; init; }
    public String? Placeholder { get; init; }
    public Boolean ShowClear { get; init; } 
    public ItemStyle Style { get; init; }
    public String? Filters { get; init; }
}

public record FormItem
{
    public FormItem() { }

    public FormItem(FormItemIs type)
    {
        Is = type;
    }

    public FormItemIs Is { get; init; } = default!;
    public String Label { get; init; } = default!;
    public String Data { get; init; } = default!;
    public ItemDataType DataType { get; init; }
    public FormItem[]? Items { get; init; }
    public String? Width { get; init; }
    public String? Height { get; init; }
    public FormItemGrid? Grid { get; init; }
    public FormItemCommand? Command { get; init; }
    public FormItemProps? Props { get; init; }
}

public record Form : FormItem
{
    public Boolean UseCollectionView { get; init; }
    public String Schema { get; init; } = default!;
    public String Table { get; init; } = default!;
    public FormItem[]? Buttons { get; init; }
    public FormItem? Taskpad { get; init; }
    public FormItem? Toolbar { get; init; }
}

public record FormMetadata(RootContainer Page, String Template)
{
}
