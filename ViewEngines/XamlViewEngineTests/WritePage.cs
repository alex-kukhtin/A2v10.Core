
using A2v10.System.Xaml;

namespace XamlViewEngineTests;

[TestClass]
[TestCategory("Xaml Writer")]
public class XamlWritersTests
{
    [TestMethod]
    public void WritePage()
    {
        var sp = new XamlServiceProvider();
        var page = new Page()
        {
            Title = "PageTitle",
            Padding = Thickness.FromString("1rem"),
            Toolbar = new Toolbar(sp)
            {
                Children = [
                    new Button(),
                    new Button()
                ]
            },
            CollectionView = new CollectionView()
            {
                Bindings = b => b.SetBinding(nameof(CollectionView.ItemsSource), new Bind("Agents"))
            },
            Children = [
                new Grid(sp)
                {
                    Rows = RowDefinitions.FromString("Auto,1*"),
                    Children = [
                        new DataGrid() {
                            Attach = a => a.Add("Grid.Row", "1")
                        },
                        new Pager() {
                            Attach = a => a.Add("Grid.Row", "2")
                        }
                    ]
                },
            ]
        };

        var xw = new XamlWriter();
        var written = xw.GetXaml(page);

    }


    [TestMethod]
    public void WriteIndexPage()
    {
        var sp = new XamlServiceProvider();

        var bindCmdCreate = new BindCmd("Open")
        {
            Url = "/agents/edit",
            Argument = "new"
        }; 
        var page = new Page()
        {
            Title = "PageTitle",
            Padding = Thickness.FromString("1rem"),
            CollectionView = new CollectionView()
            {
                Bindings = b => b.SetBinding(nameof(CollectionView.ItemsSource), new Bind("Agents"))
            },
            Children = [
                new Grid(sp)
                {
                    Rows = RowDefinitions.FromString("Auto,1*,Auto"),
                    Children = [
                        new Toolbar(sp) {
                            Children = [
                                new Button() {
                                    Icon = Icon.Calc,
                                    Content = "Create",
                                    Bindings = b => b.SetBinding(nameof(Button.Command), bindCmdCreate) 
                                },

                            ],  
                        },
                        new DataGrid() {
                            Attach = a => a.Add("Grid.Row", "1")
                        },
                        new Pager() {
                            Attach = a => a.Add("Grid.Row", "2")
                        }
                    ]
                },
            ]
        };

        page.InitComplete();

        var xw = new XamlWriter();
        var written = xw.GetXaml(page);

        int x = 22;

    }
}