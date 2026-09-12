// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Diagnostics;
using System.Dynamic;

using A2v10.Xaml.Report;
using A2v10.ReportEngine.Script;

namespace Test.PdfReportEngine;

/*
Бюджет коробки выражений. Проверяется не тип исключения Jint — он меняется между версиями, —
а то, что важно серверу: бесконечный цикл и бесконечная рекурсия заканчиваются исключением,
а не зависшим потоком и не упавшим процессом, и бюджет считается на вызов, а не на документ.
*/

[TestClass]
[TestCategory("Script Budget")]
public class ScriptBudget
{
	private readonly RenderContext _context;
	private readonly ExpandoObject _model = new();

	const String CODE = """
		const spin = () => { while (true) {} };
		const deep = (n) => deep(n + 1);
		const work = () => { let s = 0; for (let i = 0; i < 1000; i++) s += i; return s > 0 ? 1 : 0; };
		""";

	public ScriptBudget()
	{
		_context = new RenderContext(new TestCodeProvider(), String.Empty, new TestLocalizer(), _model, CODE);
	}

	String? Resolve(String source)
	{
		return _context.Resolve(source, _model, DataType.String, null)?.Value;
	}

	[TestMethod]
	public void InfiniteLoopIsStopped()
	{
		// счётчик, а не таймер: остановка за доли секунды, а не за TIMEOUT
		var watch = Stopwatch.StartNew();
		Assert.Throws<Exception>(() => Resolve("{spin()}"));
		Assert.IsLessThan(2000, watch.ElapsedMilliseconds);
	}

	[TestMethod]
	public void RunawayRecursionIsStopped()
	{
		// без предела глубины это StackOverflowException, который роняет процесс тестов целиком
		Assert.Throws<Exception>(() => Resolve("{deep(0)}"));
	}

	[TestMethod]
	public void BudgetIsPerCall()
	{
		// тысяча вызовов по ~2000 операторов — вдвое больше предела, если бы он был на документ
		for (var i = 0; i < 1000; i++)
			Assert.AreEqual("1", Resolve("{work()}"));
	}
}
