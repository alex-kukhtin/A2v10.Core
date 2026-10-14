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

	private static DataService Service(Func<ModelJson> file, TestUser user)
		=> new(null!, new TestModelReader(file), null!, user, null!, null!, null!, null!, null!, null!);

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
		var service = Service(() => Classic(), new TestUser([], readOnly: false, permitted: true));

		await Refused("Access denied", service, [.. Reads, .. Writes]);
	}

	[TestMethod]
	public async Task A_user_without_the_permission_is_refused_on_the_view()
	{
		var service = Service(() => Classic(new() { ["Agents"] = PermissionBits.Edit }), new TestUser(["Manager"], readOnly: false, permitted: false));

		await Refused("UI:Access denied", service, [.. Reads.Where(r => r.Name is "Load" or "Expand" or "LoadLazy" or "Export"),
			.. Writes.Where(w => w.Name is "Save" or "DbRemove")]);
	}

	[TestMethod]
	public async Task A_read_only_user_reads_and_is_refused_every_write()
	{
		var service = Service(() => Classic(), new TestUser(["Manager"], readOnly: true, permitted: true));

		await Refused("UI:@[Error.DataReadOnly]", service, Writes);
		await Passed(service, Reads);
	}

	// the metadata branch used to leave before the check
	[TestMethod]
	public async Task A_metadata_command_is_checked_before_the_builder()
	{
		var service = Service(Meta, new TestUser([], readOnly: false, permitted: true));

		await Refused("Access denied", service, [("Invoke", s => s.InvokeAsync("_page/catalog/agent/index/0", "post", null, NoParams))]);
	}

	[TestMethod]
	public async Task A_metadata_remove_is_refused_to_a_read_only_user()
	{
		var service = Service(Meta, new TestUser(["Manager"], readOnly: true, permitted: true));

		await Refused("UI:@[Error.DataReadOnly]", service, [("DbRemove", s => s.DbRemoveAsync("_page/catalog/agent/index/0", 1, null, NoParams))]);
	}
}
