// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Dynamic;
using System.Globalization;

using A2v10.Infrastructure;
using A2v10.Xaml.Report;
using A2v10.ReportEngine.Script;

namespace Test.PdfReportEngine;

/*
Правило целиком: голый путь — от текущего scope, всё остальное — обычный JS,
где this и есть scope, свободные имена от корня, Root — явный корень.
*/

internal class TestLocalizer : IReportLocalizer
{
	public CultureInfo CurrentCulture => CultureInfo.InvariantCulture;
	public String? Localize(String? content) => content;
}

[TestClass]
[TestCategory("Bind Scope")]
public class BindScope
{
	private readonly RenderContext _context;
	private readonly ExpandoObject _model;
	private readonly ExpandoObject _row;

	const String CODE = """
		function rowTotalFn() { return this.Price * this.Qty; }
		const rowTotalArrow = (row) => row.Price * row.Qty;
		const nameOf = (d) => d.No;
		""";

	public BindScope()
	{
		_model = CreateModel();
		_row = _model.Eval<List<ExpandoObject>>("Document.Rows")![0];
		_context = new RenderContext(new TestCodeProvider(), String.Empty, new TestLocalizer(), _model, CODE);
	}

	static ExpandoObject CreateModel()
	{
		var row1 = new ExpandoObject();
		row1.Set("Name", "Ten");
		row1.Set("Price", 10M);
		row1.Set("Qty", 2M);

		var row2 = new ExpandoObject();
		row2.Set("Name", "Twenty");
		row2.Set("Price", 20M);
		row2.Set("Qty", 3M);

		var doc = new ExpandoObject();
		doc.Set("No", "A-1");
		doc.Set("Date", new DateTime(2026, 9, 8));
		doc.Set("Total", 1234.5M);
		doc.Set("Rows", new List<ExpandoObject>() { row1, row2 });

		var root = new ExpandoObject();
		root.Set("Document", doc);
		root.Set("Name", "ROOT");     // затеняется свойством строки
		root.Set("thisYear", 2026);   // побочный ущерб неякорной замены "this"
		return root;
	}

	String? Resolve(String source, ExpandoObject? scope = null)
	{
		return _context.Resolve(source, scope ?? _model, DataType.String, null)?.Value;
	}

	// Тип исключения как значение: падение видно в ожидании, а не в стек-трейсе
	static String Caught(Func<String?> fn)
	{
		try
		{
			return fn() ?? "<null>";
		}
		catch (Exception ex)
		{
			return $"!{ex.GetType().Name}";
		}
	}

	[TestMethod]
	public void PathFromRoot()
	{
		Assert.AreEqual("A-1", Resolve("{Document.No}"));
		Assert.AreEqual("ROOT", Resolve("{Name}"));
	}

	[TestMethod]
	public void PathFromScope()
	{
		Assert.AreEqual("Ten", Resolve("{Name}", _row));
		Assert.AreEqual("10.00", Resolve("{Price}", _row));
	}

	[TestMethod]
	public void PathWithFormat()
	{
		Assert.AreEqual("1 234.50", Resolve("{Document.Total:#,##0.00}"));
	}

	[TestMethod]
	public void ComputedDecimalLosesCurrency()
	{
		// из JS число приходит Double, и угадывание по CLR-типу даёт Number вместо
		// Currency. Лечится объявленным DataType
		Assert.AreEqual("10.00", Resolve("{Price}", _row));
		Assert.AreEqual("20", Resolve("{(rowTotalArrow(this))}", _row));
	}

	[TestMethod]
	public void ScriptFreeNameFromRoot()
	{
		Assert.AreEqual("A-1", Resolve("{(nameOf(Document))}", _row));
	}

	[TestMethod]
	public void ScriptRootPrefixFromScope()
	{
		Assert.AreEqual("A-1", Resolve("{(nameOf(Root.Document))}", _row));
	}

