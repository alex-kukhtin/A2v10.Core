using System;
using System.Data;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.System.Xaml;
using A2v10.Xaml;
using A2v10.Xaml.DynamicRendrer;
using System.Data.Common;

namespace A2v10.Core.Web.Site.TestServices;


public static class DbParamsExtension
{
	public static DbParameterCollection AddBigInt(this DbParameterCollection coll, String name, Int64? value)
	{
		coll.Add(new SqlParameter(name, SqlDbType.BigInt) { Value = value != null ? value : DBNull.Value });
		return coll;
	}
	public static DbParameterCollection AddInt(this DbParameterCollection coll, String name, Int32? value)
	{
		coll.Add(new SqlParameter(name, SqlDbType.Int) { Value = value != null ? value : DBNull.Value });
		return coll;
	}

	public static DbParameterCollection AddString(this DbParameterCollection coll, String name, String? value, Int32 size = 255)
	{
		coll.Add(new SqlParameter(name, SqlDbType.NVarChar, size) { Value = value != null ? value : DBNull.Value });
		return coll;
	}
	public static DbParameterCollection AddDate(this DbParameterCollection coll, String name, DateTime? value)
	{
		coll.Add(new SqlParameter(name, SqlDbType.Date) { Value = value != null ? value : DBNull.Value });
		return coll;
	}
	public static DbParameterCollection AddBit(this DbParameterCollection coll, String name, Boolean? value)
	{
		coll.Add(new SqlParameter(name, SqlDbType.Bit) { Value = value != null ? value : DBNull.Value });
		return coll;
	}
}

public class TestPageHandler(IServiceProvider _serviceProvider) : IEndpointHandler
{ 
	private readonly IDbContext _dbContext = _serviceProvider.GetRequiredService<IDbContext>();
	private readonly ICurrentUser _currentUser = _serviceProvider.GetRequiredService<ICurrentUser>();

	private readonly IServiceProvider _xamlSericeProvider = new XamlServiceProvider();
	private readonly DynamicRenderer _dynamicRenderer = new(_serviceProvider);

	public async Task<String> RenderResultAsync(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject prms)
	{
		var sqlString = """
			select [Agent!TAgent!Object] = null, [Id!!Id] = a.Id, [Name!!Name] = a.Name, a.Memo
			from cat.Agents a where TenantId = @TenantId and Id = @Id;
		""";
		var tenantId = _currentUser.Identity.Tenant;

		var dm = await _dbContext.LoadModelSqlAsync(null, sqlString, dbprms =>
		{
			dbprms
				.AddInt("@TenantId", tenantId)
				.AddBigInt("@UserId", _currentUser.Identity.Id)
				.AddBigInt("@Id", 761);
		});

		var page = new Page()
		{
			Toolbar = new Toolbar(_xamlSericeProvider)
			{
				Children = [
					new Button() {
						Content = "Save",
						Icon = Icon.Save,
						Bindings = (btn) => {
							btn.SetBinding(nameof(Button.Command), new BindCmd() {Command = Xaml.CommandType.Save});
						}
					},
					new Separator(),
					new Button() {
						Content = "@[Reload]",
						Icon = Icon.Reload,
						Bindings = (btn) => {
							btn.SetBinding(nameof(Button.Command), new BindCmd() {Command = Xaml.CommandType.Reload});
						}
					},
				]
			},
			Children = [
				new Grid(_xamlSericeProvider) {
					Children = [
						new TextBox() {
							Label = "@[Name]",
							Bindings = (tb) => {
								tb.SetBinding(nameof(TextBox.Value), new Bind("Agent.Name"));
							}
						},
						new TextBox() {
							Label = "@[Memo]",
							Multiline  = true,
							Bindings = (tb) => {
								tb.SetBinding(nameof(TextBox.Value), new Bind("Agent.Memo"));
							},
							Attach = d => {
								d.Add("Grid.Row", 2);
								d.Add("Grid.ColSpan", 2);
							}
						}
					]
				}
			]
		};

		var template = $$"""

			const template = {
				validators: {
					'Agent.Name': '@[Error.Required]',
				}
			};

			module.exports = template;            
			""";

		String rootId = $"el{Guid.NewGuid()}";

		var rri = new DynamicRenderPageInfo()
		{
			RootId = rootId,
			Page = page,
			ModelView = modelView,
			PlatformUrl = platformUrl,
			Model = dm,
			Template = template
		};
		return await _dynamicRenderer.RenderPage(rri);
	}

	public async Task<ExpandoObject> SaveAsync(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject data, ExpandoObject prms)
	{

		var sqlString = """
		declare @Agent as table(Id bigint, Name nvarchar(255), Memo nvarchar(255));
		
		insert into @Agent(Id, [Name], Memo)
		select @Id, @Name, @Memo;

		merge cat.Agents as t
		using @Agent as s
		on t.TenantId = @TenantId and t.Id = s.Id
		when matched then update set
			t.[Name] = s.[Name],
			t.Memo = s.Memo;

		select [Agent!TAgent!Object] = null, [Id!!Id] = a.Id, [Name!!Name] = a.Name, a.Memo
		from cat.Agents a where TenantId = @TenantId and Id = @Id;
		""";
		var tenantId = _currentUser.Identity.Tenant;

		var dm = await _dbContext.LoadModelSqlAsync(null, sqlString, dbprms =>
		{
			dbprms
				.AddInt("@TenantId", tenantId)
				.AddBigInt("@UserId", _currentUser.Identity.Id)
				.AddBigInt("@Id", 761)
				.AddString("@Name", data.Eval<String>("Agent.Name"))
				.AddString("@Memo", data.Eval<String>("Agent.Memo"));
		});
		return dm.Root;
	}

	DataTable CreateTableParameter(ExpandoObject data)
	{
		var dt = new DataTable();
		dt.Columns.Add(new DataColumn("Id", typeof(Int64)));
		dt.Columns.Add(new DataColumn("Name", typeof(String)) { MaxLength = 255 });
		dt.Columns.Add(new DataColumn("Memo", typeof(String)) { MaxLength = 255 });
		var r = dt.NewRow();
		r[0] = data.Get<Int64>("Id");
		r[1] = data.Get<String>("Name");
		r[2] = data.Get<String>("Memo");
		return dt;
	}

	public async Task<IDataModel> ReloadAsync(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject prms)
	{
		var sqlString = """
			select [Agent!TAgent!Object] = null, [Id!!Id] = a.Id, [Name!!Name] = a.Name, a.Memo
			from cat.Agents a where TenantId = @TenantId and Id = @Id;
		""";
		var tenantId = _currentUser.Identity.Tenant;

		var dm = await _dbContext.LoadModelSqlAsync(null, sqlString, dbprms =>
		{
			dbprms
				.AddInt("@TenantId", tenantId)
				.AddBigInt("@UserId", _currentUser.Identity.Id)
				.AddBigInt("@Id", 761);
		});
		return dm;
	}
}
