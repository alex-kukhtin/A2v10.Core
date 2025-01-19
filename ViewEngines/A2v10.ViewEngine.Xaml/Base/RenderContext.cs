// Copyright © 2015-2025 Oleksandr Kukhtin. All rights reserved.

using System.Collections.Generic;
using System.IO;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Xaml;
public readonly struct GridRowCol(Int32? _row, Int32? _col, Int32? _rowSpan, Int32? _colSpan, AlignItem? _vAlign)
{
    public IList<StringKeyValuePair> GetGridAttributes()
	{
		var rv = new List<StringKeyValuePair>();
		String? row = null;
		String? col = null;
		if (_row != null && _row.Value != 0)
			row = _row.Value.ToString();
		if (_rowSpan != null && _rowSpan.Value != 0)
		{
			if (row == null)
				row = $"span {_rowSpan.Value}";
			else
				row += $" / span {_rowSpan.Value}";
		}
		if (row != null)
			rv.Add(new StringKeyValuePair() { Key = "grid-row", Value = row });

		if (_col != null && _col.Value != 0)
			col = _col.Value.ToString();
		if (_colSpan != null && _colSpan.Value != 0)
		{
			if (col == null)
				col = $"span {_colSpan.Value}";
			else
				col += $"/ span {_colSpan.Value}";
		}
		if (col != null)
			rv.Add(new StringKeyValuePair() { Key = "grid-column", Value = col });

		if (_vAlign != null)
		{
			String? vAlign = _vAlign.Value.AlignSelf();
			if (vAlign != null)
				rv.Add(new StringKeyValuePair() { Key = "align-self", Value = vAlign });
			if (vAlign == "stretch")
				rv.Add(new StringKeyValuePair() { Key = "overflow", Value = "auto" });
		}
		return rv;
	}
}

public sealed class GridContext : IDisposable
{
	private readonly RenderContext _renderContext;

	public GridContext(RenderContext renderContext, GridRowCol rowCol)
	{
		_renderContext = renderContext;
		_renderContext.PushRowCol(rowCol);
	}

	public void Dispose()
	{
		_renderContext.PopRowCol();
	}
}



public sealed class ScopeContext : IDisposable
{
	private readonly RenderContext _renderContext;

	public ScopeContext(RenderContext context, String scope, String? path, Func<String, String>? replace = null)
	{
		_renderContext = context;
		_renderContext.PushScope(scope, path, replace);
	}

	public void Dispose()
	{
		_renderContext.PopScope();
	}
}

internal readonly struct ScopeElem(String scope, String? path, Func<String, String>? replace)
{
	public readonly String Scope { get; } = scope;
	public readonly String? Path { get; } = path;
	public readonly Func<String, String>? Replace { get; } = replace;
}

public class RenderContext(IXamlElement _root, IRenderInfo _ri, ILocalizer _localizer, TextWriter writer)
{
	public String? RootId { get; set; }
	public String? Path { get; set; }
	public Component? RenderedComponent { get; set; }

    public TextWriter Writer { get; private set; } = writer;

    public Boolean IsDebugConfiguration { get; } = true; // TODO: ri.IsDebugConfiguration;

    private readonly Stack<GridRowCol> _stackGrid = new();
	private readonly Stack<ScopeElem> _stackScope = new();

	private readonly IDataModel? _dataModel = _ri.DataModel;
	//private readonly ITypeChecker? _typeChecker;

	readonly private String? _currentLocale = _ri.CurrentLocale;

    public Boolean IsDialog => _root is Dialog;
	public Boolean IsWizard => _root is Wizard;


	public XamlElement? FindComponent(String name)
	{
		if (_root is IRootContainer container)
			return container.FindComponent(name);
		return null;
	}

	public IDataModel? DataModel => _dataModel;

	public Boolean IsDataModelIsReadOnly
	{
		get
		{
			if (_dataModel != null)
				return _dataModel.IsReadOnly;
			return false;
		}
	}

	public Object? CalcDataModelExpression(String? expression)
	{
		if (_dataModel == null || expression == null)
			return null;
		return _dataModel.CalcExpression<Object>(expression);
	}

	public void RenderSpace()
	{
		Writer.Write(" ");
	}

	public void RenderNbSpace()
	{
		Writer.Write("&#xa;");
	}

