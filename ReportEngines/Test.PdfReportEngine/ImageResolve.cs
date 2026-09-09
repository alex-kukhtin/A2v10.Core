// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Dynamic;
using System.Text;

using A2v10.Infrastructure;
using A2v10.Xaml.Report;
using A2v10.Xaml.Report.Spreadsheet;
using A2v10.ReportEngine.Script;
using A2v10.ReportEngine.Excel;

namespace Test.PdfReportEngine;

/*
Правило целиком: значение картинки — либо байты, либо имя файла; растр или SVG решают
первые байты. Один резолвер на все слоты, ждущие картинку.
*/

[TestClass]
[TestCategory("Image Resolve")]
public class ImageResolve
{
	const String BASE = "_reports";

	// Сигнатура PNG: важны только первые байты, декодер тут не работает
	static readonly Byte[] PNG = [0x89, (Byte)'P', (Byte)'N', (Byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
	const String SVG = "<svg xmlns='http://www.w3.org/2000/svg'></svg>";

	static Byte[] Utf8(String s) => Encoding.UTF8.GetBytes(s);
	static Byte[] Utf8Bom(String s) => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(s)];

	private readonly RenderContext _context;
	private readonly ExpandoObject _model;

	public ImageResolve()
	{
		_model = CreateModel();
		var provider = new TestCodeProvider(new()
		{
			["_reports/logo.png"] = PNG,
			["_reports/sign.svg"] = Utf8(SVG),
			["img/logo.png"] = PNG,
		});
		_context = new RenderContext(provider, BASE, new TestLocalizer(), _model, null);
	}

	static ExpandoObject CreateModel()
	{
		var company = new ExpandoObject();
		company.Set("Logo", PNG);
		company.Set("Sign", Utf8(SVG));
		company.Set("LogoFile", "logo.png");

		var root = new ExpandoObject();
		root.Set("Company", company);
		return root;
	}

	[TestMethod]
	public void BytesAreTheImageItself()
	{
		var img = _context.ResolveImage(PNG);
		Assert.IsInstanceOfType<ReportImage.Raster>(img);
		CollectionAssert.AreEqual(PNG, ((ReportImage.Raster)img!).Bytes);
	}

	[TestMethod]
	public void StringIsAFileName()
	{
		var img = _context.ResolveImage("logo.png");
		Assert.IsInstanceOfType<ReportImage.Raster>(img);
		CollectionAssert.AreEqual(PNG, ((ReportImage.Raster)img!).Bytes);
	}

	[TestMethod]
	public void NameIsRelativeToTheReportFolder()
	{
		// подпапки и .. нормализует MakePath, отчётный слой путями не занимается
		Assert.IsInstanceOfType<ReportImage.Raster>(_context.ResolveImage("../img/logo.png"));
	}

	[TestMethod]
	public void MissingFileNamesThePath()
	{
		var ex = Assert.ThrowsExactly<FileNotFoundException>(() => _context.ResolveImage("none.png"));
		StringAssert.Contains(ex.Message, "_reports/none.png");
	}

	[TestMethod]
	public void SvgBySignatureNotByExtension()
	{
		Assert.IsInstanceOfType<ReportImage.Svg>(_context.ResolveImage("sign.svg"));
		// то же содержимое под именем растра — решают байты
		Assert.IsInstanceOfType<ReportImage.Svg>(_context.ResolveImage(Utf8(SVG)));
	}

	[TestMethod]
	public void SvgSignatureSurvivesBomAndBlanks()
	{
		Assert.IsInstanceOfType<ReportImage.Svg>(_context.ResolveImage(Utf8Bom(SVG)));
		Assert.IsInstanceOfType<ReportImage.Svg>(_context.ResolveImage(Utf8("\r\n  " + SVG)));
		Assert.IsInstanceOfType<ReportImage.Svg>(_context.ResolveImage(Utf8("<?xml version='1.0'?>" + SVG)));
	}

	[TestMethod]
	public void SvgTextLosesTheBom()
	{
		// BOM внутри текста ломает разбор SVG, поэтому снимается вместе с распознаванием
		var img = _context.ResolveImage(Utf8Bom(SVG));
		Assert.AreEqual(SVG, ((ReportImage.Svg)img!).Text);
	}

	[TestMethod]
	public void EmptyValueIsNoImage()
	{
		Assert.IsNull(_context.ResolveImage(null));
		Assert.IsNull(_context.ResolveImage(String.Empty));
		Assert.IsNull(_context.ResolveImage(Array.Empty<Byte>()));
		Assert.IsNull(_context.ResolveImage(42));
	}

	/* ------------ слоты разметки ------------ */

	[TestMethod]
	public void BoundSlotKeepsBytesOutOfJs()
	{
		// через Jint Byte[] вернулся бы массивом чисел, и картинка потерялась бы молча
		var image = new A2v10.Xaml.Report.Image();
		image.BindImpl.SetBinding("Source", new Bind("Company.Logo"));
		var img = _context.ResolveImage(image, _model, "Source", image.Source);
		Assert.IsInstanceOfType<ReportImage.Raster>(img);
		CollectionAssert.AreEqual(PNG, ((ReportImage.Raster)img!).Bytes);
	}

	[TestMethod]
	public void BoundSlotMayYieldAName()
	{
		var image = new A2v10.Xaml.Report.Image();
		image.BindImpl.SetBinding("Source", new Bind("Company.LogoFile"));
		Assert.IsInstanceOfType<ReportImage.Raster>(_context.ResolveImage(image, _model, "Source", image.Source));
	}

	[TestMethod]
	public void LiteralSlotIsAName()
	{
		var image = new A2v10.Xaml.Report.Image() { FileName = "sign.svg" };
		Assert.IsInstanceOfType<ReportImage.Svg>(_context.ResolveImage(image, _model, "FileName", image.FileName));
	}

	[TestMethod]
	public void WatermarkResolvesInTheRootScope()
	{
		var page = new Page() { Watermark = "sign.svg" };
		Assert.IsInstanceOfType<ReportImage.Svg>(_context.ResolveImage(page, _model, nameof(Page.Watermark), page.Watermark));

		var empty = new Page();
		Assert.IsNull(_context.ResolveImage(empty, _model, nameof(Page.Watermark), empty.Watermark));
	}

	/* ------------ ячейка книги ------------ */

	[TestMethod]
	public void WorkbookCellTakesBytesAsImage()
	{
		// боевая фича {Company.Logo}: varbinary из модели рисуется, а не печатается
		var cell = WorkbookCell("{Company.Logo}");
		Assert.IsInstanceOfType<ReportImage.Raster>(cell.Image);
		CollectionAssert.AreEqual(PNG, ((ReportImage.Raster)cell.Image!).Bytes);
	}

	[TestMethod]
	public void WorkbookCellDrawsSvgFromVarbinary()
	{
		Assert.IsInstanceOfType<ReportImage.Svg>(WorkbookCell("{Company.Sign}").Image);
	}

	[TestMethod]
	public void WorkbookCellStringStaysText()
	{
		// имя файла в ячейку не приходит: иначе каждая текстовая ячейка стала бы именем
		var cell = WorkbookCell("{Company.LogoFile}");
		Assert.IsNull(cell.Image);
		Assert.AreEqual("logo.png", cell.Value);
	}

	private WorkbookCell WorkbookCell(String value)
	{
		var wb = new Workbook() { RowCount = 1, ColumnCount = 1 };
		wb.Cells.Add("A1", new Cell() { Value = value });
		var helper = new WorkbookHelper(wb, _context);
		return helper.CellMatrix![0, 0]!;
	}
}
