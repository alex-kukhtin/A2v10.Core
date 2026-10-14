// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

internal static class Constants
{
    // what 'initialValues' may take from the context (InitialSource.Context); the SQL of the load spells each
    public static class ContextValues
    {
        public const String Today = "today";
    }

    public static class FieldNames
    {
        public const String Id = nameof(Id);
        public const String Name = nameof(Name);
        public const String Memo = nameof(Memo);
        public const String Void = nameof(Void);
        public const String IsSystem = nameof(IsSystem);
        public const String RowNo = nameof(RowNo);
        public const String Date = nameof(Date);
        public const String Done = nameof(Done);
        public const String Document = nameof(Document);
        public const String Path = nameof(Path);
        /* Where a referenced document opens - a member of its reference view, never of a table. '$'
         * because no name from a file can take it (CheckNames), and the client does not send a '$'
         * member back.
         */
        public const String Url = "$Url";
        // how its cell draws whether it is posted - a platform icon name, as a colour is a class name
        public const String Icon = "$Icon";
        /* What a document's reference view adds to the row, in the order the resolve sends it. The one
         * list: the resolve, the TS type and the deploy hash read it (every document table carries it
         * in its Xtra), so a member added here moves the hash and reaches a deployed database. A
         * member here that the view does not make fails the first page that resolves a document.
         */
        public static readonly String[] RefViewMembers = [Url, Icon];
        public const String RowVersion = "rv";
        public const String Parent = nameof(Parent);
        // 'Parent' in the model: the word is reserved there - see TableColumn.ModelName
        public const String ParentElem = nameof(ParentElem);
        public const String Folder = nameof(Folder);
        public const String Color = nameof(Color);
        public const String For = nameof(For);
        /* The url parameter a new document's operation is named by: /document/invoice/new?Op=export.
         * PascalCase because the platform stores every query key so (PlatformUrl.AddQueryParam) - a
         * lower-case spelling here is a key nobody finds, and the document silently starts on the first.
         */
        public const String OperationQuery = "Op";
        // birth on basis: /document/waybillout/edit/new?BasedOn=123&Base=/document/order - PascalCase for the reason above
        public const String BasedOnQuery = "BasedOn";
        public const String BaseQuery = "Base";
        public const String Tag = nameof(Tag);
        public const String Tags = nameof(Tags);
        public const String Order = nameof(Order);
        public const String Pattern = nameof(Pattern);
        public const String Period = nameof(Period);
        // the counters table: which numbering, whose company, which period, where it has got to
        public const String Autonum = nameof(Autonum);
        public const String Company = nameof(Company);
        public const String Year = nameof(Year);
        public const String Quart = nameof(Quart);
        public const String Month = nameof(Month);
        public const String CurrentNumber = nameof(CurrentNumber);
        // a chart of accounts
        public const String AccountType = nameof(AccountType);
        public const String NormalBalance = nameof(NormalBalance);
        public const String SplitBy = nameof(SplitBy);
        public const String DisplayName = nameof(DisplayName);
        // a set of states: what the value is to the cycle - see StateRole
        public const String Role = nameof(Role);
        // a ledger: the leg's sign, its account, the account it corresponds to, its sum
        public const String InOut = nameof(InOut);
        public const String Acc = nameof(Acc);
        public const String CorrAcc = nameof(CorrAcc);
        public const String Sum = nameof(Sum);
        // the children of a tree node: the SQL declares it ('!Items'), the TreeGrid walks it
        public const String Items = nameof(Items);
        // stamps: who + when, see ColumnType.StampUser
        public const String UserCreated = nameof(UserCreated);
        public const String UtcDateCreated = nameof(UtcDateCreated);
        public const String UserModified = nameof(UserModified);
        public const String UtcDateModified = nameof(UtcDateModified);
        public const String UserPosted = nameof(UserPosted);
        public const String UtcDatePosted = nameof(UtcDatePosted);
    }
    /* The actions an endpoint answers, in the one spelling every dispatch reads: the model, the template,
     * the XAML (BaseModelBuilder, XamlBuilder.CreateXamlContainer). Four switches that never saw each other
     * let a button reach an action with a half missing. Every switch names every action of 'All'; an action
     * not built yet says so with NotImplementedException, and only an action a switch forgot reaches its
     * default arm (Unknown) - held by ActionDispatchTests, so a forgotten one fails a test, not a click.
     */
    public static class Actions
    {
        public const String Index = "index";
        public const String IndexPartial = "indexpartial";
        public const String Browse = "browse";
        public const String Edit = "edit";
        // not built yet: the page of an owner (CLAUDE.md, "Owner") - its model loads, its template and XAML do not exist
        public const String Show = "show";
        public const String BrowseFolder = "browsefolder";
        public const String EditFolder = "editfolder";

        public static readonly String[] All = [Index, IndexPartial, Browse, Edit, Show, BrowseFolder, EditFolder, Trans.Action, Print.Action];

        public static Exception Unknown(String action) =>
            new ArgumentOutOfRangeException(nameof(action), action, $"Unknown action '{action}'");

        public static Exception NotBuilt(String action, String what) =>
            new NotImplementedException($"The {what} of '{action}' is not built yet");
    }

    public static class FormNames
    {
        public const String Index  = "index";
        public const String Edit   = "edit";
        public const String Browse = "browse";
    }

