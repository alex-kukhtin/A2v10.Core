// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.IO;
using System.Reflection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Services;

namespace TestServices;

/* Upload: the type of a blob. The inline types (raster, PDF) come from the bytes, never from the name;
 * a name that claims one of them without the bytes is refused; anything else keeps the type by the name.
 * The door's accept and maxSize are checked by that type, and the door takes the roles of its endpoint.
 * Everything is checked before Storage and the database: a refused upload leaves nothing behind.
 * Read: only the issued token reads, and a wrong one is refused before Storage.
 * Save: a non-empty reference <model>.<Key> carries the token Load or the upload issued; empty passes.
 * Attachments: an upload into a requireBase endpoint links the file to the owner under the base, in the same
 * insert; a field door links nothing, an attachment door takes nothing but a link.
 * requireBase: every call carries the base and its model.json's roles admit the user - checked on the Base
 * the procedure gets, so a command's posted Base, not the query's.
 */
[TestClass]
[TestCategory("Blobs")]
public class BlobServiceTests
{
	// a2blob.Blobs in memory: answers Blob.Insert and Blob.Load by the names the procedures use
	private sealed class Db : IDbContext
	{
		public readonly Dictionary<Int64, (String Name, String Mime, String BlobName, Guid AccessToken)> Rows = [];
		public readonly Dictionary<Int64, (String Base, String LinkId, Int64 Blob)> Links = [];

