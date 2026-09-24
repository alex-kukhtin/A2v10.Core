// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Services.Api;

namespace A2v10.Metadata;

public sealed class ScenarioException(String message) : Exception(message);

/* One scenario, top to bottom, through the door. What it writes is the model the screen gets -
 * collections by kind, references as objects - and what it reads is that same model, so the file
 * speaks the application's words and not the tables'.
 *
 * Records are named by the scenario; '{ "ref": name }' stands for the id the named record got.
 * Ids are never written: they are generated.
 */
internal sealed class ScenarioRunner(IServiceScopeFactory scopeFactory)
{
    sealed record Record(String At, Object Id);

    readonly Door _door = new(scopeFactory);
    readonly Dictionary<String, Record> _records = [];

    public async Task RunAsync(String file)
    {
        var scenario = JsonConvert.DeserializeObject<ExpandoObject>(File.ReadAllText(file))
            ?? throw new ScenarioException($"{file}: empty");
        var requirement = Get(scenario, "requirement") as String
            ?? throw new ScenarioException($"{file}: no 'requirement' - a scenario states the fact it checks");

        async Task Step(String where, Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (ScenarioException ex)
            {
                throw new ScenarioException($"{requirement}\n{where}: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new ScenarioException($"{requirement}\n{where}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (Get(scenario, "records") is ExpandoObject records)
            foreach (var (name, record) in records)
                await Step($"records.{name}", () => SaveAsync(name, Object(record, $"records.{name}")));

        var steps = Get(scenario, "steps") as List<Object> ?? [];
        for (var i = 0; i < steps.Count; i++)
        {
            var step = Object(steps[i], $"steps[{i}]");
            var what = Get(step, "do") as String;
            await Step($"steps[{i}] ({what})", () => what switch
            {
                "save" => SaveAsync(Required<String>(step, "name"), step),
                "post" => InvokeAsync(Required<String>(step, "name"), _door.PostAsync),
                "unpost" => InvokeAsync(Required<String>(step, "name"), _door.UnPostAsync),
                "expect" => ExpectAsync(step),
                _ => throw new ScenarioException($"'do' is '{what}': a step is 'save', 'post', 'unpost' or 'expect'")
            });
        }
    }

    /* New: the instance the screen starts from, defaults and all. Known: the record as loaded.
     * Either way it crosses JSON on its way back, as it does between the browser and the server.
     */
    async Task SaveAsync(String name, ExpandoObject step)
    {
        var at = Required<String>(step, "at");
        var data = Required<ExpandoObject>(step, "data");

        DataResult opened;
        if (_records.TryGetValue(name, out var known))
        {
            if (known.At != at)
                throw new ScenarioException($"'{name}' is a record of {known.At}, not of {at}");
            opened = await _door.OpenAsync(at, IdText(known.Id));
        }
        else
            opened = await _door.CreateAsync(at);
        var main = MainObject(opened.Model);
        var root = JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(opened.Data))!;

        var element = Get(root, main) as IDictionary<String, Object?>
            ?? throw new ScenarioException($"{at}: the model has no '{main}'");
        foreach (var (key, value) in data)
            element[key] = Resolve(value);

        var saved = await _door.SaveAsync(at, root);
        _records[name] = new Record(at, opened.IdOf(saved));
    }

    // the record already knows where it lives: a step names it, not its address
    Task InvokeAsync(String name, Func<String, Object, Task> command)
    {
        var record = _records.GetValueOrDefault(name)
            ?? throw new ScenarioException($"'{name}' is not a record of this scenario (yet)");
        return command(record.At, record.Id);
    }

    Task ExpectAsync(ExpandoObject step)
    {
        if (Get(step, "record") is String name)
            return ExpectRecordAsync(name, Required<ExpandoObject>(step, "data"));
        if (Get(step, "list") is List<Object> rows)
            return ExpectListAsync(Required<String>(step, "at"), rows);
        throw new ScenarioException("'expect' checks a 'record' (with 'data') or a 'list' (with 'at')");
    }

    // the fields named, and only those: what the scenario does not mention it does not claim
    async Task ExpectRecordAsync(String name, ExpandoObject expected)
    {
        var record = _records.GetValueOrDefault(name)
            ?? throw new ScenarioException($"'{name}' is not a record of this scenario");
        var model = (await _door.OpenAsync(record.At, IdText(record.Id))).Model;
        var main = MainObject(model);
        Match(expected, Get(model.Root, main), main);
    }

    /* The whole set, not 'contains': a row the scenario did not expect fails it too. The set is
     * the scenario's own records - the run is shared, and another scenario's rows are not this
     * one's business.
     */
    async Task ExpectListAsync(String at, List<Object> expected)
    {
        var type = MainType((await _door.CreateAsync(at)).Model);
        var index = (await _door.IndexAsync(at)).Model;
        var collection = index.Metadata["TRoot"].Fields
            .FirstOrDefault(f => f.Value.RefObject == type && Get(index.Root, f.Key) is IList).Key
            ?? throw new ScenarioException($"{at}: the index has no collection of {type}");

        var own = _records.Values.Where(r => r.At == at).Select(r => r.Id).ToList();
        var remaining = ((IList)Get(index.Root, collection)!).OfType<IDictionary<String, Object?>>()
            .Where(row => own.Any(id => Same(id, Field(row, "Id"))))
            .ToList();
        var actualText = Show(remaining);

        foreach (var row in expected)
        {
            var hit = remaining.FirstOrDefault(r => Matches(row, r));
            if (hit == null)
                throw new ScenarioException($"{at}: no row matches {Show(row)}\n  rows: {actualText}");
            remaining.Remove(hit);
        }
        if (remaining.Count > 0)
            throw new ScenarioException($"{at}: rows not expected: {Show(remaining)}\n  rows: {actualText}");
    }

    Boolean Matches(Object? expected, Object? actual)
    {
        try
        {
            Match(expected, actual, String.Empty);
            return true;
        }
        catch (ScenarioException)
        {
            return false;
        }
    }

    void Match(Object? expected, Object? actual, String path)
    {
        switch (expected)
        {
            case ExpandoObject eo when RefName(eo) is String name:
                var id = RecordId(name);
                var actualId = actual is IDictionary<String, Object?> ? Field(actual, "Id") : actual;
                if (!Same(id, actualId))
                    throw new ScenarioException($"{path}: expected record '{name}' (Id {id}), got {Show(actual)}");
                break;
            case ExpandoObject eo:
                if (actual is not IDictionary<String, Object?> obj)
                    throw new ScenarioException($"{path}: expected an object, got {Show(actual)}");
                foreach (var (key, value) in eo)
                {
                    if (!obj.TryGetValue(key, out var field))
                        throw new ScenarioException($"{path}.{key}: the model has no such field");
                    Match(value, field, $"{path}.{key}");
                }
                break;
            case List<Object> list:
                if (actual is not IList rows)
                    throw new ScenarioException($"{path}: expected a collection, got {Show(actual)}");
                if (rows.Count != list.Count)
                    throw new ScenarioException($"{path}: expected {list.Count} row(s), got {rows.Count}\n  expected: {Show(list)}\n  actual:   {Show(rows)}");
                for (var i = 0; i < list.Count; i++)
                    Match(list[i], rows[i], $"{path}[{i}]");
                break;
            default:
                if (!Same(expected, actual))
                    throw new ScenarioException($"{path}: expected {Show(expected)}, got {Show(actual)}");
                break;
        }
    }

    Object? Resolve(Object? value) => value switch
    {
        ExpandoObject eo when RefName(eo) is String name => Reference(RecordId(name)),
        ExpandoObject eo => eo.Aggregate(new ExpandoObject(), (acc, kv) =>
        {
            ((IDictionary<String, Object?>)acc)[kv.Key] = Resolve(kv.Value);
            return acc;
        }),
        List<Object> list => list.Select(Resolve).ToList(),
        _ => value
    };

    Object RecordId(String name)
        => _records.GetValueOrDefault(name)?.Id
            ?? throw new ScenarioException($"'{name}' is not a record of this scenario (yet)");

    static ExpandoObject Reference(Object id)
    {
        var eo = new ExpandoObject();
        ((IDictionary<String, Object?>)eo)["Id"] = id;
        return eo;
    }

    static String? RefName(ExpandoObject eo)
    {
        var d = (IDictionary<String, Object?>)eo;
        return d.Count == 1 && d.TryGetValue("ref", out var name) ? name as String : null;
    }

    // the record, as the model marks it: '[Agent!TAgent!MainObject]'
    static String MainObject(IDataModel model)
        => model.Metadata["TRoot"].MainObject
            ?? throw new ScenarioException("the model marks no main object: there is no record to read");

    static String MainType(IDataModel model)
        => model.Metadata["TRoot"].Fields[MainObject(model)].RefObject;

    static Boolean Same(Object? a, Object? b)
    {
        a = a is DBNull ? null : a;
        b = b is DBNull ? null : b;
        if (a is null || b is null)
            return a is null && b is null;
        if (a is DateTime || b is DateTime)
            return AsDate(a) == AsDate(b);
        if (IsNumber(a) && IsNumber(b))
            return Convert.ToDecimal(a, CultureInfo.InvariantCulture) == Convert.ToDecimal(b, CultureInfo.InvariantCulture);
        return String.Equals(Convert.ToString(a, CultureInfo.InvariantCulture), Convert.ToString(b, CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    static DateTime? AsDate(Object o) => o switch
    {
        DateTime dt => dt,
        String s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) => dt,
        _ => null
    };

    static Boolean IsNumber(Object o)
        => o is Byte or Int16 or Int32 or Int64 or Decimal or Double or Single;

    static String IdText(Object id) => Convert.ToString(id, CultureInfo.InvariantCulture)!;

    static String Show(Object? o) => o is null or DBNull ? "null" : JsonConvert.SerializeObject(o);

    static Object? Get(ExpandoObject eo, String key) => Field(eo, key);

    static Object? Field(Object? obj, String key)
        => obj is IDictionary<String, Object?> d && d.TryGetValue(key, out var value) ? value : null;

    static T Required<T>(ExpandoObject eo, String key)
        => Get(eo, key) is T value ? value : throw new ScenarioException($"'{key}' is missing or of the wrong shape");

    static ExpandoObject Object(Object? value, String where)
        => value as ExpandoObject ?? throw new ScenarioException($"{where}: expected an object");
}
