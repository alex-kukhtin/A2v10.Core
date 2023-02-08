using A2v10.Data.Interfaces;
using A2v10.Web.Identity;
using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApiHost.Tests.MockDB;

public class MockDbContext : IDbContext
{
	public string? ConnectionString(string? source)
	{
		throw new NotImplementedException();
	}

	public void Execute<T>(string? source, string command, T element) where T : class
	{
		throw new NotImplementedException();
	}

	public TOut? ExecuteAndLoad<TIn, TOut>(string? source, string command, TIn element)
		where TIn : class
		where TOut : class
	{
		throw new NotImplementedException();
	}

	public Task<TOut?> ExecuteAndLoadAsync<TIn, TOut>(string? source, string command, TIn element)
		where TIn : class
		where TOut : class
	{
		throw new NotImplementedException();
	}

	public Task ExecuteAsync<T>(string? source, string command, T element) where T : class
	{
		throw new NotImplementedException();
	}

	public void ExecuteExpando(string? source, string command, ExpandoObject element)
	{
		throw new NotImplementedException();
	}

	public Task ExecuteExpandoAsync(string? source, string command, ExpandoObject element)
	{
		throw new NotImplementedException();
	}

	public IDbConnection GetDbConnection(string? source)
	{
		throw new NotImplementedException();
	}

	public Task<IDbConnection> GetDbConnectionAsync(string? source)
	{
		throw new NotImplementedException();
	}

	public T? Load<T>(string? source, string command, object? prms = null) where T : class
	{
		throw new NotImplementedException();
	}

	public Task<T?> LoadAsync<T>(string? source, string command, object? prms = null) where T : class
	{
		Object o = new AppUser<Int64>();
		return Task.FromResult((T?) o);
	}

	public IList<T>? LoadList<T>(string? source, string command, object? prms = null) where T : class
	{
		throw new NotImplementedException();
	}

	public Task<IList<T>?> LoadListAsync<T>(string? source, string command, object? prms = null) where T : class
	{
		throw new NotImplementedException();
	}

	public IDataModel LoadModel(string? source, string command, object? prms = null)
	{
		throw new NotImplementedException();
	}

	public Task<IDataModel> LoadModelAsync(string? source, string command, object? prms = null)
	{
		throw new NotImplementedException();
	}

	public Task<T> LoadTypedModelAsync<T>(string? source, string command, object? prms, int commandTimeout = 0) where T : new()
	{
		throw new NotImplementedException();
	}

	public ExpandoObject? ReadExpando(string? source, string command, ExpandoObject? prms = null)
	{
		throw new NotImplementedException();
	}

	public Task<ExpandoObject?> ReadExpandoAsync(string? source, string command, ExpandoObject? prms = null)
	{
		throw new NotImplementedException();
	}

	public void SaveList<T>(string? source, string command, object? prms, IEnumerable<T> list) where T : class
	{
		throw new NotImplementedException();
	}

	public Task SaveListAsync<T>(string? source, string command, object? prms, IEnumerable<T> list) where T : class
	{
		throw new NotImplementedException();
	}

	public IDataModel SaveModel(string? source, string command, ExpandoObject data, object? prms = null, int commandTimeout = 0)
	{
		throw new NotImplementedException();
	}

	public Task<IDataModel> SaveModelAsync(string? source, string command, ExpandoObject data, object? prms = null, Func<ITableDescription, ExpandoObject>? onSetData = null, int commandTimeout = 0)
	{
		throw new NotImplementedException();
	}

	public Task<IDataModel> SaveModelBatchAsync(string? source, string command, ExpandoObject data, object? prms = null, IEnumerable<BatchProcedure>? batches = null, int commandTimeout = 0)
	{
		throw new NotImplementedException();
	}
}