	[TestMethod]
	public void CollectionIsRealArray()
	{
		// Jint отдаёт IList<ExpandoObject> массивом: итог пишется в ячейке, без функции в Code
		Assert.AreEqual("80", Resolve("{(Document.Rows.reduce((s, r) => s + r.Price * r.Qty, 0))}"));
		Assert.AreEqual("Ten, Twenty", Resolve("{(Document.Rows.map(r => r.Name).join(', '))}"));
		Assert.AreEqual("1", Resolve("{(Document.Rows.filter(r => r.Price > 15).length)}"));
		// индекс — уже не путь, поэтому значение идёт через JS и теряет Decimal
		Assert.AreEqual("10", Resolve("{Document.Rows[0].Price}"));
	}

	[TestMethod]
	public void JsOnlyMemberNeedsExpression()
	{
		// length живёт в JS, а не в данных, поэтому путём его не достать
		Assert.AreEqual(String.Empty, Resolve("{Document.Rows.length}"));
		Assert.AreEqual("2", Resolve("{(Document.Rows.length)}"));
	}

	[TestMethod]
	public void BuiltinsCrossTheBranch()
	{
		// qrCode отдаёт не строку, а тип результата — тоже ветка выражения
		Assert.AreEqual("08.09.2026", Resolve("{formatDate(Document.Date, 'dd.MM.yyyy')}"));
		var qr = _context.Resolve("{qrCode(Document.No)}", _model, DataType.String, null);
		Assert.AreEqual(ResolveResultType.QrCode, qr?.ResultType);
		Assert.AreEqual("A-1", qr?.Value);
	}

	[TestMethod]
	public void ScopeAsArgument()
	{
		Assert.AreEqual("20", Resolve("{(rowTotalArrow(this))}", _row));
		Assert.AreEqual("20", Resolve("{(rowTotalFn.call(this))}", _row));
	}

	[TestMethod]
	public void CallWithOrWithoutOuterParens()
	{
		// скобки больше не маркер, но остаются валидным выражением
		Assert.AreEqual("A-1", Resolve("{nameOf(Document)}"));
		Assert.AreEqual("A-1", Resolve("{(nameOf(Document))}"));
	}

	[TestMethod]
	public void RootPathNeedsNoParens()
	{
		// Root не проходится обходом по данным, значит выражение
		Assert.AreEqual("A-1", Resolve("{Root.Document.No}", _row));
	}

	[TestMethod]
	public void ColonSplitsFormatOnlyAfterPath()
	{
		Assert.AreEqual("1 234.50", Resolve("{Document.Total:#,##0.00}"));
		Assert.AreEqual("Ten", Resolve("{(this.Price > 15 ? 'Big' : this.Name)}", _row));
		Assert.AreEqual("Ten", Resolve("{this.Price > 15 ? 'Big' : this.Name}", _row));
	}

	[TestMethod]
	public void PathOutsideScopeIsEmpty()
	{
		// строка не знает про Document: путь молча пуст, выражение падает
		Assert.AreEqual(String.Empty, Resolve("{Document.No}", _row));
		Assert.AreEqual("!JavaScriptException", Caught(() => Resolve("{this.Document.No}", _row)));
	}

	/* ------------ ломала подстановка this ------------ */

	[TestMethod]
	public void ThisWithoutParens()
	{
		// замена включалась по наличию '(' — выходило _elem_.this.Price
		Assert.AreEqual("20", Resolve("{(this.Price * this.Qty)}", _row));
	}

	[TestMethod]
	public void IdentifierContainingThis()
	{
		// thisYear превращался в _elem_Year
		Assert.AreEqual("A-1/2026", Resolve("{(nameOf(Document) + '/' + thisYear)}", _row));
	}

	[TestMethod]
	public void LiteralContainingThis()
	{
		// замена лезла внутрь литерала: молча неверное значение, хуже падения
		Assert.AreEqual("A-1this", Resolve("{(nameOf(Document) + 'this')}", _row));
	}

	[TestMethod]
	public void RootPrefixFromAnyScope()
	{
		// было два входа с разными ответами; остался один
		Assert.AreEqual("A-1", _context.Evaluate("Document.No", _model)?.ToString());
		Assert.AreEqual("A-1", _context.Evaluate("Root.Document.No", _row)?.ToString());
		Assert.AreEqual("A-1", Resolve("{(nameOf(Root.Document))}", _row));
	}
}
