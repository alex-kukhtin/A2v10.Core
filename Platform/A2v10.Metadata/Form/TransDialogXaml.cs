// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal partial class XamlBuilder
{
    /* One tab per journal this endpoint posts to, in the order 'post' names them, each showing that
     * journal's rows for this document. Built from the declaration and not from a form: there is
     * nothing here an author could say that 'post' has not already said - see SqlBuilderTrans.
     */
    internal Dialog CreateTransDialogXaml()
    {
        var journals = Declaration.PostJournals().ToList();
        if (journals.Count == 0)
            throw new InvalidOperationException($"ShowTrans: {Endpoint.Path} declares no 'post'");

        // a fresh Bind per control: a binding is owned by the element it is set on
        static Bind TabState() => new($"{Constants.Trans.Root}.{Constants.Trans.Tab}");

        DataGrid JournalGrid(TableMetadata journal) =>
            new()
            {
                FixedHeader = true,
                Height = Length.FromString("100%"),
                Bindings = b => b.SetBinding(nameof(DataGrid.ItemsSource),
                    new Bind(journal.TransName())),
                Columns = [.. IndexColumnsXaml(journal, journal.TransMembers())]
            };

        return new Dialog()
        {
            Title = "@[Transactions]",
            Width = Length.FromString("80rem"),
            Height = Length.FromString("40rem"),
            Buttons = [
                new Button()
                {
                    Content = "@[Close]",
                    Bindings = b => b.SetBinding(nameof(Button.Command),
                        new BindCmd() { Command = CommandType.Close })
                }
            ],
            Children = [
                new Grid(_xamlServiceProvider)
                {
                    Rows = RowDefinitions.FromString("Auto,1*"),
                    MinHeight = Length.FromString("0"),
                    Height = Length.FromString("100%"),
                    Children = [
                        new TabBar()
                        {
                            Bindings = b => b.SetBinding(nameof(TabBar.Value), TabState()),
                            Buttons = [.. journals.Select(j => new TabButton()
                            {
                                Content = $"@[{j.TransName()}]",
                                ActiveValue = j.TransName(),
                                Bindings = b => b.SetBinding(nameof(TabButton.Badge),
                                    new Bind($"{j.TransName()}.Count"))
                            })]
                        },
                        new Switch()
                        {
                            Bindings = b => b.SetBinding(nameof(Switch.Expression), TabState()),
                            Cases = [.. journals.Select(j => new Case()
                            {
                                Value = j.TransName(),
                                Children = [JournalGrid(j)]
                            })]
                        }
                    ]
                }
            ]
        };
    }
}
