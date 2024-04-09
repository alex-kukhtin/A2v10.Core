// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Runtime.Remoting;
using A2v10.Infrastructure;

namespace A2v10.AppRuntimeBuilder;

internal class TableTypeBuilder
{
	public static DataTable BuildDataTable(RuntimeTable table, ExpandoObject? data)
	{
		var dtable = new DataTable();

		List<DataColumn> columns = [];
		foreach (var f in table.RealFields()) {
			var c = new DataColumn(f.Name, f.ValueType());
			if (f.Length.HasValue)
				c.MaxLength = f.Length.Value;
			columns.Add(c);
		}
		for (int i = 0; i < columns.Count; i++)
			dtable.Columns.Add(columns[i]);

		if (data == null)
			return dtable;

		var r = dtable.NewRow();
		dtable.Rows.Add(r);

		for (int i = 0; i < columns.Count; i++)
		{
			var col = columns[i];
			if (data.HasProperty(col.ColumnName))
			{
				var obj = data.Get<Object>(col.ColumnName);
				if (obj is ExpandoObject exp)
					obj = exp.Get<Object>("Id");
				r[i] = obj;
			}
		}
		return dtable;
	}
}
