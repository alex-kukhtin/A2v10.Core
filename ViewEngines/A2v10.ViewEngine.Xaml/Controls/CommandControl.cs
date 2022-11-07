// Copyright © 2015-2022 Alex Kukhtin. All rights reserved.


namespace A2v10.Xaml;

public abstract class CommandControl : ContentControl
{
	public Command? Command { get; set; }

	public override void MergeAttributes(TagBuilder tag, RenderContext context, MergeAttrMode mode = MergeAttrMode.All)
	{
		base.MergeAttributes(tag, context, mode);
		MergeCommandAttribute(tag, context);
	}

	internal void MergeAttributesBase(TagBuilder tag, RenderContext context, MergeAttrMode mode = MergeAttrMode.All)
	{
		// without commands
		base.MergeAttributes(tag, context, mode);
	}

	protected override Boolean SkipCheckReadOnly()
	{
		var cmdBind = GetBindingCommand(nameof(Command));
		if (cmdBind != null)
			return cmdBind.IsSkipCheckReadOnly();
		return false;
	}
}
