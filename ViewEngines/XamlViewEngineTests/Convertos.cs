
namespace XamlViewEngineTests;

[TestClass]
[TestCategory("Xaml View Engine")]
public class Convertors
{
    [TestMethod]
    public void LengthConverter()
    {
        Assert.AreEqual("10px", Length.FromString("10").ToString());
        Assert.AreEqual("50px", Length.FromString("50px").ToString());
        Assert.AreEqual("auto", GridLength.FromString("Auto").ToString());
    }

    [TestMethod]
    public void GridLengthConverter()
    {
        Assert.AreEqual("minmax(10%,20px)", GridLength.FromString("MinMax(10%;20px)").ToString());
        Assert.AreEqual("2fr", GridLength.FromString("2*").ToString());
    }
}