// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Microsoft.Extensions.Options;

using A2v10.Infrastructure;
using A2v10.ViewEngine.Xaml;

namespace XamlViewEngineTests;

// a second child of a single-object content property: the error the server and `a2 view validate`
// report, loaded the way they load a view (XamlPartProvider -> AppXamlReaderService)
[TestClass]
[TestCategory("Xaml View Engine")]
public class SingleContent
{
    static Object? Load(String xaml)
    {
        var provider = new XamlPartProvider(new NoFilesCodeProvider(), Options.Create(new AppOptions()));
        return provider.GetXamlPartText(xaml, "view.vxaml");
    }

    [TestMethod]
    public void TwoChildrenInColumnTakeTheHint()
    {
        // the reader's exception, not A2v10.Xaml.XamlException of the engine
        var ex = Assert.Throws<A2v10.System.Xaml.XamlException>(() => Load("""
            <DataGrid xmlns="clr-namespace:A2v10.Xaml;assembly=A2v10.Xaml">
                <DataGridColumn>
                    <TagLabel If="{Bind Row.IsNew}" Content="New"/>
                    <TagLabel If="{Bind Row.IsClosed}" Content="Closed"/>
                </DataGridColumn>
            </DataGrid>
            """));
        Assert.AreEqual("Element 'DataGridColumn': content property 'Content' takes one object; a second child at line 4, position 10. " +
            "Wrap the children in <Group>: it renders no element of its own.", ex.Message);
    }

    [TestMethod]
    public void TheHintLoads()
    {
        var grid = Load("""
            <DataGrid xmlns="clr-namespace:A2v10.Xaml;assembly=A2v10.Xaml">
                <DataGridColumn>
                    <Group>
                        <TagLabel If="{Bind Row.IsNew}" Content="New"/>
                        <TagLabel If="{Bind Row.IsClosed}" Content="Closed"/>
                    </Group>
                </DataGridColumn>
            </DataGrid>
            """) as DataGrid;
        Assert.IsNotNull(grid);
        var group = grid.Columns[0].Content as Group;
        Assert.IsNotNull(group);
        Assert.AreEqual(2, group.Children.Count);
    }

    // inline text reads no file
    sealed class NoFilesCodeProvider : IAppCodeProvider
    {
        public String AppId => throw new NotImplementedException();
        public Boolean HasLicensedModules => throw new NotImplementedException();
        public IEnumerable<Guid> LicensedModules => throw new NotImplementedException();
        public String? ModuleVersion => throw new NotImplementedException();
        public String MakePath(String path, String fileName) => throw new NotImplementedException();
        public Boolean IsFileExists(String path) => throw new NotImplementedException();
        public Stream? FileStreamRO(String path, Boolean primaryOnly = false) => throw new NotImplementedException();
        public Stream FileStreamResource(String path, Boolean primaryOnly = false) => throw new NotImplementedException();
        public IEnumerable<Stream> EnumerateFileStreamsRO(String path) => throw new NotImplementedException();
        public IEnumerable<String> EnumerateAllFiles(String path, String searchPattern) => throw new NotImplementedException();
        public IEnumerable<String> EnumerateAllFilesRecursive(String path, String searchPattern) => throw new NotImplementedException();
        public IEnumerable<String> EnumerateWatchedDirs(String path, String searchPattern) => throw new NotImplementedException();
        public String GetMainModuleFullPath(String path, String fileName) => throw new NotImplementedException();
    }
}
