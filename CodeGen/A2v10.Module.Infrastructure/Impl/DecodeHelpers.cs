// Copyright © 2022-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.IO.Compression;
using System.IO;
using System.Text;

namespace A2v10.Module.Infrastructure.Impl;

public static class DecodeHelpers
{
	/*
	public static ExpandoObject Add(this ExpandoObject obj, String name, Object value)
	{
		if (obj is not IDictionary<String, Object> d)
			return obj;
		d.Add(name, value);
		return obj;
	}
	*/

	public static String Decode(byte[] bytes)
	{
		using var source = new MemoryStream(bytes);
		using var target = new MemoryStream();
		using (var ds = new DeflateStream(source, CompressionMode.Decompress))
		{
			ds.CopyTo(target);
		}
		return Encoding.UTF8.GetString(target.ToArray());
	}

	public static Stream DecodeStream(byte[] bytes)
	{
		using var source = new MemoryStream(bytes);
		var target = new MemoryStream();
		using (var ds = new DeflateStream(source, CompressionMode.Decompress))
		{
			ds.CopyTo(target);
		}
		target.Seek(0, SeekOrigin.Begin);
		return target;
	}
}
