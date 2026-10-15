// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal class TrialBalanceReportPage(ReportMetadata report, ReportGrouping grouping) : LedgerReportPage(report, grouping)
{
    /* The layout of the hand-written trial balance: code, name over two columns, three debit/credit
     * pairs on gray-banded columns; a flat section, one row per account, the total row above it.
     */
    public override UIElement CreatePage()
    {
        static SheetCell Sum(String path) => new()
        {
            Bindings = b => b.SetBinding(nameof(SheetCell.Content), new BindSum(path))
        };

        static SheetCell Text(String content, Int32 colSpan = 1) => new()
        {
            Content = content,
            ColSpan = colSpan
        };

        String[] pairs = ["DtStart", "CtStart", "DtSum", "CtSum", "DtEnd", "CtEnd"];

        return new Page()
        {
            Title = _report?.ItemLabel.Localize() ?? "@[TrialBalance]",
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
                            Bindings = b => {
                                b.SetBinding(nameof(Sheet.If), new Bind("Filter.Run"));
                                b.SetBinding(nameof(Sheet.Stale), new Bind("Root.$AlertVisible"));
                            },
                            Columns = [.. "Fit,Fit,Auto,Auto+A,Auto+A,Auto,Auto,Auto+A,Auto+A".Split(',').Select(c => new SheetColumn(c))],
                            Header = [..CreateSheetHeader()],
                            Sections = [
                                new SheetSection() {
                                    Children = [
                                        new SheetRow() {
                                            Style = RowStyle.Header,
                                            Wrap = WrapMode.NoWrap,
                                            Cells = [
                                                Text("@[Account]", colSpan: 3),
                                                Text("@[TrialBalance.Start]", colSpan: 2),
                                                Text("@[TrialBalance.Turnover]", colSpan: 2),
                                                Text("@[TrialBalance.End]", colSpan: 2)
                                            ]
                                        },
                                        new SheetRow() {
                                            Style = RowStyle.Header,
                                            Cells = [
                                                Text("@[Code]"), Text("@[Name]", colSpan: 2),
                                                Text("@[Debit]"), Text("@[Credit]"),
                                                Text("@[Debit]"), Text("@[Credit]"),
                                                Text("@[Debit]"), Text("@[Credit]")
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
                                                ..pairs.Select(p => Sum($"RepData.{p}"))
                                            ]
                                        }
                                    ]
                                },
                                new SheetSection() {
                                    Bindings = b => b.SetBinding(nameof(SheetSection.ItemsSource), new Bind("RepData.Items")),
                                    Children = [
                                        new SheetRow() {
                                            Align = TextAlign.Right,
                                            Wrap = WrapMode.NoWrap,
                                            Cells = [
                                                new SheetCell() {
                                                    Align = TextAlign.Center,
                                                    Bindings = b => b.SetBinding(nameof(SheetCell.Content), new Bind("Id"))
                                                },
                                                new SheetCell() {
                                                    ColSpan = 2,
                                                    Align = TextAlign.Left,
                                                    Wrap = WrapMode.Wrap,
                                                    MinWidth = Length.FromString("20rem"),
                                                    Bindings = b => {
                                                        b.SetBinding(nameof(SheetCell.Content), new Bind("Name"));
                                                        b.SetBinding(nameof(SheetCell.Tip), new Bind("Name"));
                                                    }
                                                },
                                                ..pairs.Select(p => Sum(p))
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
