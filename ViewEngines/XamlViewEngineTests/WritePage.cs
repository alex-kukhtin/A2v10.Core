
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

        int z = 55;
    }

}