// Copyright © 2015-2024 Alex Kukhtin. All rights reserved.


namespace A2v10.Xaml;
public interface ISupportBinding
{
	BindImpl BindImpl { get; }
	Bind? GetBinding(String name);
	BindCmd? GetBindingCommand(String name);
}