	public GridContext GridContext(Object elem, Grid parentGrid)
	{
		var rowCol = new GridRowCol(parentGrid.GetRow(elem), parentGrid.GetCol(elem), parentGrid.GetRowSpan(elem), parentGrid.GetColSpan(elem), parentGrid.GetVAlign(elem));
		return new GridContext(this, rowCol);
	}

	public GridContext GridContext(Int32? row, Int32? col, Int32? rowSpan, Int32? colSpan, AlignItem? vAlign)
	{
		var rowCol = new GridRowCol(row, col, rowSpan, colSpan, vAlign);
		return new GridContext(this, rowCol);
	}

	internal void PushRowCol(GridRowCol rowCol)
	{
		_stackGrid.Push(rowCol);
	}

	internal void PopRowCol()
	{
		_stackGrid.Pop();
	}

	internal void PushScope(String scope, String? path, Func<String, String>? replace)
	{
		_stackScope.Push(new ScopeElem(scope, path, replace));
	}

	internal Int32 ScopeLevel => _stackScope.Count;

	internal void PopScope()
	{
		_stackScope.Pop();
	}

	public IEnumerable<StringKeyValuePair>? GetGridAttributes()
	{
		if (_stackGrid.Count == 0)
			return null;
		GridRowCol rowCol = _stackGrid.Peek();
		return rowCol.GetGridAttributes();
	}

	internal String GetNormalizedPath(String? path, Boolean isWrapped = false)
	{
		// check for invert
		path ??= String.Empty;
		if (path.StartsWith('!'))
			return "!" + GetNormalizedPathInternal(path[1..]);

		//if (_typeChecker != null)
		//_typeChecker.CheckXamlExpression(GetExpressionForChecker(path));

		return GetNormalizedPathInternal(path, isWrapped);
	}

	internal String GetTypedNormalizedPath(String? path, TypeCheckerTypeCode _1/*typeCode*/, Boolean isWrapped = false)
	{
		// check for invert
		path ??= String.Empty;
		if (path.StartsWith('!'))
			return "!" + GetNormalizedPathInternal(path[1..]);

		//if (_typeChecker != null && typeCode != TypeCheckerTypeCode.Skip)
		//_typeChecker.CheckTypedXamlExpression(GetExpressionForChecker(path), typeCode);

		return GetNormalizedPathInternal(path, isWrapped);
	}

	private String GetNormalizedPathInternal(String path, Boolean isWrapped = false)
	{
		ArgumentNullException.ThrowIfNull(path, nameof(path));

		const String rootKey = "Root.";
		if (_stackScope.Count == 0)
		{
			if (path == "Root")
				return "$data";
			return path.Replace(rootKey, "$data.");
		}
		if (path.StartsWith("Parent."))
			return path;
		if (path.StartsWith(rootKey))
			return "$data." + path[rootKey.Length..];
		ScopeElem scope = _stackScope.Peek();
		String result = scope.Scope;
		if (!String.IsNullOrEmpty(path))
		{
			if (isWrapped)
				result += $"['{path.Replace("'", "\\'")}']";
			else
				result += "." + path;
		}
		if (scope.Replace != null)
			return scope.Replace(result);
		return result;
	}

	/*
	String GetExpressionForChecker(String path)
	{
		if (_stackScope.Count == 0)
			return path;
		var parent = String.Join(".", _stackScope.Select(x => x.Path).Reverse().ToArray());
		if (String.IsNullOrEmpty(path))
			return parent;
		return $"{parent}.{path}";
	}
	*/

	internal String GetEmptyPath()
	{
		if (_stackScope.Count == 0)
			return String.Empty;
		ScopeElem scope = _stackScope.Peek();
		String result = scope.Scope;
		if (scope.Replace != null)
			return scope.Replace(result);
		return result;
	}

	public String? Localize(String? text, Boolean replaceNewLine = true)
	{
		if (_localizer == null)
			return text;
		return _localizer.Localize(_currentLocale, text, replaceNewLine);
	}

	public String? LocalizeCheckApostrophe(String? text)
	{
		if (text == null)
			return null;
		var txt = Localize(text);
		if (txt == null)
			return null;
		return txt.Replace("\\'", "'");
	}
}

