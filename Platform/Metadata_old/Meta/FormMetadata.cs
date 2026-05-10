// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

public enum FormItemIs 
{
    Unknown,
    // root
    Page, 
    Dialog,
    Grid,
    StackPanel,
    Popup,
    Tabs,
    Tab,
    // layouts
    Pager,
    DataGrid,
    DataGridColumn,
    TreeView,
    Toolbar,
    Table,
    TableCell,
    TabBar,
    Taskpad,
    Panel,
    Aligner,
    Separator,
    // controls
    Button,
    TextBox,
    SearchBox,
    Content, // for cells
    Static,
    Selector,
    ComboBox,
    ComboBoxBit,
    DatePicker,
    PeriodPicker,
    CheckBox,
    Label,
    Header
}

public enum FormCommand
{
    Unknown,
    Reload,
    Create, // page
    Open,   // page
    Edit,
    EditSelected, // or page
    Delete,
    DeleteSelected,
    Copy,
    Apply,
    UnApply,
    Save,
    SaveAndClose,
    Select,
    Close,
    Print,
    Append,
    Remove,
    Dialog
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

    public Boolean IsEmpty => Row == 0 && Col == 0 && RowSpan == 0 && ColSpan == 0;
}

public record FormItemCommand
{
    public FormItemCommand() { }
    public FormItemCommand(FormCommand cmd, String? arg = null, String? url = null) 
    { 
        Command = cmd;
        Argument = arg;
        Url = url;
    }
    public FormCommand Command { get; init; }
    public String? Argument { get; init; }
    public String? Url { get; init; }
    internal Boolean IsEmpty => Command == FormCommand.Unknown &&
        String.IsNullOrEmpty(Argument) && String.IsNullOrEmpty(Url);
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
    public Boolean Multiline { get; init; }
    public Int32 TabIndex { get; init; }
    public Int32 LineClamp { get; init; }
    public Boolean Fit { get; init; }   
    public Boolean NoWrap { get; init; }    
    public Boolean Required { get; init; }
    public String? ItemsSource { get; init; }
    public Boolean Highlight { get; init; }
    public Boolean Folder { get; init; }
    public Boolean Total { get; init; }
    internal Boolean IsEmpty =>
        String.IsNullOrEmpty(Rows) && String.IsNullOrEmpty(Columns)
        && String.IsNullOrEmpty(Url) && String.IsNullOrEmpty(Placeholder)
        && !ShowClear && Style == ItemStyle.Default
        && String.IsNullOrEmpty(Filters) && !Multiline && TabIndex == 0
        && LineClamp == 0 && !Fit && !NoWrap && !Required && !Highlight && !Folder && !Total
        && String.IsNullOrEmpty(ItemsSource);
}

public record FormItem
{
    public FormItem() { }

    public FormItem(FormItemIs type)
    {
        Is = type;
    }

    public FormItemIs Is { get; init; }
    public String Label { get; init; } = String.Empty;
    public String Data { get; init; } = String.Empty;
    public ItemDataType DataType { get; init; }
    public FormItem[]? Items { get; init; }
    public String? Width { get; init; }
    public String? Height { get; init; }
    public String? MinHeight { get; init; }
    public String? CssClass { get; init; }
    public String? If { get; init; }
    public FormItemGrid? Grid { get; init; }
    public FormItemCommand? Command { get; init; }
    public FormItemProps? Props { get; init; }
}

public record Form : FormItem
{
    public Boolean UseCollectionView { get; init; }
    public String Schema { get; init; } = String.Empty;
    public String Table { get; init; } = String.Empty;
    public FormItem[]? Buttons { get; init; }
    public FormItem? Taskpad { get; init; }
    public FormItem? Toolbar { get; init; }
    public EditWithMode EditWith { get; init; }
}

public record FormMetadata(Form Form, String Template)
{
}
