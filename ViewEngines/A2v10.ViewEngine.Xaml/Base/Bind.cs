// Copyright © 2015-2022 Oleksandr Kukhtin. All rights reserved.

using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;

using A2v10.Infrastructure;

namespace A2v10.Xaml;

public class Bind : BindBase, ISupportInitialize
{
	public String? Path { get; set; }
	public String? Format { get; set; }
	public DataType DataType { get; set; }
	public Boolean HideZeros { get; set; }
	public String? Mask { get; set; }
	public Boolean NegativeRed { get; set; }
	public FilterCollection? Filters { get; set; }

	private Boolean _wrapped;

	public Bind()
	{

	}
	public Bind(String path)
	{
		Path = path;
	}

	public String GetPath(RenderContext context)
	{
		return context.GetNormalizedPath(Path);
	}

	public String GetTypedPath(RenderContext context, TypeCheckerTypeCode typeCode)
	{
		return context.GetTypedNormalizedPath(Path, typeCode);
	}

	public void SetWrapped()
	{
		_wrapped = true;
	}


	// for text bindings only
	public String GetPathFormat(RenderContext context)
	{
		if (Path == null)
			return context.GetEmptyPath(); // may be scoped
		String realPath = context.GetNormalizedPath(Path, _wrapped);
		var maskBind = GetBinding(nameof(Mask));
		var formatBind = GetBinding(nameof(Format));
		if (String.IsNullOrEmpty(Format) &&
			DataType == DataType.String &&
			String.IsNullOrEmpty(Mask) &&
			maskBind == null &&
			formatBind == null &&
			!HideZeros)
			return realPath;
		var opts = new List<String>();
		if (DataType != DataType.String)
			opts.Add($"dataType: '{DataType}'");

		if (formatBind != null)
			opts.Add($"format: {formatBind.GetPathFormat(context)},");
		else if (!String.IsNullOrEmpty(Format))
			opts.Add($"format: '{context.Localize(Format.ToJsString())}'");

		if (maskBind != null)
			opts.Add($"mask: {maskBind.GetPathFormat(context)}");
		else if (!String.IsNullOrEmpty(Mask))
			opts.Add($"mask: '{context.Localize(Mask.ToJsString())}'");

		if (HideZeros)
			opts.Add("hideZeros: true");
		return $"$format({realPath}, {opts.ToJsonObject()})";
	}


	private static readonly Regex _selectedRegEx = new(@"([\w\.]+)\.Selected\((\w+)\)", RegexOptions.Compiled);

	public Boolean HasFilters => Filters != null && Filters.Count > 0;

	public String FiltersJS()
	{
		if (!HasFilters)
			return String.Empty;
		var fStrings = Filters!.Select(x => $"'{x.ToString().ToLowerInvariant()}'");
		return $"[{String.Join(",", fStrings)}]";
	}


	#region ISupportInitialize
	public void BeginInit()
	{
	}

	public void EndInit()
	{
		if (Path == null)
			return;
		var match = _selectedRegEx.Match(Path);
		if (match.Groups.Count == 3)
			Path = $"{match.Groups[1].Value}.Selected('{match.Groups[2].Value}')";
	}
	#endregion
}
