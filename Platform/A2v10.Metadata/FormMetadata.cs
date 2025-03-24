// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using A2v10.Xaml;
using System;

namespace A2v10.Metadata;

public enum FormItemIs 
{
    Unknown,
    // root
    Page, 
    Dialog,
    Grid,
    Popup,
    // layouts
    Pager,
    DataGrid,
    DataGridColumn,
    Toolbar,
    TabBar,
    Taskpad,
    Panel,
    Aligner,
    // controls
    Button,
    TextBox,
    Selector,
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
    Create,
    Edit,
    Delete,
    Copy,
    Apply,
    Unapply,
    Save,
    SaveAndClose,
    Select,
    Close   
}

public enum ItemDataType
{
    Unknown,
    String,
    Date,
    DateTime,
    Currency,
    Boolean
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
    public Int32 row { get; init; }
    public Int32 col { get; init; }
    public Int32 rowSpan { get; init; }
    public Int32 colSpan { get; init; }
    public FormItem[]? Items { get; init; }

    // special properties
    public String? Rows { get; init; }
    public String? Columns { get; init; }
    public String? Width { get; init; }
    public String? Height { get; init; }
    public FormCommand Command { get; init; }
    public String? Parameter { get; init; }
    public Boolean Primary { get; init; }
}

public record Form : FormItem
{
    public Boolean UseCollectionView { get; init; }
    public FormItem[]? Buttons { get; init; }
    public FormItem? Taskpad { get; init; }
}

public record FormMetadata(RootContainer Page, String Template)
{
}
