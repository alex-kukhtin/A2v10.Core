// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Dynamic;

using A2v10.Infrastructure;
using A2v10.Services;

namespace TestServices;

/* Who may do what is checked once, in DataService.CheckAccess, by every entry and before the fork
 * 'classic | metadata'. The database and the runtime builder are null here: an entry that passes
 * the check reaches one of them and fails there - any failure but DataServiceException reads as passed.
 */
[TestClass]
[TestCategory("Access")]
public class DataServiceAccessTests
{
	private sealed class User(IEnumerable<String> roles, Boolean readOnly, Boolean permitted) : ICurrentUser
	{
		private sealed class UserIdentity(IEnumerable<String> roles) : IUserIdentity
		{
			public Int64? Id => 1;
			public String? Name => "user";
			public String? PersonName => null;
			public Int32? Tenant => null;
			public String? Segment => null;
			public Boolean IsAdmin => false;
			public Boolean IsTenantAdmin => false;
			public String? Theme => null;
			public void SetInitialTenantId(Int32 tenant) { }
			public IEnumerable<String>? Roles => roles;
		}
		private sealed class UserState(Boolean readOnly) : IUserState
		{
			public Int64? Company => null;
			public Boolean IsReadOnly => readOnly;
			public IEnumerable<Guid> Modules => [];
		}
		public IUserIdentity Identity { get; } = new UserIdentity(roles);
		public IUserState State { get; } = new UserState(readOnly);
		public IUserLocale Locale => throw new NotSupportedException();
		public void SetCompanyId(Int64 id) { }
		public void SetInitialTenantId(Int32 tenantId) { }
		public void SetUserState(Boolean admin, Boolean readOnly, String? permissions) { }
		public void AddModules(IEnumerable<Guid> modules) { }
		public ExpandoObject DefaultParams() => [];
		public Boolean IsPermissionEnabled(String key, PermissionFlag flag) => permitted;
	}

	// the same dispatch as ModelJsonReader, over a model.json built in code
	private sealed class Reader(Func<ModelJson> file) : IModelJsonReader
	{
		ModelJson Load(IPlatformUrl url)
		{
			var rm = file();
			rm.OnEndInit(url);
			return rm;
		}
		public Task<IModelView?> TryGetViewAsync(IPlatformUrl url) => throw new NotSupportedException();
		public Task<IModelView> GetViewAsync(IPlatformUrl url) => Task.FromResult<IModelView>(Load(url).GetAction(url.Action));
		public Task<IModelBlob?> GetBlobAsync(IPlatformUrl url, String? suffix = null) => Task.FromResult<IModelBlob?>(url.Kind switch
		{
			UrlKind.Image => Load(url).GetBlob(url.Action, suffix),
			UrlKind.File => Load(url).GetFile(url.Action),
			_ => null
		});
		public Task<IModelCommand> GetCommandAsync(IPlatformUrl url, String command) => Task.FromResult<IModelCommand>(Load(url).GetCommand(command));
		public Task<IModelReport> GetReportAsync(IPlatformUrl url) => throw new NotSupportedException();
	}

	private static ModelJson Classic(Dictionary<String, PermissionBits>? permissions = null) => new()
	{
		Schema = "cat",
		Model = "Agent",
		Roles = ["Manager"],
		Actions = new(StringComparer.OrdinalIgnoreCase)
		{
			["index"] = new ModelJsonView() { Index = true, Export = new ModelJsonExport(), Permissions = permissions },
			["edit"] = new ModelJsonView() { Permissions = permissions },
		},
		Files = new(StringComparer.OrdinalIgnoreCase) { ["photo"] = new ModelJsonBlob() },
		Commands = new(StringComparer.OrdinalIgnoreCase) { ["run"] = new ModelJsonCommand() { Type = ModelCommandType.sql, Procedure = "Run" } },
	};

	private static ModelJson Meta() => new()
	{
		Model = IModelBase.MetaModel,
		Roles = ["Manager"],
	};

	private static DataService Service(Func<ModelJson> file, User user)
		=> new(null!, new Reader(file), null!, user, null!, null!, null!, null!, null!, null!);

