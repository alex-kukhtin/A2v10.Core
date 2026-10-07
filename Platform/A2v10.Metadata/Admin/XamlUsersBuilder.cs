// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using A2v10.System.Xaml;
using A2v10.Xaml;

namespace A2v10.Metadata;

internal class XamlUsersBuilder(String _path)
{
    private readonly IServiceProvider _xamlServiceProvider = new XamlServiceProvider();

    private const String Users = "Users";

    private static Button CancelButton() => new()
    {
        Content = "@[Cancel]",
        Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd(CommandType.Close))
    };

    private static TextBox Text(String label, String path, Boolean password = false, Int32 tabIndex = 0) => new()
    {
        Label = label,
        Password = password,
        TabIndex = tabIndex,
        Bindings = b => b.SetBinding(nameof(TextBox.Value), new Bind(path))
    };

    // shown, not edited: the login is the account's, and Identity has no rename here
    private static TextBox Login() => new()
    {
        Label = "@[Admin.Login]",
        Disabled = true,
        TabIndex = -1,
        Bindings = b => b.SetBinding(nameof(TextBox.Value), new Bind("User.UserName"))
    };

    // a dialog of the user the list has selected
    private BindCmd DialogSelected(String action)
    {
        var cmd = new BindCmd(CommandType.Dialog) { Action = DialogAction.ShowSelected, Url = $"{_path}/{action}" };
        cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(Users));
        return cmd;
    }

    // a template command on the user the list has selected
    private static BindCmd ExecuteSelected(String command)
    {
        var cmd = new BindCmd(CommandType.ExecuteSelected) { CommandName = command };
        cmd.BindImpl.SetBinding(nameof(BindCmd.Argument), new Bind(Users));
        return cmd;
    }

    private static Button ToolbarButton(Icon icon, String tip, BindCmd command) => new()
    {
        Icon = icon,
        Tip = tip,
        Bindings = b => b.SetBinding(nameof(Button.Command), command)
    };

    /* The whole list, sorted on the client: an organisation has users by the dozen, not by the
     * thousand - no pager, no search, no CollectionView.
     */
    public Page IndexPage()
    {
        static DataGridColumn Column(String header, String path, DataType dataType = DataType.String, ColumnRole role = ColumnRole.Default) => new()
        {
            Header = header,
            SortProperty = path,
            Role = role,
            Bindings = b => b.SetBinding(nameof(DataGridColumn.Content), new Bind(path) { DataType = dataType })
        };

        static SpanIcon Mark(Icon icon, String visible) => new()
        {
            Icon = icon,
            Bindings = b => b.SetBinding(nameof(SpanIcon.If), new Bind(visible))
        };

        var grid = new DataGrid()
        {
            FixedHeader = true,
            Sort = true,
            Bindings = b =>
            {
                b.SetBinding(nameof(DataGrid.ItemsSource), new Bind(Users));
                b.SetBinding(nameof(DataGrid.DoubleClick), DialogSelected(UserAdminEndpointMetadata.EditAction));
            },
            Columns = [
                new DataGridColumn()
                {
                    Fit = true,
                    Sort = false,
                    Content = new Group()
                    {
                        Children = [Mark(Icon.Lock, "IsBlocked"), Mark(Icon.WarningOutlineYellow, "!EmailConfirmed")]
                    }
                },
                Column("@[Admin.Login]", "UserName"),
                Column("@[Admin.PersonName]", "PersonName"),
                Column("@[Roles]", "Roles"),
                Column("@[Admin.LastLogin]", "LastLoginDate", DataType.DateTime, ColumnRole.Date),
                Column("@[Memo]", "Memo")
            ]
        };

        return new Page()
        {
            Children = [
                new Grid(_xamlServiceProvider)
                {
                    Rows = RowDefinitions.FromString("Auto,1*"),
                    Height = Length.FromString("100%"),
                    Children = [
                        new Toolbar(_xamlServiceProvider)
                        {
                            Children = [
                                new Button()
                                {
                                    Icon = Icon.Add,
                                    Content = "@[Create]",
                                    Bindings = b => b.SetBinding(nameof(Button.Command),
                                        new BindCmd(CommandType.Execute) { CommandName = UsersTemplateBuilder.CreateUser })
                                },
                                ToolbarButton(Icon.Edit, "@[Edit]", DialogSelected(UserAdminEndpointMetadata.EditAction)),
                                ToolbarButton(Icon.Clear, "@[Delete]", ExecuteSelected(UsersTemplateBuilder.DeleteUser)),
                                new Separator(),
                                new Button()
                                {
                                    Content = "@[SetPassword]",
                                    Bindings = b => b.SetBinding(nameof(Button.Command), DialogSelected(UserAdminEndpointMetadata.PasswordAction))
                                },
                                ToolbarButton(Icon.Lock, "@[Admin.Block]", ExecuteSelected(UsersTemplateBuilder.Block)),
                                ToolbarButton(Icon.Unlock, "@[Admin.Unblock]", ExecuteSelected(UsersTemplateBuilder.Unblock)),
                                new Separator(),
                                FormButtons.Reload
                            ]
                        },
                        grid
                    ]
                }
            ]
        };
    }

    /* The card: the profile, the roles and the boundary - a tab per dimension of app.json 'boundary'.
     * Nothing that acts on the account: the password and the block are commands of the list.
     */
    public Dialog EditDialog(IReadOnlyList<BoundaryDimension> boundary)
    {
        // a fresh Bind per control: a binding is owned by the element it is set on
        static Bind TabState() => new("Root.$$Tab");
        static Bind NotAdmin() => new($"!Root.{UsersTemplateBuilder.IsAdminProperty}");

        // a row per value, ticked when the user holds it - the roles and every dimension alike
        static StackPanel Checklist(String itemsSource) => new()
        {
            Gap = GapSize.FromString("0.5rem"),
            Margin = Thickness.FromString("0.5rem,0, 0, 0"),
            Bindings = b => b.SetBinding(nameof(Table.ItemsSource), new Bind(itemsSource)),
            Children = [ 
                new CheckBox()
                {
                    Bindings = b =>
                    {
                        b.SetBinding(nameof(CheckBox.Value), new Bind("Checked"));
                        b.SetBinding(nameof(CheckBox.Label), new Bind("Name"));
                    }
                }
            ]
        };

        /* Nothing ticked is the whole network - said on the tab, since the list alone reads as "nowhere".
         * Hidden for an admin, as the tab button is: the boundary does not hold for him.
         */
        Block Dimension(Int32 index) => new()
        {
            Scroll = true,
            Height = Length.FromString("10rem"),
            Bindings = b => b.SetBinding(nameof(Group.If), NotAdmin()),
            Children = [
                new Span() { Block = true, Content = "@[Admin.BoundaryAll]" },
                Checklist($"Root.{UsersTemplateBuilder.DimensionProperty(index)}")
            ]
        };

        return new Dialog()
        {
            Title = "@[User]",
            Width = Length.FromString("40rem"),
            Buttons = [
                new Button()
                {
                    Content = "@[SaveAndClose]",
                    Style = ButtonStyle.Primary,
                    Bindings = b => b.SetBinding(nameof(Button.Command), new BindCmd(CommandType.SaveAndClose) { ValidRequired = true })
                },
                CancelButton()
            ],
            Children = [
                new Grid(_xamlServiceProvider)
                {
                    Children = [
                        Login(),
                        Text("@[Admin.PersonName]", "User.PersonName", tabIndex: 1),
                        Text("@[Phone]", "User.PhoneNumber"),
                        new TabBar()
                        {
                            Bindings = b => b.SetBinding(nameof(TabBar.Value), TabState()),
                            Buttons = [
                                new TabButton() { Content = "@[Roles]", ActiveValue = UsersTemplateBuilder.RolesTab },
                                ..boundary.Select(d => new TabButton()
                                {
                                    Content = $"@[{d.Catalog.CollectionName}]",
                                    ActiveValue = d.Path,
                                    Bindings = b => b.SetBinding(nameof(TabButton.If), NotAdmin())
                                })
                            ]
                        },
                        new Switch()
                        {
                            Bindings = b => b.SetBinding(nameof(Switch.Expression), TabState()),
                            Cases = [
                                new Case() { Value = UsersTemplateBuilder.RolesTab, 
                                    Children = [
                                        new Block 
                                        {
                                            Scroll = true,
                                            Height = Length.FromString("10rem"),
                                            Children = [Checklist(UsersTemplateBuilder.RolesTab)]
                                        }
                                     ]
                                },
                                ..boundary.Select((d, i) => new Case() { Value = d.Path, Children = [Dimension(i)] })
                            ]
                        },
                        new TextBox()
                        {
                            Label = "@[Memo]",
                            Multiline = true,
                            Rows = 3,
                            Bindings = b => b.SetBinding(nameof(TextBox.Value), new Bind("User.Memo"))
                        }
                    ]
                }
            ]
        };
    }

    public Dialog CreateDialog() => new()
    {
        Title = "@[Admin.NewUser]",
        Width = Length.FromString("30rem"),
        Overflow = true,
        Buttons = [
            new Button()
            {
                Content = "@[Create]",
                Style = ButtonStyle.Primary,
                Bindings = b => b.SetBinding(nameof(Button.Command),
                    new BindCmd(CommandType.Execute) { CommandName = UsersTemplateBuilder.Create, ValidRequired = true })
            },
            CancelButton()
        ],
        Children = [
            new Grid(_xamlServiceProvider)
            {
                Children = [
                    Text("@[Admin.Login]", "User.UserName", tabIndex: 1),
                    Text("@[Admin.PersonName]", "User.PersonName"),
                    Text("@[Password]", "User.Password", password: true),
                    Text("@[PasswordAgain]", "User.Confirm", password: true)
                ]
            }
        ]
    };

    public Dialog PasswordDialog() => new()
    {
        Title = "@[SetPassword]",
        Width = Length.FromString("30rem"),
        Buttons = [
            new Button()
            {
                Content = "@[SetPassword]",
                Style = ButtonStyle.Primary,
                Bindings = b => b.SetBinding(nameof(Button.Command),
                    new BindCmd(CommandType.Execute) { CommandName = UsersTemplateBuilder.SetPassword, ValidRequired = true })
            },
            CancelButton()
        ],
        Children = [
            new Grid(_xamlServiceProvider)
            {
                Children = [
                    Login(),
                    Text("@[Password]", "User.Password", password: true, tabIndex: 1),
                    Text("@[PasswordAgain]", "User.Confirm", password: true)
                ]
            }
        ]
    };
}
