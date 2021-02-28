using A2v10.Infrastructure;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Services
{
	public class PlatformUrl : IPlatformUrl
	{
		private readonly String[] _parts;

		public PlatformUrl(String url)
		{
			url = NormalizePath(url);
			//if (url.Contains('?'))
			// Parse Query and make parts

			_parts = url.Split('/');

			Kind = _parts[0] switch
			{
				"_page" => UrlKind.Page,
				"_dialog" => UrlKind.Dialog,
				_ => UrlKind.Undefined
			};
			Parse();
		}

		public String LocalPath { get; private set; }

		public UrlKind Kind { get; private set; }

		public String Action { get; private set; }

		public ExpandoObject Query { get; private set; }

		static String NormalizePath(String path)
		{
			path = path.ToLowerInvariant().Replace('\\', '/');
			if (path.StartsWith('/'))
				path = path[1..];
			return path;
		}

		void Parse()
		{
			Int32 len = _parts.Length;
			var id = _parts[len - 1];
			Action = _parts[len - 2];
			var pathArr = new ArraySegment<String>(_parts, 1, len - 3);
			LocalPath = String.Join("/", pathArr);

			//TODO: parse query 
		}
	}
}