	private static readonly Action<ExpandoObject> NoParams = _ => { };

	// every entry, by what it does
	private static readonly (String Name, Func<DataService, Task> Call)[] Reads =
	[
		("Load", s => s.LoadAsync("_page/catalog/agent/edit/1", NoParams)),
		("Expand", s => s.ExpandAsync("_page/catalog/agent/index/0", 1, NoParams)),
		("LoadLazy", s => s.LoadLazyAsync("_page/catalog/agent/edit/1", 1, "rows", NoParams)),
		("Export", s => s.ExportAsync("catalog/agent/index/0", NoParams)),
		("LoadBlob", s => s.LoadBlobAsync(UrlKind.Image, "catalog/agent/photo/1", NoParams)),
		("Invoke", s => s.InvokeAsync("_page/catalog/agent/index/0", "run", null, NoParams)),
	];

	private static readonly (String Name, Func<DataService, Task> Call)[] Writes =
	[
		("Save", s => s.SaveAsync("_page/catalog/agent/edit/1", [], NoParams)),
		("DbRemove", s => s.DbRemoveAsync("_page/catalog/agent/index/0", 1, null, NoParams)),
		("SaveBlob", s => s.SaveBlobAsync("catalog/agent/photo/1", _ => { }, NoParams)),
		("DeleteBlob", s => s.DeleteBlobAsync("catalog/agent/photo/1", NoParams)),
	];

	private static async Task<Exception> Thrown(Func<DataService, Task> call, DataService service)
	{
		try
		{
			await call(service);
		}
		catch (Exception ex)
		{
			return ex;
		}
		throw new AssertFailedException("the call returned without reaching the data");
	}

	private static async Task Refused(String expected, DataService service, IEnumerable<(String Name, Func<DataService, Task> Call)> entries)
	{
		foreach (var (name, call) in entries)
		{
			var ex = await Thrown(call, service);
			Assert.IsInstanceOfType<DataServiceException>(ex, $"{name}: {ex}");
			Assert.AreEqual(expected, ex.Message, name);
		}
	}

	private static async Task Passed(DataService service, IEnumerable<(String Name, Func<DataService, Task> Call)> entries)
	{
		foreach (var (name, call) in entries)
		{
			var ex = await Thrown(call, service);
			Assert.IsNotInstanceOfType<DataServiceException>(ex, $"{name}: {ex}");
		}
	}

	[TestMethod]
	public async Task A_user_without_the_role_is_refused_everywhere()
	{
		var service = Service(() => Classic(), new User([], readOnly: false, permitted: true));

		await Refused("Access denied", service, [.. Reads, .. Writes]);
	}

	[TestMethod]
	public async Task A_user_without_the_permission_is_refused_on_the_view()
	{
		var service = Service(() => Classic(new() { ["Agents"] = PermissionBits.Edit }), new User(["Manager"], readOnly: false, permitted: false));

		await Refused("UI:Access denied", service, [.. Reads.Where(r => r.Name is "Load" or "Expand" or "LoadLazy" or "Export"),
			.. Writes.Where(w => w.Name is "Save" or "DbRemove")]);
	}

	[TestMethod]
	public async Task A_read_only_user_reads_and_is_refused_every_write()
	{
		var service = Service(() => Classic(), new User(["Manager"], readOnly: true, permitted: true));

		await Refused("UI:@[Error.DataReadOnly]", service, Writes);
		await Passed(service, Reads);
	}

	// the metadata branch used to leave before the check
	[TestMethod]
	public async Task A_metadata_command_is_checked_before_the_builder()
	{
		var service = Service(Meta, new User([], readOnly: false, permitted: true));

		await Refused("Access denied", service, [("Invoke", s => s.InvokeAsync("_page/catalog/agent/index/0", "post", null, NoParams))]);
	}

	[TestMethod]
	public async Task A_metadata_remove_is_refused_to_a_read_only_user()
	{
		var service = Service(Meta, new User(["Manager"], readOnly: true, permitted: true));

		await Refused("UI:@[Error.DataReadOnly]", service, [("DbRemove", s => s.DbRemoveAsync("_page/catalog/agent/index/0", 1, null, NoParams))]);
	}
}
