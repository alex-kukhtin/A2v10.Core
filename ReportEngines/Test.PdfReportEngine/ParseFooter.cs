using A2v10.ReportEngine.Pdf;
using A2v10.ReportEngine.Script;
using A2v10.Xaml.Report.Spreadsheet;
using System.Globalization;

namespace Test.PdfReportEngine;

[TestClass]
[TestCategory("Parse Footer")]
public class ParseFooter
{
	[TestMethod]
	public void ResoloveSimple()
	{
		var x = PageFooter.Resolve("&(Page) of &(Pages)").ToArray();
		Assert.AreEqual(3, x.Length);
		Assert.AreEqual("&(Page)", x[0]);
		Assert.AreEqual(" of ", x[1]);
		Assert.AreEqual("&(Pages)", x[2]);


		x = PageFooter.Resolve("Стор. &(Page) з &(Pages)").ToArray();
		Assert.AreEqual(4, x.Length);
		Assert.AreEqual("Стор. ", x[0]);
		Assert.AreEqual("&(Page)", x[1]);
		Assert.AreEqual(" з ", x[2]);
		Assert.AreEqual("&(Pages)", x[3]);
	}

	[TestMethod]
	public void ParseSimple()
	{
		var ch = PageFooter.FromString("&C&P of &N");
		Assert.AreEqual("&(Page) of &(Pages)", ch?.Center);

		var x = "&L{Document.Company.Name}&C&P of &N&R&D &T";
		ch = PageFooter.FromString(x);
		Assert.AreEqual("&(Page) of &(Pages)", ch?.Center);
	}
}