    /* The shell of the transactions dialog - an object with no data of its own, carrying which tab
     * is in front. Here because the SQL writes these names and the XAML binds to them, and the two
     * are generated by different builders (SqlBuilderTrans, TransDialogXaml).
     */
    public static class Trans
    {
        public const String Action = "showtrans";
        public const String Root = nameof(Trans);
        public const String TypeName = "TTrans";
        public const String Tab = nameof(Tab);
    }

    /* A print blank is reached by an action of the endpoint, with the blank named in the query -
     * 'document/waybillin/print/2222?Form=printform1'. Two segments and an id, like every other
     * action; WHICH blank cannot ride in the path, because ParsePath takes no third segment.
     */
    public static class Print
    {
        public const String Action = "print";
        public const String FormQuery = "Form";
    }

    public static class SqlNames
    {
        /* A list of ids and nothing else, on the real 'platformid' - which is why it is ours and
         * not a2sys.[Id.TableType]: that one is declared 'bigint', and a database whose platformid
         * is uniqueidentifier would take the rows and write none. Lives beside 'platformid' itself.
         */
        public const String IdTableType = "dbo.[PlatformId.TableType]";
        // the roles of the user's card on its save (/admin/user) - see CliDatabaseCreator.CreateUserRoleTableType
        public const String UserRoleTableType = "a2security.[UserRole.TableType]";
        // the boundary of the user's card on its save - see CliDatabaseCreator.CreateUserBoundaryTableType
        public const String UserBoundaryTableType = "a2security.[UserBoundary.TableType]";
    }

    /* The blob an Image column points at (a2v10_blobs_simple.sql): no endpoint and no shape, so the
     * names are fixed here. The token field is Token, as the Save check and the upload response name it.
     * TR, as every reference: an author model named Blob would take the same type.
     */
    public static class Blobs
    {
        public const String Table = "a2blob.Blobs";
        public const String TypeName = "TRBlob";
        // what an Image takes: anything an <img> may draw, svg included - it is never served inline
        public const String ImageAccept = "image/*";
    }

    /* Platform entries of the filter namespace - the ones a trait or the kind contributes, as
     * opposed to the ones a column contributes under its own name. See FilterMetadata.
     */
    public static class FilterNames
    {
        public const String Period = nameof(Period);
        public const String Tags = nameof(Tags);
    }

    /* The keys the index reads from its query besides the filters, and the filters arrive in the same
     * flat query under their own names - so a filter named as one of these reads the platform's value
     * (a ref column 'Order' took the sort key 'Id', and the list came back empty on reload). Refused at
     * the bake (DeclarationBake.CheckFilterNames).
     */
    public static class QueryNames
    {
        public const String Offset = nameof(Offset);
        public const String PageSize = nameof(PageSize);
        public const String Order = nameof(Order);
        public const String Dir = nameof(Dir);
        public const String Fragment = nameof(Fragment);
        public const String From = nameof(From);
        public const String To = nameof(To);
        public static readonly String[] All = [Offset, PageSize, Order, Dir, Fragment, From, To, FilterNames.Tags];
    }

    public static class SchemaNames
    {
        public const String Catalog = "catalog";
        public const String Document = "document";
        public const String Journal = "journal";
        public const String Details = "details";
        public const String Report = "report";
        /* Platform-owned namespaces: their endpoints are declared in code, not in files. Own
         * namespaces on purpose - inside catalog/ or document/ they would be shadowed by, or
         * confused with, an application endpoint of the same name.
         */
        public const String Operation = "operation";
        public const String Tag = "tag";
        // the attachments of any record, under ?base= - see AttachmentEndpointMetadata
        public const String Attachment = "attachment";
        // the platform's screens for running the application, one endpoint per screen - see AdminEndpointMetadata
        public const String Admin = "admin";
        /* One step further than those two: an enum has no endpoint at all, in code or in files.
         * The namespace exists so that a reference can name a set by address.
         */
        public const String Enum = "enum";
        /* The second kind of set: its values carry a colour and a role in a life cycle, so a
         * reference to one says the record HAS a life cycle. Named after the entity whose states
         * they are - /state/order - which is why the model is composed and not the folder alone
         * (TableMetadata.SetDefaults): the candidates ride in the root of that entity's own page,
         * where 'Order' is already taken by the entity itself.
         */
        public const String State = "state";
        /* One file at the root and no folders under it: there is one registry of numberings, and
         * its schema, table and model default in TableMetadata.SetDefaults. The price is locality -
         * what 'waybill' is cannot be read beside the endpoint that names it, and the file grows
         * with the application. What makes that affordable is that the name resolves at load, so a
         * wrong one fails there and not at the first save.
         */
        public const String Autonum = "autonum";
        /* A chart of accounts, always named - /accplan/national - even when there is one: a bare
         * accplan/metadata.json would be a second spelling of the same fact, and a second plan would
         * move the first one together with every reference to it.
         */
        public const String AccPlan = "accplan";
        // the postings against one chart of accounts - two mirrored legs per posting
        public const String Ledger = "ledger";
    }

    /* Folders of a module's own layout: first segments with no endpoints of their own, so not a
     * namespace and not in SchemaNames. Reserved beside them all the same - an alias of that name
     * would make a path read two ways.
     */
    public static class ModuleFolders
    {
        // $crm/patches/catalog/agent - what follows is the address of the node patched, not a node of the module
        public const String Patches = "patches";
    }
}
