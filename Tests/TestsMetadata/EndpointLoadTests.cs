// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* The floor: the app really loads, and the two things this session decided about it hold.
 * See Platform/A2v10.Metadata/TESTS.md, layer C.
 */
public class EndpointLoadTests
{
    const String Document = "document";
    const String State = "state";

    static Task<EndpointMetadata> LoadAsync(String schema, String table) =>
        TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, schema, table);

    static async Task<NormalEndpointMetadata> LoadNormalAsync(String schema, String table) =>
        Assert.IsType<NormalEndpointMetadata>(await LoadAsync(schema, table));

    [Fact]
    public async Task Operation_and_its_storage_share_one_shape_instance()
    {
        var operation = await LoadNormalAsync(Document, "waybillin");
        var storage = await LoadNormalAsync(Document, String.Empty);

        // ReferenceEquals, not equality: the whole storage cache stands on this
        Assert.Same(storage.Storage, operation.Storage);
    }

    [Fact]
    public async Task PrintForms_are_the_operations_own_and_not_the_storages()
    {
        var operation = await LoadNormalAsync(Document, "waybillin");
        var storage = await LoadNormalAsync(Document, String.Empty);

        Assert.Empty(storage.Declaration.PrintForms);

        var form = Assert.Single(operation.Declaration.PrintForms);
        Assert.Equal("print/printform1", form.Path);
        Assert.False(String.IsNullOrEmpty(form.Title));
    }

    /* What a new record starts on, answered once and after the graph is linked: what the file
     * declared, the operation this endpoint IS, and the state its set begins with. The last two are
     * the far half of a reference - a row of the registry, a code the SET declares - so neither can
     * be baked with the declaration, and both used to be spelled twice: once for the values that
     * are sent and once for the map that resolves them.
     */
    [Fact]
    public async Task A_new_record_starts_on_its_operation_and_on_the_initial_state()
    {
        var operation = await LoadNormalAsync(Document, "waybillin");

        var initials = operation.AllInitials();

        var op = initials["Operation"];
        Assert.Equal(InitialSource.Literal, op.Source);
        Assert.Equal("waybillin", op.Value);

        // the code of the state whose role is Initial, and the document says nothing about it
        Assert.Equal("new", initials["State"].Value);
        Assert.DoesNotContain("State", operation.Declaration.Initials.Keys);
    }

    // the storage is the family and not an act, so it has no operation of its own to start on
    [Fact]
    public async Task The_storage_itself_starts_on_a_state_and_on_no_operation()
    {
        var initials = (await LoadNormalAsync(Document, String.Empty)).AllInitials();

        Assert.DoesNotContain("Operation", initials.Keys);
        Assert.Equal("new", initials["State"].Value);
    }

    /* The link back to the header is named for what it POINTS AT - the master's Model - which is
     * the one naming rule every link column here follows, refs included. It is therefore not a
     * constant any more, and the only way back to it is the column's type: this pins both halves,
     * because a generator that kept a literal name would still pass the first assertion alone.
     */
    [Fact]
    public async Task The_link_to_the_master_is_named_after_the_masters_model()
    {
        var doc = (await LoadNormalAsync(Document, String.Empty)).Storage;
        var rows = doc.Details["Rows"];

        Assert.Equal(doc.Model, rows.MasterField);

        var master = Assert.Single(rows.AllColumns(c => c.Type == ColumnType.Master));
        Assert.Equal(rows.MasterField, master.Name);
    }

    /* A set of states is named after the ENTITY whose states they are - /state/order - so its
     * model is composed and not the folder alone. The folder alone would name the candidates array
     * exactly as the entity's own collection, in the root of the entity's own page: two arrays
     * under one name, and nothing says so. An enum escapes this by being named after a concept.
     */
    [Fact]
    public async Task A_set_of_states_composes_its_model_from_the_folder()
    {
        var states = (await LoadNormalAsync(State, "order")).Storage;

        Assert.Equal(EndpointKind.State, states.Kind);
        Assert.Equal("OrderState", states.Model);
        Assert.Equal("OrderStates", states.CollectionName);
        // the SQL schema is the folder itself: only long words are abbreviated
        Assert.Equal("state.[OrderStates]", states.SqlTableName);
    }

    /* The baseline is an enum's row plus the two facts the kind exists for. Colour is found by
     * TYPE and the role by NAME - the way each will be found by the generators that write them.
     */
    [Fact]
    public async Task A_value_of_a_set_of_states_carries_a_colour_and_a_role()
    {
        var states = (await LoadNormalAsync(State, "order")).Storage;

        Assert.Single(states.AllColumns(c => c.Type == ColumnType.Color));
        Assert.Single(states.AllColumns(c => c.Name == "Role"));
        Assert.Equal("Name", states.Presentation);
        Assert.Equal(5, states.Values.Count);
    }

    /* The cardinalities the load enforces, seen from the other side: one state begins the cycle and
     * one is reaching the goal, while being under way and failing are as many as the domain has.
     * Both spellings travel untranslated - the role as the member of StateRole, the colour in the
     * lower case a CSS class requires.
     */
    [Fact]
    public async Task The_roles_of_a_cycle_are_counted_where_they_are_unequal()
    {
        var states = (await LoadNormalAsync(State, "order")).Storage;
        var live = states.Values.Where(v => !v.Void).ToList();

        Assert.Single(live, v => v.Role == StateRole.Initial);
        Assert.Single(live, v => v.Role == StateRole.Success);
        Assert.Equal(2, live.Count(v => v.Role == StateRole.InProgress));

        Assert.All(live, v => Assert.Equal(v.Color, v.Color?.ToLowerInvariant()));
    }

    /* The column is what says the record has a life cycle. It travels like an enum - a code, the
     * set riding with the page, one filter entry - and it is a type of its own so that the saying
     * is on the column itself, not behind a walk to the target's kind.
     */
    [Fact]
    public async Task A_state_column_points_at_a_set_of_states_and_travels_as_its_code()
    {
        var root = await LoadNormalAsync(Document, String.Empty);
        var doc = root.Storage;

        var state = Assert.Single(doc.AllColumns(c => c.Type == ColumnType.State));
        Assert.Equal(EndpointKind.State, state.RefTableCheck.Storage.Kind);
        // spelled as the key it points at, or the foreign key could not hold
        Assert.Equal("nvarchar(64)", state.SqlDataType());
        Assert.True(state.IsSetRef);

        var filter = Assert.Single(doc.Filters(root.Declaration), f => f.Name == "State");
        Assert.Equal(FilterKind.Set, filter.Kind);
    }

    /* Two entries on one column, because there are two questions and neither is the other's coarser
     * version: which state, and which stage of the cycle. The second is named after the column, so
     * a table with two state columns gets four entries that cannot meet.
     */
    [Fact]
    public async Task A_state_column_gives_the_namespace_two_filters()
    {
        var root = await LoadNormalAsync(Document, String.Empty);
        var doc = root.Storage;

        var role = Assert.Single(doc.Filters(root.Declaration), f => f.Kind == FilterKind.Role);
        Assert.Equal("StateRole", role.Name);
        Assert.Equal("State", role.ColumnCheck.Name);

        // an enum contributes one: it has no roles to ask about
        Assert.DoesNotContain(doc.Filters(root.Declaration), f => f.Name == "VatRateRole");
    }

    // an enum value is a code and a name: the two keys of a state set have nowhere to land here
    [Fact]
    public async Task An_enum_value_has_neither_of_them()
    {
        var rates = (await LoadNormalAsync("enum", "vatrate")).Storage;

        Assert.NotEmpty(rates.Values);
        Assert.All(rates.Values, v => Assert.Null(v.Role));
        Assert.All(rates.Values, v => Assert.Null(v.Color));
    }

    /* The second satellite that hangs under a record, and the reason the rule is a rule rather
     * than a detail of the details tables: it is built on the fly, by another factory, and would
     * be exactly the place for the old fixed name to survive.
     */
    [Fact]
    public async Task Tag_entries_are_named_by_the_same_rule()
    {
        var doc = (await LoadNormalAsync(Document, String.Empty)).Storage;
        var entries = TableMetadataDefaults.CreateTagEntriesTable(doc);

        Assert.Equal(doc.Model, entries.MasterField);

        var master = Assert.Single(entries.AllColumns(c => c.Type == ColumnType.Master));
        Assert.Equal(entries.MasterField, master.Name);
    }
}
