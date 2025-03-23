// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using A2v10.Xaml;
using System;

namespace A2v10.Metadata;

public enum FormItemIs {
    Unknown,
    Page, 
    Dialog,
    Grid,
    Pager,
    DataGrid,
    DataGridColumn,
    Toolbar,
    TabBar,
    Taskpad,
    Button,
    TextBox,
    Selector,
    DatePicker,
    PeriodPicker,
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
    Unapply
}

public record FormItem
{
    public FormItemIs Is { get; init; } = default!;
    public String Label { get; init; } = default!;
    public String Data { get; init; } = default!;
    public Int32 row { get; init; }
    public Int32 col { get; init; }
    public Int32 rowSpan { get; init; }
    public Int32 colSpan { get; init; }
    public FormItem[]? Items { get; init; }

    // special properties
    public String? Rows { get; init; }
    public String? Columns { get; init; }
    public String? Height { get; init; }
    public FormCommand Command { get; init; }
    public String? CommandParameter { get; init; }
}

public record Form : FormItem
{
    public String Title { get; init; } = String.Empty;  
    public Boolean UseCollectionView { get; init; }
}

public record FormMetadata(RootContainer Page, String Template)
{
}
