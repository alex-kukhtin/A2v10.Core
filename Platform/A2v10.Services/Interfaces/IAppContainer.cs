// Copyright © 2022 Alex Kukhtin. All rights reserved.

namespace A2v10.Services;

public interface IAppContainer
{
	ModelJson GetModelJson(String path);
	String? GetText(String path);
	Object? GetXamlObject(String path);
}
