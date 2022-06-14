// Copyright © 2022 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;

namespace A2v10.Services;

public class ModelJsonBase
{

	public String? Source { get; set; }
	public String? Schema { get; set; }
	public String? Model { get; set; }
	public Int32 CommandTimeout { get; set; }

	public ExpandoObject? Parameters { get; set; }
}

public class ModelJsonMerge : ModelJsonBase
{
}

public class ModelJsonViewBase : ModelJsonBase
{
	#region JSON
	public Boolean Index { get; set; }

	public Boolean SkipDataStack { get; set; }
	public Boolean Copy { get; set; }

	public ModelJsonMerge? Merge { get; set; }
	#endregion
}

public class ModelJsonBlob : ModelJsonViewBase
{
	public String? Key { get; set; }
	public String? Id { get; set; }
	public String? Suffix { get; set; }
	public String? Procedure { get; set; }
}

public class ModelJsonView : ModelJsonViewBase
{
	// explicit
	public String? View { get; set; }
	public String? ViewMobile { get; set; }
	public String? Template { get; set; }
	public String? CheckTypes { get; set; }

	public Boolean Indirect { get; set; }
	public String? Target { get; set; }
	public String? TargetId { get; set; }
	public List<String>? Scripts { get; set; }
	public List<String>? Styles { get; set; }
}

public class ModelJsonDialog : ModelJsonView
{
}

public class ModelJsonFile : ModelJsonBase
{
}

public enum ModelCommandType
{
	none,
	sql,
	clr,
	javascript,
	xml,
	file,
	callApi,
	sendMessage,
	processDbEvents,
	startProcess,
	resumeProcess,
	script,
	invokeTarget
}

public class ModelJsonCommand : ModelJsonBase
{
	public ModelCommandType Type { get; set; }
	public String? Procedure { get; set; }
	public String? File { get; set; }
	public String? ClrType { get; set; }
	public Boolean Async { get; set; }
	public Boolean DebugOnly { get; set; } /*TODO: Implement me*/
	public ExpandoObject? Args { get; set; }

	public String? Target { get; set; }
}

public class ModelJsonReport : ModelJsonBase
{
	public String? Type { get; set; }
	public String? Report { get; set; }
	public String? Procedure { get; set; }
	public String? Name { get; set; }
	public String? Encoding { get; set; }
	public Boolean Validate { get; set; }

	public ExpandoObject? Variables { get; set; }
}

public class ModelJson
{

	#region JSON
	public String? Source { get; set; }
	public String? Schema { get; set; }
	public String? Model { get; set; }

	public Dictionary<String, ModelJsonView> Actions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<String, ModelJsonDialog> Dialogs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<String, ModelJsonView> Popups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<String, ModelJsonFile> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<String, ModelJsonCommand> Commands { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<String, ModelJsonReport> Reports { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	#endregion
}

