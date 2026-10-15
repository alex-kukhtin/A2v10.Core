// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal class ChessboardReportPage(ReportMetadata report, ReportGrouping grouping) : LedgerReportPage(report, grouping)
{
    /* The layout of the hand-written chessboard: code, name over two columns, a column per
     * corresponding account, the row total on a yellow column; the column totals come from the
     * model ($CrossTotals of the rows), the grand total from the total row.
     */
    public override UIElement CreatePage()
    {
        static Bind Sum(String path) => new BindSum(path);

        return new Page()
        {
            Title = _report?.ItemLabel.Localize() ?? "@[Chessboard]",
            CssClass = "report-page",
            UserSelect = true,
            Background = BackgroundStyle.White,
            Toolbar = CreateToolbar(),
            Taskpad = CreateTaskpad(),
            Children = [
                new Block() {
                    CssClass = "sheet-page sheet-report",
                    Children = [
                        new Sheet()
                        {
                            GridLines = GridLinesVisibility.Both,
                            Wrap = WrapMode.NoWrap,
                            Width = Length.FromString("1rem"),
                            Bindings = b => {
                                b.SetBinding(nameof(Sheet.If), new Bind("Filter.Run"));
                                b.SetBinding(nameof(Sheet.Stale), new Bind("Root.$AlertVisible"));
                            },
                            Columns = [
                                new SheetColumn() { Fit = true },
                                new SheetColumn() { Width = Length.FromString("3rem") },
                                new SheetColumn(),
                                new SheetColumnGroup() {
                                    Columns = { new SheetColumn() { Width = Length.FromString("Auto") } },
                                    Bindings = b => b.SetBinding(nameof(SheetColumnGroup.ItemsSource), new Bind("RepData.Cross"))
                                },
                                new SheetColumn() { Fit = true, Background = ColumnBackgroundStyle.Yellow }
                            ],
                            Header = [..CreateSheetHeader()],
                            Sections = [
                                new SheetSection() {
                                    Children = [
                                        new SheetRow() {
                                            Style = RowStyle.Header,
                                            Wrap = WrapMode.NoWrap,
                                            Cells = [
                                                new SheetCell() { ColSpan = 3, Content = "@[Chessboard.Header]" },
                                                new SheetCellGroup() {
                                                    Cells = {
                                                        new SheetCell() {
                                                            Align = TextAlign.Center,
                                                            Bindings = b => b.SetBinding(nameof(SheetCell.Content), new Bind("Key"))
                                                        }
                                                    },
                                                    Bindings = b => b.SetBinding(nameof(SheetCellGroup.ItemsSource), new Bind("RepData.Cross"))
                                                },
                                                new SheetCell() { Content = "@[Total]" }
                                            ]
                                        }
                                    ]
                                },
                                new SheetSection() {
                                    Children = [
                                        new SheetRow() {
                                            Style = RowStyle.Total,
                                            Align = TextAlign.Right,
                                            Wrap = WrapMode.NoWrap,
                                            Cells = [
                                                new SheetCell() { Align = TextAlign.Left, ColSpan = 3, Content = "@[Total]" },
                                                new SheetCellGroup() {
                                                    Cells = {
                                                        new SheetCell() {
                                                            Align = TextAlign.Right,
                                                            Bindings = b => b.SetBinding(nameof(SheetCell.Content), Sum("Sum"))
                                                        }
                                                    },
                                                    Bindings = b => b.SetBinding(nameof(SheetCellGroup.ItemsSource), new Bind("RepData.Items.$CrossTotals"))
                                                },
                                                new SheetCell() {
                                                    Bindings = b => b.SetBinding(nameof(SheetCell.Content), Sum("RepData.Sum"))
                                                }
                                            ]
                                        }
                                    ]
                                },
                                new SheetSection() {
                                    Bindings = b => b.SetBinding(nameof(SheetSection.ItemsSource), new Bind("RepData.Items")),
                                    Children = [
                                        new SheetRow() {
                                            Wrap = WrapMode.NoWrap,
                                            Cells = [
                                                new SheetCell() {
                                                    Align = TextAlign.Center,
                                                    Bindings = b => b.SetBinding(nameof(SheetCell.Content), new Bind("Id"))
                                                },
                                                new SheetCell() {
                                                    ColSpan = 2,
                                                    Bindings = b => b.SetBinding(nameof(SheetCell.Content), new Bind("Name"))
                                                },
                                                new SheetCellGroup() {
                                                    Cells = {
                                                        new SheetCell() {
                                                            Align = TextAlign.Right,
                                                            Bindings = b => b.SetBinding(nameof(SheetCell.Content), Sum("Sum"))
                                                        }
                                                    },
                                                    Bindings = b => b.SetBinding(nameof(SheetCellGroup.ItemsSource), new Bind("Cross"))
                                                },
                                                new SheetCell() {
                                                    Align = TextAlign.Right,
                                                    Bold = true,
                                                    Bindings = b => b.SetBinding(nameof(SheetCell.Content), Sum("Sum"))
                                                }
                                            ]
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                },
                CreateNonRunPanel()
            ]
        };
    }
}
