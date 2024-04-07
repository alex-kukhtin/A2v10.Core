// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

using Microsoft.Extensions.Options;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Web.Identity;

namespace A2v10.Platform.Web;

public record PermissionObject
{
	public String Name { get; init; } = String.Empty;
	public Int64 Id { get; init; }	
}

public class WebPermissonBug(IOptions<AppUserStoreOptions<Int64>> _userStoreOptions) : IPermissionBag
{
	private Dictionary<Int64, PermissionObject>? _dictionary = null;

	public async Task LoadPermisionBagAsync(IDbContext dbContext, String? dataSource)
	{
		if (_dictionary != null)
			return;

        var so = _userStoreOptions.Value;
		var list = await dbContext.LoadListAsync<PermissionObject>(dataSource, $"[{so.SecuritySchema}].[PemissionObjects]", null);
		Dictionary<Int64, PermissionObject>? dict = null;
		if (list != null)
			dict = list.ToDictionary(l => l.Id);
		else
			dict = [];
		_dictionary = dict;
    }

	public Dictionary<String, PermissionFlag> DecodePermissions(String? permissions)
	{
		var d = new Dictionary<String, PermissionFlag>();
		if (String.IsNullOrEmpty(permissions))
			return d;
		if (_dictionary == null)
			return d;
		var arr = permissions.Split(',');
		foreach (var str in arr)
		{
			var el = str.Split(':');
			var id = Int64.Parse(el[0]);
			var flags = (PermissionFlag) Int32.Parse(el[1], NumberStyles.HexNumber);
			if (flags == 0)
				continue;
			if (_dictionary.TryGetValue(id, out PermissionObject? po))
				d.Add(po.Name, flags);
		}
		return d;
	}
}
