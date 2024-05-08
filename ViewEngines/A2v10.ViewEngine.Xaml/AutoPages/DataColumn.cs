
// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System.Collections.Generic;

namespace A2v10.Xaml.Auto;

public class DataColumnCollection : List<DataColumn>
{
};

public class DataColumn : UIElementBase
{
	public String Data { get; set; } = String.Empty;	
	public Boolean Fit { get; set; }	
	public Boolean? Sort { get; set; }
	public ColumnRole Role { get; set; }	
	public Int32 MaxChars { get; set; }
	public Int32 LineClamp { get; set; }
	public String? Header { get; set; }	

	public override void RenderElement(RenderContext context, Action<TagBuilder>? onRender = null)
	{
		throw new NotImplementedException();
	}

	public DataGridColumn DataGridColumn => new()
	{
		Header = this.Header ?? $"@[{Data}]",
		Fit = this.Fit,
		MaxChars = this.MaxChars,
		LineClamp = this.LineClamp,
		Sort = this.Sort,
		Role = this.Role,
		Wrap = this.Fit ? WrapMode.NoWrap : this.Wrap,
		Bindings = dc => dc.SetBinding(nameof(DataGridColumn.Content), new Bind(this.Data ?? String.Empty))
	};
}
