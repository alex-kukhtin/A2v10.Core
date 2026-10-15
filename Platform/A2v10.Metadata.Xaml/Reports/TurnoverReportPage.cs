// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal class TurnoverReportPage(ReportMetadata report, ReportGrouping grouping) : ReportPage(report, grouping)
{
    public override UIElement CreatePage()
    {
        var dataCount = _grouping.Data.Count;
        var headerRowCount = dataCount > 1 ? 2 : 1;

        IEnumerable<SheetCell> dataCells()
        {
            yield return new SheetGroupCell();
            yield return new SheetCell()
            {
                GroupIndent = true,
                ColSpan = 2,
                Bindings = b => b.SetBinding(nameof(SheetCell.Content), new Bind("$groupName"))
            };
            foreach (var d in _grouping.Data)
                yield return d.BindSheetCell("Start");
            foreach (var d in _grouping.Data)
                yield return d.BindSheetCell("In");
            foreach (var d in _grouping.Data)
                yield return d.BindSheetCell("Out");
            foreach (var d in _grouping.Data)
                yield return d.BindSheetCell("End");
        }

        IEnumerable<SheetCell> totalCells()
        {
            yield return new SheetCell();
            yield return new SheetCell()
            {
                ColSpan = 2,
                Content = "@[Total]"
            };
            foreach (var d in _grouping.Data)
                yield return d.BindSheetCell("RepData.Start");
            foreach (var d in _grouping.Data)
                yield return d.BindSheetCell("RepData.In");
            foreach (var d in _grouping.Data)
                yield return d.BindSheetCell("RepData.Out");
            foreach (var d in _grouping.Data)
                yield return d.BindSheetCell("RepData.End");
        }

        IEnumerable<SheetCell> headerCellsRow1()
        {
            yield return new SheetCell()
            {
                RowSpan = headerRowCount,
            };
            yield return new SheetCell()
            {
                ColSpan = 2,
                RowSpan = headerRowCount,
                Content = String.Join(" / ", _grouping.Grouping.Select(g => g.LocalizeLabel()))
            };
            foreach (var d in "@[TurnOver.Start]|@[TurnOver.In]|@[TurnOver.Out]|@[TurnOver.End]".Split('|'))
            {
                yield return new SheetCell()
                {
                    ColSpan = dataCount,
                    Content = d
                };
            }
        }

        IEnumerable<SheetCell> headerCellsRow2()
        {
            foreach (var r in Enumerable.Range(1, 4))
                foreach (var d in _grouping.Data)
                    yield return new SheetCell()
                    {
                        Content = d.LocalizeLabel(),
                        Wrap = WrapMode.NoWrap
                    };
        }

        IEnumerable<SheetRow> headerRows()
        {
            yield return new SheetRow()
            {
                Style = RowStyle.Header,
                Cells = [.. headerCellsRow1()]
            };
            if (dataCount > 1)
            {
                yield return new SheetRow()
                {
                    Style = RowStyle.Header,
                    Cells = [..headerCellsRow2()]
                };
            }
        }

        SheetSection TotalSection()
        {
            return new SheetSection()
            {
                Children = [
                    new SheetRow()
                    {
                        Style = RowStyle.Total,
                        Cells = [..totalCells()]
                    }
                ]
            };
        }

        var page = new Page()
        {
            Title = _report?.ItemLabel.Localize() ?? "@[Report]",
            CssClass = "report-page",
            UserSelect = true,
            Background = BackgroundStyle.White,
            Toolbar = CreateToolbar(),
            Taskpad = CreateTaskpad(),
            Children = [
                new Block() {
                    CssClass = "sheet-page sheet-report",
                    Bindings = b => b.SetBinding(nameof(Block.CssClass), new Bind("Root.$SheetPageClass")),
                    Children = [
                        new Sheet()
                        {
                            GridLines = GridLinesVisibility.Both,
                            Bindings = b => b.SetBinding(nameof(Sheet.If), new Bind("Filter.Run")),
                            Sections = [
                                new SheetSection() {
                                    Children = [..headerRows()]
                                },
                                TotalSection(),
                                new SheetTreeSection() {
                                    Bindings = b => b.SetBinding(nameof(SheetSection.ItemsSource), new Bind("RepData.Items")),
                                    Children = [
                                        new SheetRow() {
                                            Cells = [..dataCells()]
                                        }
                                    ]
                                }
                            ],
                            Header = [..CreateSheetHeader()],
                            Columns = [
                                new SheetColumn() {
                                    Width = Length.FromString("16px"),
                                },
                                new SheetColumn() {
                                    Width = Length.FromString("4rem"),
                                },
                                new SheetColumn() {
                                    Width = Length.FromString("100%"),
                                    MinWidth = Length.FromString("Auto")
                                }
                            ]
                        }
                    ]
                },
                CreateNonRunPanel()
            ]
        };

        return page;
    }
}
