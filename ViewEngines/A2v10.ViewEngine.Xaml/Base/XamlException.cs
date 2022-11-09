// Copyright © 2015-2017 Alex Kukhtin. All rights reserved.

namespace A2v10.Xaml
{
	[Serializable]
	public class XamlException : Exception
	{
		public XamlException(String msg)
			: base(msg)
		{
		}
	}
}
