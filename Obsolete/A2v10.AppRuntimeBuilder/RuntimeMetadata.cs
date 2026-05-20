// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using A2v10.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.AppRuntimeBuilder;

public enum FieldType
{
	String,
	Id,
	Int,
	Money,
	Float,
	Date,
	DateTime,
	Boolean,
	Reference,
	Parent
}

public enum TableType
{
	Catalog,
	Document,
	Details,
	Journal
}

public record RuntimeField
{
	public String Name { get; init; } = String.Empty;
	public Int32? Length { get; init; }
	public String? Ref { get; init; }
	public FieldType Type { get; init; }
}

public enum SortDir
{
	Asc,
	Desc

}
public record SortElement
{
	public String? Order { get; init; }
	public SortDir? Dir { get; init; }
}

public enum SearchType
{
	None,
	Like,
	Exact
}

public record UiField
{
	public String Name { get; set; } = String.Empty;
	public String? Title { get; set; }
    public Boolean Sort { get; set; }
	public SearchType Search { get; set; }
	public Int32 LineClamp { get; set; }
	public Boolean Multiline { get; set; }
	public Boolean Required { get; set; }
    public Boolean Fit { get; set; }
    public String? Display { get; set; }
	public String? Computed { get; set; }
	public Boolean Filter { get; set; }
	public Boolean Total { get; set; }
	public String? Width { get; set; }
	public TextAlign Align { get; set; }
	public RuntimeField? BaseField { get; set; }
	public RuntimeTable? RefTable { get; set; }
}
public record BaseUiElement
{
	public List<UiField> Fields { get; init; } = [];
	public EndpointDescriptor? Endpoint { get; private set; }	
    public virtual void SetParent(EndpointDescriptor endpoint)
	{
		Endpoint = endpoint;
		foreach (var field in Fields)
		{
			field.BaseField = endpoint.BaseTable.FindField(field.Name);
			if (field.BaseField.Ref != null)
				field.RefTable= endpoint.GetTable(field.BaseField.Ref);
		}
	}
}
public record IndexUiElement : BaseUiElement
{
	public SortElement? Sort { get; init; } = new();

	internal String SortOrder()
	{
		return Sort?.Order ?? (Endpoint?.EndpointType() == TableType.Document ? "Date" : "Name");
	}

	internal String SortDirection()
	{
		return Sort?.Dir?.ToString().ToLowerInvariant() ??
			(Endpoint?.EndpointType() == TableType.Document ? "desc" : "asc");
	}
}

public record EditUiElement : BaseUiElement 
{ 
	public String? Name { get; init; }
	public List<EditUiElement>? Details { get; init; }

	public RuntimeTable? BaseTable { get; private set; }

	public void SetParentTable(RuntimeTable table, EndpointDescriptor endpoint)
	{
		BaseTable = table;
		foreach (var f in Fields)
		{
			f.BaseField = table.FindField(f.Name);
            if (f.BaseField.Ref != null)
                f.RefTable = endpoint.GetTable(f.BaseField.Ref);
        }
    }

	public override void SetParent(EndpointDescriptor endpoint)
	{
		base.SetParent(endpoint);
		if (Details == null)
			return;
		foreach (var detalils in Details)
		{
			if (detalils.Name == null)
				throw new InvalidOperationException("Details Name is null");
			var baseTable = endpoint.BaseTable.Details?.FirstOrDefault(x => x.Name == detalils.Name)
					?? throw new InvalidOperationException("Base table not found");
			detalils.SetParentTable(baseTable, endpoint);
		}
	}
}

public record UIDescriptor
{
	public IndexUiElement? Index { get; set; }
	public IndexUiElement? Browse { get; set; }
	public EditUiElement? Edit { get; set; }	

	public void SetParent(EndpointDescriptor endpoint)
	{
		Index?.SetParent(endpoint);
		Browse?.SetParent(endpoint);	
		Edit?.SetParent(endpoint);
	}
}

public record ApplyDescriptor
{
	public String Journal { get; init; } = String.Empty;
	public Boolean In { get; init; }
	public Boolean Out { get; init; }
	public String Source { get; init; } = String.Empty;
	public Boolean Storno { get; init; }
}

