// Copyright © 2015-2022 Alex Kukhtin. All rights reserved.

namespace A2v10.Xaml;

[ContentProperty("Content")]
public class DataGridRowDetails : XamlElement
{
	public UIElementBase? Content { get; set; }
	public RowDetailsActivate Activate { get; set; }
	public Boolean Visible { get; set; }
}