		static void Set(Object target, String name, Object? value) =>
			target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)!.SetValue(target, value);
		static Object? Get(Object source, String name) =>
			source.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)!.GetValue(source);
		static T Create<T>() => (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;

		public Task<TOut?> ExecuteAndLoadAsync<TIn, TOut>(String? source, String command, TIn element) where TIn : class where TOut : class
		{
			Assert.AreEqual("a2blob.[Blob.Insert]", command);
			var id = 1000L + Rows.Count;
			var row = ((String)Get(element, "Name")!, (String)Get(element, "Mime")!, (String)Get(element, "BlobName")!, Guid.NewGuid());
			Rows.Add(id, row);
			var output = Create<TOut>();
			Set(output, "Id", id);
			Set(output, "AccessToken", row.Item4);
			// an attachment: the link in the same call, as the procedure does
			if (Get(element, "Base") is String @base)
			{
				var link = 5000L + Links.Count;
				Links.Add(link, (@base, (String)Get(element, "LinkId")!, id));
				Set(output, "Link", link);
			}
			return Task.FromResult<TOut?>(output);
		}

		public Task<T?> LoadAsync<T>(String? source, String command, Object? prms = null) where T : class
		{
			Assert.AreEqual("a2blob.[Blob.Load]", command);
			var id = Convert.ToInt64(((IDictionary<String, Object?>)prms!)["Id"]);
			if (!Rows.TryGetValue(id, out var row))
				return Task.FromResult<T?>(null);
			var output = Create<T>();
			Set(output, "Name", row.Name);
			Set(output, "Mime", row.Mime);
			Set(output, "BlobName", row.BlobName);
			Set(output, "AccessToken", row.AccessToken);
			return Task.FromResult<T?>(output);
		}

		// nothing else is asked of the database here
		public String? ConnectionString(String? source) => throw new NotSupportedException();
		public void Execute<T>(String? source, String command, T element) where T : class => throw new NotSupportedException();
		public TOut? ExecuteAndLoad<TIn, TOut>(String? source, String command, TIn element) where TIn : class where TOut : class => throw new NotSupportedException();
		public Task ExecuteAsync<T>(String? source, String command, T element) where T : class => throw new NotSupportedException();
		public void ExecuteExpando(String? source, String command, ExpandoObject element) => throw new NotSupportedException();
		public Task ExecuteExpandoAsync(String? source, String command, ExpandoObject element) => throw new NotSupportedException();
		public IDbConnection GetDbConnection(String? source) => throw new NotSupportedException();
		public Task<IDbConnection> GetDbConnectionAsync(String? source) => throw new NotSupportedException();
		public T? Load<T>(String? source, String command, Object? prms = null) where T : class => throw new NotSupportedException();
		public IReadOnlyList<T>? LoadList<T>(String? source, String command, Object? prms = null) where T : class => throw new NotSupportedException();
		public Task<IReadOnlyList<T>?> LoadListAsync<T>(String? source, String command, Object? prms = null) where T : class => throw new NotSupportedException();
		public IDataModel LoadModel(String? source, String command, Object? prms = null) => throw new NotSupportedException();
		public Task<IDataModel> LoadModelAsync(String? source, String command, Object? prms = null) => throw new NotSupportedException();
		public IDataModel LoadModelSql(String? source, String sqlString, Object? prms = null) => throw new NotSupportedException();
		public Task<IDataModel> LoadModelSqlAsync(String? source, String sqlString, Object? prms = null) => throw new NotSupportedException();
		public Task<IDataModel> LoadModelSqlAsync(String? source, String sqlString, Action<DbParameterCollection> onSetParams) => throw new NotSupportedException();
		public void LoadRaw(String? source, String procedure, ExpandoObject prms, Action<Int32, IDataReader> action) => throw new NotSupportedException();
		public Task LoadRawAsync(String? source, String procedure, ExpandoObject prms, Action<Int32, IDataReader> action) => throw new NotSupportedException();
		public Task<T> LoadTypedModelAsync<T>(String? source, String command, Object? prms, Int32 commandTimeout = 0) where T : new() => throw new NotSupportedException();
		public Task<T?> LoadTypedModelAsync<T>(String? source, String command, Object? prms = null) where T : new() => throw new NotSupportedException();
		public ExpandoObject? ReadExpando(String? source, String command, ExpandoObject? prms = null) => throw new NotSupportedException();
		public Task<ExpandoObject?> ReadExpandoAsync(String? source, String command, ExpandoObject? prms = null) => throw new NotSupportedException();
		public void SaveList<T>(String? source, String command, Object? prms, IEnumerable<T> list) where T : class => throw new NotSupportedException();
		public Task SaveListAsync<T>(String? source, String command, Object? prms, IEnumerable<T> list) where T : class => throw new NotSupportedException();
		public IDataModel SaveModel(String? source, String command, ExpandoObject data, Object? prms = null, Int32 commandTimeout = 0) => throw new NotSupportedException();
		public Task<IDataModel> SaveModelAsync(String? source, String command, ExpandoObject data, Object? prms = null, Func<ITableDescription, ExpandoObject>? onSetData = null, Int32 commandTimeout = 0) => throw new NotSupportedException();
		public Task<IDataModel> SaveModelBatchAsync(String? source, String command, ExpandoObject data, Object? prms = null, IEnumerable<BatchProcedure>? batches = null, Int32 commandTimeout = 0) => throw new NotSupportedException();
		public Task<TOut?> SaveTypedModelAsync<TIn, TOut>(String? source, String command, TIn data, Object? prms = null) where TOut : new() => throw new NotSupportedException();
		public IParameterBuilder ParameterBuilder(DbParameterCollection prms) => throw new NotSupportedException();
		public Task LoadStreamAsync(String? source, String procedure, Object? prms, Func<IDataReader, Task> onMetadata, Func<IDataReader, Task> onRecord, CancellationToken token = default) => throw new NotSupportedException();
		public Task LoadStreamSqlAsync(String? source, String sqlString, Object? prms, Func<IDataReader, Task> onMetadata, Func<IDataReader, Task> onRecord, CancellationToken token = default) => throw new NotSupportedException();
	}

	// Storage in memory; Reads counts what was read, so a refusal can show it read nothing
	private sealed class Storage : IBlobStorage, IBlobStorageProvider
	{
		public readonly Dictionary<String, Byte[]> Files = [];
		public Int32 Reads;
		public IBlobStorage FindBlobStorage(String name) => this;
		public Task<ReadOnlyMemory<Byte>> LoadAsync(String? source, String? container, String blobName)
		{
			Reads++;
			return Task.FromResult<ReadOnlyMemory<Byte>>(Files[blobName]);
		}
		public Task SaveAsync(String? source, String? container, IBlobUpdateInfo blobInfo)
		{
			using var ms = new MemoryStream();
			blobInfo.Stream!.CopyTo(ms);
			Files.Add(blobInfo.BlobName!, ms.ToArray());
			return Task.CompletedTask;
		}
		public Task DeleteAsync(String? source, String? container, String blobName) => throw new NotSupportedException();
	}

	private sealed class Tokens : ITokenProvider
	{
		public String GenerateToken(Guid accessToken) => $"T:{accessToken}";
	}

	// the same as ModelJsonPartProvider, over model.json files built in code, by folder; none - null, as no file
	private sealed class Parts(IReadOnlyDictionary<String, ModelJson> files) : IModelJsonPartProvider
	{
		public Task<ModelJson?> GetModelJsonAsync(IPlatformUrl url)
		{
			if (!files.TryGetValue(url.LocalPath, out var file))
				return Task.FromResult<ModelJson?>(null);
			file.OnEndInit(url);
			return Task.FromResult<ModelJson?>(file);
		}
	}

	private sealed record Stand(BlobService Service, Db Db, Storage Storage, IModelView Edit, Parts Parts);

	// attachment: requireBase, the door Upload, the dialog index, the command delete; open to all roles
	private static ModelJson AttachmentFile() => new()
	{
		Schema = "cat",
		Model = "Attachment",
		RequireBase = true,
		Dialogs = new(StringComparer.OrdinalIgnoreCase) { ["index"] = new ModelJsonDialog() { Index = true } },
		Blobs = new(StringComparer.OrdinalIgnoreCase) { ["Upload"] = new ModelJsonBlobNew() },
		Commands = new(StringComparer.OrdinalIgnoreCase)
		{
			["delete"] = new ModelJsonCommand() { Type = ModelCommandType.sql, Procedure = "Attachment.Delete" }
		}
	};

	/* catalog/agentblob: model Agent, role Manager, the door Logo, the dialog edit that saves it - and the base
	 * of the attachments; catalog/locked: a base of the Admin role alone; catalog/none: a base with no model.json
	 */
	private static Stand Create(String? accept = null, Int32? maxSize = null, TestUser? user = null)
	{
		var file = new ModelJson()
		{
			Schema = "cat",
			Model = "Agent",
			Roles = ["Manager"],
			Dialogs = new(StringComparer.OrdinalIgnoreCase)
			{
				["edit"] = new ModelJsonDialog()
			},
			Blobs = new(StringComparer.OrdinalIgnoreCase)
			{
				["Logo"] = new ModelJsonBlobNew() { Accept = accept, MaxSize = maxSize }
			}
		};
		file.OnEndInit(new PlatformUrl(UrlKind.Dialog, "catalog/agentblob/edit/"));
		var db = new Db();
		var storage = new Storage();
		var sp = new ServiceCollection().AddSingleton<IBlobStorageProvider>(storage).BuildServiceProvider();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<String, String?>() { ["BlobStorage:Provider"] = "Memory" })
			.Build();
		var attachment = AttachmentFile();
		var parts = new Parts(new Dictionary<String, ModelJson>()
		{
			["catalog/agentblob"] = file,
			["catalog/locked"] = new ModelJson() { Roles = ["Admin"] },
			["attachment"] = attachment
		});
		var service = new BlobService(sp, new ModelJsonReader(parts, new NullAppRuntimeBuilder()), db,
			user ?? new TestUser(["Manager"]), config, new Tokens());
		return new Stand(service, db, storage, file.GetDialog("edit"), parts);
	}

	private static readonly Byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
	private static readonly Byte[] Html = "<html><script>alert(1)</script></html>"u8.ToArray();
	private static readonly Byte[] Svg = "<svg xmlns='http://www.w3.org/2000/svg'/>"u8.ToArray();

	// mime = the type by the name, as BlobController passes it
	private static Task<BlobUploadResult> Upload(Stand stand, String name, String mime, Byte[] bytes, String key = "Logo")
		=> stand.Service.UploadAsync($"catalog/agentblob/{key}", name, mime, new MemoryStream(bytes));

	private static async Task Refused(Stand stand, String expected, Func<Task> upload)
	{
		var ex = await Assert.ThrowsExactlyAsync<DataServiceException>(upload);
		Assert.AreEqual(expected, ex.Message);
		Assert.IsEmpty(stand.Storage.Files, "a refused upload wrote Storage");
		Assert.IsEmpty(stand.Db.Rows, "a refused upload wrote the database");
	}

	[TestMethod]
	public async Task Png_bytes_are_stored_as_png()
	{
		var stand = Create();
		var result = await Upload(stand, "a.png", MimeTypes.Image.Png, Png);

		Assert.AreEqual(MimeTypes.Image.Png, result.Mime);
		Assert.AreEqual(MimeTypes.Image.Png, stand.Db.Rows[result.Id].Mime);
		Assert.AreEqual("a.png", result.Name);
		Assert.AreEqual($"T:{stand.Db.Rows[result.Id].AccessToken}", result.Token);
		CollectionAssert.AreEqual(Png, stand.Storage.Files[stand.Db.Rows[result.Id].BlobName]);
	}

	[TestMethod]
	public async Task The_bytes_win_over_the_name()
	{
		var stand = Create();
		var result = await Upload(stand, "a.jpg", MimeTypes.Image.Jpg, Png);

		Assert.AreEqual(MimeTypes.Image.Png, result.Mime);
	}

	[TestMethod]
	public async Task A_name_claiming_an_inline_type_without_its_bytes_is_refused()
	{
		var stand = Create();

		await Refused(stand, "UI:The file content does not match its type", () => Upload(stand, "a.png", MimeTypes.Image.Png, Html));
	}

	[TestMethod]
	public async Task Svg_keeps_the_type_by_the_name()
	{
		var stand = Create(accept: "image/png,image/jpeg,image/svg+xml");
		var result = await Upload(stand, "logo.svg", MimeTypes.Image.Svg, Svg);

		Assert.AreEqual(MimeTypes.Image.Svg, result.Mime);
	}

	[TestMethod]
	public async Task Accept_is_checked_by_the_real_type()
	{
		var stand = Create(accept: "image/jpeg");

		await Refused(stand, "UI:Invalid file type 'image/png'", () => Upload(stand, "a.jpg", MimeTypes.Image.Jpg, Png));
	}

	[TestMethod]
	public async Task Accept_allows_a_wildcard()
	{
		var stand = Create(accept: "image/*");
		var result = await Upload(stand, "a.png", MimeTypes.Image.Png, Png);

		Assert.AreEqual(MimeTypes.Image.Png, result.Mime);
	}

	[TestMethod]
	public async Task A_file_above_maxSize_is_refused()
	{
		var stand = Create(maxSize: 1);
		var big = new Byte[2048];
		Png.CopyTo(big, 0);

		await Refused(stand, "UI:File is too large (max 1 KB)", () => Upload(stand, "a.png", MimeTypes.Image.Png, big));
	}

	[TestMethod]
	public async Task The_door_key_ignores_case()
	{
		var stand = Create();
		var result = await Upload(stand, "a.png", MimeTypes.Image.Png, Png, key: "logo");

		Assert.AreEqual(MimeTypes.Image.Png, result.Mime);
	}

	[TestMethod]
	public async Task An_unknown_door_is_refused()
	{
		var stand = Create();

		await Refused(stand, "Blob 'photo' not found in 'catalog/agentblob/model.json'", () => Upload(stand, "a.png", MimeTypes.Image.Png, Png, key: "Photo"));
	}

	[TestMethod]
	public async Task The_door_takes_the_roles_of_its_endpoint()
	{
		var stand = Create(user: new TestUser([]));

		await Refused(stand, "Access denied", () => Upload(stand, "a.png", MimeTypes.Image.Png, Png));
	}

	// Read

	[TestMethod]
	public async Task The_issued_token_reads_the_bytes()
	{
		var stand = Create();
		var uploaded = await Upload(stand, "a.png", MimeTypes.Image.Png, Png);

		var blob = await stand.Service.LoadAsync(uploaded.Id, uploaded.Token, thumb: false);

		Assert.AreEqual("a.png", blob.Name);
		Assert.AreEqual(MimeTypes.Image.Png, blob.Mime);
		CollectionAssert.AreEqual(Png, blob.Data.ToArray());
	}

	[TestMethod]
	public async Task A_wrong_token_is_refused_before_Storage()
	{
		var stand = Create();
		var uploaded = await Upload(stand, "a.png", MimeTypes.Image.Png, Png);

		var ex = await Assert.ThrowsExactlyAsync<DataServiceException>(() => stand.Service.LoadAsync(uploaded.Id, $"T:{Guid.NewGuid()}", thumb: false));

		Assert.AreEqual($"Invalid blob token ({uploaded.Id})", ex.Message);
		Assert.AreEqual(0, stand.Storage.Reads);
	}

	[TestMethod]
	public async Task A_missing_blob_is_refused()
	{
		var stand = Create();

		var ex = await Assert.ThrowsExactlyAsync<DataServiceException>(() => stand.Service.LoadAsync(9999, "T:any", thumb: false));

		Assert.AreEqual("Blob not found (9999)", ex.Message);
	}

	[TestMethod]
	public async Task A_thumb_without_a_thumbnail_reads_the_original()
	{
		var stand = Create();
		var uploaded = await Upload(stand, "a.png", MimeTypes.Image.Png, Png);

		var blob = await stand.Service.LoadAsync(uploaded.Id, uploaded.Token, thumb: true);

		CollectionAssert.AreEqual(Png, blob.Data.ToArray());
	}

	// Save

	private static ExpandoObject Obj(params (String Key, Object? Value)[] props)
	{
		var eo = new ExpandoObject();
		var d = (IDictionary<String, Object?>)eo;
		foreach (var (k, v) in props)
			d[k] = v;
		return eo;
	}

	// the posted model: { Agent: { Id, Logo } }
	private static ExpandoObject Saved(Object? logo, String root = "Agent", String field = "Logo")
		=> Obj((root, Obj(("Id", 1L), (field, logo))));

	private static Task Check(Stand stand, ExpandoObject data) => stand.Service.CheckReferencesAsync(stand.Edit, data);

	private static async Task RefusedOnSave(Stand stand, Int64 id, ExpandoObject data)
	{
		var ex = await Assert.ThrowsExactlyAsync<DataServiceException>(() => Check(stand, data));
		Assert.AreEqual($"Invalid blob token ({id})", ex.Message);
	}

	[TestMethod]
	public void The_edit_view_knows_the_doors_of_its_endpoint()
	{
		var stand = Create();

		CollectionAssert.AreEqual(new[] { "Logo" }, stand.Edit.BlobDoors.Keys.ToArray());
	}

	[TestMethod]
	public async Task An_empty_reference_passes()
	{
		var stand = Create();

		await Check(stand, Saved(Obj(("Id", 0L), ("Token", ""))));
		await Check(stand, Saved(null));
		await Check(stand, Obj(("Agent", Obj(("Id", 1L)))));
	}

	[TestMethod]
	public async Task The_issued_token_passes()
	{
		var stand = Create();
		var uploaded = await Upload(stand, "a.png", MimeTypes.Image.Png, Png);

		await Check(stand, Saved(Obj(("Id", uploaded.Id), ("Token", uploaded.Token))));
	}

	[TestMethod]
	public async Task A_foreign_id_with_the_old_token_is_refused()
	{
		var stand = Create();
		var mine = await Upload(stand, "a.png", MimeTypes.Image.Png, Png);
		var foreign = await Upload(stand, "b.png", MimeTypes.Image.Png, Png);

		await RefusedOnSave(stand, foreign.Id, Saved(Obj(("Id", foreign.Id), ("Token", mine.Token))));
	}

	[TestMethod]
	public async Task A_reference_without_a_token_is_refused()
	{
		var stand = Create();
		var uploaded = await Upload(stand, "a.png", MimeTypes.Image.Png, Png);

		await RefusedOnSave(stand, uploaded.Id, Saved(Obj(("Id", uploaded.Id))));
	}

	[TestMethod]
	public async Task A_bare_scalar_is_a_reference_without_a_token()
	{
		var stand = Create();
		var uploaded = await Upload(stand, "a.png", MimeTypes.Image.Png, Png);

		await RefusedOnSave(stand, uploaded.Id, Saved(uploaded.Id));
	}

	[TestMethod]
	public async Task The_posted_names_ignore_case()
	{
		var stand = Create();
		var mine = await Upload(stand, "a.png", MimeTypes.Image.Png, Png);
		var foreign = await Upload(stand, "b.png", MimeTypes.Image.Png, Png);

		await Check(stand, Saved(Obj(("id", mine.Id), ("token", mine.Token)), root: "agent", field: "logo"));
		await RefusedOnSave(stand, foreign.Id, Saved(Obj(("id", foreign.Id), ("token", mine.Token)), root: "agent", field: "logo"));
	}

	// a metadata folder: the root routes ('$meta'), the door names the model the data come as
	[TestMethod]
	public async Task A_synthesized_door_is_checked_under_its_own_model()
	{
		var stand = Create();
		var mine = await Upload(stand, "a.png", MimeTypes.Image.Png, Png);
		var foreign = await Upload(stand, "b.png", MimeTypes.Image.Png, Png);
		var meta = new ModelJson()
		{
			Model = IModelBase.MetaModel,
			Blobs = new(StringComparer.OrdinalIgnoreCase) { ["Logo"] = new ModelJsonBlobNew() { Model = "Agent" } }
		};
		meta.OnEndInit(new PlatformUrl(UrlKind.Dialog, "catalog/agent/edit/"));
		var edit = meta.GetDialog("edit");

		await stand.Service.CheckReferencesAsync(edit, Saved(Obj(("Id", mine.Id), ("Token", mine.Token))));
		var ex = await Assert.ThrowsExactlyAsync<DataServiceException>(
			() => stand.Service.CheckReferencesAsync(edit, Saved(Obj(("Id", foreign.Id), ("Token", mine.Token)))));
		Assert.AreEqual($"Invalid blob token ({foreign.Id})", ex.Message);
	}

	// Save through DataService: the database is null - a Save that passes the check fails there

	// the platform's reader over the stand's files: a base is looked up by its own address
	private static DataService Data(Stand stand, IBlobService? blobService)
		=> new(null!, new ModelJsonReader(stand.Parts, new NullAppRuntimeBuilder()), null!, new TestUser(["Manager"]), null!, null!, null!, null!, null!, null!,
			_blobService: blobService);

	private static Task SaveAsync(DataService data, ExpandoObject posted)
		=> data.SaveAsync("_dialog/catalog/agentblob/edit/1", posted, _ => { });

	[TestMethod]
	public async Task DataService_checks_the_references_before_the_database()
	{
		var stand = Create();
		var mine = await Upload(stand, "a.png", MimeTypes.Image.Png, Png);
		var foreign = await Upload(stand, "b.png", MimeTypes.Image.Png, Png);
		var data = Data(stand, stand.Service);

		var refused = await Assert.ThrowsExactlyAsync<DataServiceException>(() => SaveAsync(data, Saved(Obj(("Id", foreign.Id), ("Token", mine.Token)))));
		Assert.AreEqual($"Invalid blob token ({foreign.Id})", refused.Message);

		var passed = await Assert.ThrowsAsync<Exception>(() => SaveAsync(data, Saved(Obj(("Id", mine.Id), ("Token", mine.Token)))));
		Assert.IsNotInstanceOfType<DataServiceException>(passed, passed.ToString());
	}

	[TestMethod]
	public async Task DataService_with_doors_and_no_IBlobService_is_loud()
	{
		var stand = Create();

		var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => SaveAsync(Data(stand, null), Saved(null)));

		Assert.AreEqual("IBlobService is not registered", ex.Message);
	}

	// Attachments: the platform links the file to the owner under the base; the base's roles are checked

	private static Task<BlobLinkUploadResult> Attach(Stand stand, String @base, String owner, String path = "attachment/Upload")
		=> stand.Service.UploadLinkAsync(path, @base, owner, "a.png", MimeTypes.Image.Png, new MemoryStream(Png));

	[TestMethod]
	public async Task An_attachment_is_linked_to_its_owner_under_its_base()
	{
		var stand = Create();
		var result = await Attach(stand, "/catalog/agentblob", "1002");

		Assert.AreEqual(("/catalog/agentblob", "1002", result.Blob.Id), stand.Db.Links[result.Id]);
		Assert.AreEqual(MimeTypes.Image.Png, result.Blob.Mime);
		Assert.AreEqual($"T:{stand.Db.Rows[result.Blob.Id].AccessToken}", result.Blob.Token);
	}

	[TestMethod]
	public async Task A_field_door_has_nothing_to_link_to()
	{
		var stand = Create();

		await Refused(stand, "'catalog/agentblob' has no requireBase: nothing to link a file to",
			() => Attach(stand, "/catalog/agentblob", "1002", path: "catalog/agentblob/Logo"));
	}

	[TestMethod]
	public async Task An_attachment_door_refuses_an_upload_without_base()
	{
		var stand = Create();

		await Refused(stand, "'attachment' requires base: an upload there links the file to an owner",
			() => stand.Service.UploadAsync("attachment/Upload", "a.png", MimeTypes.Image.Png, new MemoryStream(Png)));
	}

	[TestMethod]
	public async Task An_attachment_requires_its_owner()
	{
		var stand = Create();

		await Refused(stand, "An upload into an attachment requires owner", () => Attach(stand, "/catalog/agentblob", ""));
	}

	// the reader's own refusal: no model.json and no metadata layer to synthesize one
	[TestMethod]
	public async Task A_base_without_model_json_is_loud()
	{
		var stand = Create();

		var ex = await Assert.ThrowsExactlyAsync<ModelJsonException>(() => Attach(stand, "/catalog/none", "1002"));
		Assert.AreEqual("File not found 'catalog/none/model.json'", ex.Message);
		Assert.IsEmpty(stand.Storage.Files, "a refused upload wrote Storage");
		Assert.IsEmpty(stand.Db.Rows, "a refused upload wrote the database");
	}

	[TestMethod]
	public async Task The_roles_of_the_base_admit_the_uploader()
	{
		var stand = Create();

		await Refused(stand, "Access denied", () => Attach(stand, "/catalog/locked", "1002"));
	}

	// requireBase through DataService: every call carries the base, the Base the procedure gets is checked

	private static readonly Action<ExpandoObject> NoParams = _ => { };

	private static async Task<Exception> Thrown(Func<Task> call)
	{
		try
		{
			await call();
		}
		catch (Exception ex)
		{
			return ex;
		}
		throw new AssertFailedException("the call returned without reaching the data");
	}

	// passed the check: reached the null database or the null command handler
	private static async Task Passed(Func<Task> call)
	{
		var ex = await Thrown(call);
		Assert.IsNotInstanceOfType<DataServiceException>(ex, ex.ToString());
	}

	[TestMethod]
	public async Task A_load_without_base_is_loud()
	{
		var stand = Create();
		var data = Data(stand, stand.Service);

		var ex = await Assert.ThrowsExactlyAsync<DataServiceException>(() => data.LoadAsync("_dialog/attachment/index/1002", NoParams));
		Assert.AreEqual("The endpoint requires base", ex.Message);
	}

	[TestMethod]
	public async Task A_load_checks_the_roles_of_its_base()
	{
		var stand = Create();
		var data = Data(stand, stand.Service);

		var ex = await Assert.ThrowsExactlyAsync<DataServiceException>(() => data.LoadAsync("_dialog/attachment/index/1002?base=/catalog/locked", NoParams));
		Assert.AreEqual("Access denied", ex.Message);
		await Passed(() => data.LoadAsync("_dialog/attachment/index/1002?base=/catalog/agentblob", NoParams));
	}

	[TestMethod]
	public async Task A_command_is_checked_by_the_Base_it_posts()
	{
		var stand = Create();
		var data = Data(stand, stand.Service);
		static ExpandoObject Posted(String? @base) => @base == null ? Obj(("Id", 5000L)) : Obj(("Id", 5000L), ("Base", @base), ("Owner", 1002L));

		var missing = await Assert.ThrowsExactlyAsync<DataServiceException>(() => data.InvokeAsync("_dialog/attachment/index/1002", "delete", Posted(null), NoParams));
		Assert.AreEqual("The endpoint requires base", missing.Message);
		// the posted Base wins over the query's, so it is the one checked
		var foreign = await Assert.ThrowsExactlyAsync<DataServiceException>(() => data.InvokeAsync("_dialog/attachment/index/1002?base=/catalog/agentblob", "delete", Posted("/catalog/locked"), NoParams));
		Assert.AreEqual("Access denied", foreign.Message);
		await Passed(() => data.InvokeAsync("_dialog/attachment/index/1002", "delete", Posted("/catalog/agentblob"), NoParams));
	}
}