public enum EndpointEdit
{
	Auto,
	Dialog,
	Page
}
public record EndpointDescriptor
{
	public EndpointEdit Edit { get; init; }
	public String Name { get; init; } = String.Empty;
	public String Title { get; init; } = String.Empty;
	public String Table { get; init; } = String.Empty;
	public RuntimeTable BaseTable { get; set; } = new();
	public UIDescriptor UI { get; set; } = new();
	public RuntimeMetadata? Metadata { get; set; }
	public Dictionary<String, String>? Parameters { get; init; }
	public List<ApplyDescriptor> Apply { get; init; } = [];

	public void SetParent(RuntimeMetadata metadata)
	{
		Metadata = metadata;
		BaseTable = metadata.GetTable(Table);
		UI.SetParent(this);
	}
}

public record RuntimeTable
{
	public String Name {  get; init; }	= String.Empty;
	public String Schema { get; private set; } = String.Empty;
	public List<RuntimeField> Fields { get; init; } = [];
	public List<RuntimeTable>? Details { get; init; }
	public RuntimeTable DetailsParent => _parent 
			?? throw new InvalidOperationException("Details parent is null");

	internal TableType TableType;

	private RuntimeMetadata? _metadata;
	private RuntimeTable? _parent;
	internal void SetParent(RuntimeMetadata meta, TableType tableType, RuntimeTable? parent = null)
	{
		_metadata = meta;
		TableType = tableType;
		_parent = parent;	
		Schema = _parent != null ? _parent.Schema : tableType.TableTypeSchema();
		if (Details != null)
			foreach (var dt in Details)
				dt.SetParent(_metadata, TableType.Details, this);
	}

	public RuntimeTable FindTable(String name)
	{
		return _metadata?.GetTable(name)
			?? throw new InvalidOperationException("Metadata is null");
	}
}

public record RuntimeMetadata
{
	public List<RuntimeTable> Catalogs { get; init; } = [];
	public List<RuntimeTable> Documents { get; init; } = [];
	public List<RuntimeTable> Journals { get; init; } = [];
	public List<EndpointDescriptor> Endpoints { get; init; } = [];
	public RuntimeTable GetTable(String tableInfo)
	{
		var info = tableInfo.Split('.');
		if (info.Length != 2)
			throw new InvalidOperationException($"Invalid runtime model {tableInfo}");
		var tableList = info[0] switch
		{
			"Catalog" => Catalogs,
			"Document" => Documents,
			"Journal" => Journals,
			_ => throw new InvalidOperationException($"Invalid runtime model key {info[0]}")
		};
		return tableList.FirstOrDefault(x => x.Name == info[1]) ??
			throw new InvalidOperationException($"Runtime Table {tableInfo} not found");
	}


	public EndpointDescriptor GetEndpoint(String name)
	{
		EndpointDescriptor DefaultFromName(String endpointName)
		{
			// TODO: ???? 
			var t = Catalogs.FirstOrDefault(c => endpointName.Equals($"/catalog/{c.Name.Singular()}", StringComparison.OrdinalIgnoreCase));
			if (t != null)
			{
				return new EndpointDescriptor() {
					Table = $"Catalog.{t.Name}",
					BaseTable = t,
					Metadata = this
				};
			}
			throw new InvalidOperationException($"Endpoint {endpointName} not found");
		}

		if (String.IsNullOrEmpty(name))
			throw new InvalidOperationException("path is empty");
		if (!name.StartsWith('/'))
			name = $"/{name}";
		var descr = Endpoints.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) 
			?? DefaultFromName(name);
        descr.BaseTable = GetTable(descr.Table);
		return descr;
	}

	public void OnEndInit()
	{
		foreach (var table in Catalogs)
			table.SetParent(this, TableType.Catalog);
		foreach (var table in Documents)
			table.SetParent(this, TableType.Document);
		foreach (var table in Journals)
			table.SetParent(this, TableType.Journal);
		foreach (var ep in Endpoints)
			ep.SetParent(this);
	}
}
