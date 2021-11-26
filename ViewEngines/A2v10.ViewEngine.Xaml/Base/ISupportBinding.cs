// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.


namespace A2v10.Xaml;
internal interface ISupportBinding
{
	BindImpl BindImpl { get; }
	Bind? GetBinding(String name);
	BindCmd? GetBindingCommand(String name);
}

