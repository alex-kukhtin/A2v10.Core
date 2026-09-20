// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* What one endpoint's assembly refuses, reported instead of thrown. The map lists EVERY stage that
 * exists FOR THIS ENDPOINT, because "not checked" has to read on the spot: a missing key is read as
 * a passing one. 'Error' is a string and which stage produced it is the single 'failed' in the map -
 * a second field naming the stage would be a second place to disagree.
 */
public sealed record EndpointValidation(
    String Endpoint, IReadOnlyDictionary<String, String> Checks, String? Error);

/* The beam of goal #1 at the assembly seam: everything the layer can produce for one endpoint,
 * built and thrown away, so what is reported is what threw. Nothing is parsed - not the generated
 * SQL, not the author text spliced into it - and nothing is written. See CLAUDE.md, "Validation:
 * the endpoint assembled, never the file parsed".
 *
 * The stages are chained and stopped at the first throw; the order is dependency and nothing else,
 * and everything after the failure is 'unknown' - requested, not reached, nothing known. The
 * generators throw rather than accumulate, so one error per run is the shape of the loop, not a
 * limitation being tolerated.
 *
 * A stage's exception is a FINDING and is caught here. Anything thrown outside the stages means
 * the tool could not run and escapes into the caller's failure shape - the one distinction this
 * result exists to make.
 */
public sealed class EndpointValidator(DatabaseMetadataProvider _metadataProvider,
    IAppCodeProvider _codeProvider, IServiceProvider _serviceProvider)
{
    private const String Declaration = "declaration";
    private const String Screen = "screen";
    private const String Print = "print";

    private const String Passed = "passed";
    private const String Failed = "failed";
    private const String Unknown = "unknown";

    public async Task<EndpointValidation> ValidateAsync(String endpointPath)
    {
        /* The base an Id is typed by, as APP.JSON declares it. The database's own answer is the
         * fact (LoadPlatformIdAsync), and this check never goes there - so an application that
         * declares none cannot be validated at all. That is the tool failing to run and not a
         * finding about the endpoint, hence the one throw here that is deliberately not caught.
         */
        var platformId = await _metadataProvider.DeclaredPlatformIdAsync()
            ?? throw new InvalidOperationException(
                "app.json declares no 'platformid'. Validation types an Id without reading the database, so it has nowhere else to read the base from.");

        // the address as it will be printed back: the folder, whichever way the argument spelled it
        var name = endpointPath.NormalizeSlash().Trim('/');

        /* The load is the one stage every address has, whatever stands behind it, and it is also
         * what says WHICH stages follow - so the map is filled in two breaths and not one. A load
         * that fails names nothing further: what this address is, is exactly what was not
         * established, and a stage list guessed for it would be the map lying about the other
         * direction.
         */
        var checks = new Dictionary<String, String>() { [Declaration] = Unknown };
        EndpointMetadata endpoint;
        try
        {
            endpoint = await LoadAsync(endpointPath);
            checks[Declaration] = Passed;
        }
        catch (Exception ex)
        {
            checks[Declaration] = Failed;
            return new EndpointValidation(name, checks, ex.Message);
        }

        var stages = StagesOf(endpoint, platformId);
        foreach (var (stage, _) in stages)
            checks[stage] = Unknown;

        foreach (var (stage, run) in stages)
        {
            try
            {
                run();
                checks[stage] = Passed;
            }
            catch (Exception ex)
            {
                checks[stage] = Failed;
                /* The OUTERMOST message, where JsonResult.Fail takes the innermost: a wrapper here
                 * is the layer adding the context the bare message lacks ("form 'edit': ..."), and
                 * unwrapping would drop exactly the half that says where to look.
                 */
                return new EndpointValidation(name, checks, ex.Message);
            }
        }
        return new EndpointValidation(name, checks, null);
    }

    /* What can be assembled for this endpoint without a database - a property of its TYPE, read
     * here and not at the builder seam: every member of IModelBuilder is an action against data,
     * so the runtime's dispatch cannot answer this question. The price of the second dispatch is
     * accepted and is the reason the default arm is written as it is: a type this tool has not
     * been taught brings NO stages, which reads as "nothing here was checked" and never as consent.
     *
     * Everything after the load is synchronous by construction: nothing a generator builds touches
     * IO, and a stage that did would be a stage this tool cannot own.
     */
    private (String Stage, Action Run)[] StagesOf(EndpointMetadata endpoint, AppPlatformId platformId) =>
        endpoint switch
        {
            NormalEndpointMetadata normal =>
            [
                (Screen, () => Screens(normal, platformId)),
                (Print, () => Prints(normal))
            ],
            // a report prints from the page it renders, so it has no blank of its own to resolve
            ReportEndpointMetadata report =>
            [
                (Screen, () => ReportScreen(report, platformId))
            ],
            // a system endpoint: described nowhere, screen and data in code, so the load is all there is
            _ => []
        };

    /* The endpoint as the runtime loads it - whatever kind stands behind the address. For a data
     * endpoint that is its own declaration layered over its storage, seeds checked, names checked,
     * forms baked; the reference graph is linked here too. Files only: none of it asks the database
     * anything.
     */
    private Task<EndpointMetadata> LoadAsync(String endpointPath)
    {
        var (schema, table) = DatabaseMetadataProvider.ParsePath(endpointPath);
        return _metadataProvider.GetEndpointAsync(null, schema, table);
    }

    /* Every screen of the endpoint, built exactly as EndpointMaterializer builds it - the XAML, the
     * template and the .d.ts map - with the text dropped.
     *
     * The set is the BAKED FORMS and not a list of actions: a shape nothing renders (a set, a
     * numbering) has none, and "there is nothing to build" is not a finding. Same reason the
     * template of 'browse' is skipped: it shares the index one, and the materializer refuses to
     * write a second copy of it.
     */
    private static void Screens(NormalEndpointMetadata endpoint, AppPlatformId platformId)
    {
        foreach (var action in endpoint.Declaration.BakedForms.Keys)
        {
            var descriptor = new BuilderDescriptor()
            {
                Endpoint = endpoint,
                PlatformUrl = endpoint.PlatformUrl(action),
                PlatformId = platformId
            };
            XamlTextBulder.GetXaml(new XamlBuilder(descriptor).CreateXamlContainer(action));

            if (action == Constants.FormNames.Browse)
                continue;

            var ts = new ScriptBuilder(descriptor, isTs: true);
            if (action == Constants.FormNames.Index)
            {
                ts.CreateIndexTemplate();
                ts.CreateIndexMapTS();
            }
            else
            {
                ts.CreateEditTemplate();
                ts.CreateEditMapTS();
            }
        }
    }

    /* A report's screen, which is three refusals in a row: the type names a builder, the builder
     * over a ledger demands a ledger, and the picks - filters, groups, data - resolve against the
     * surface column by column. Then the page and the template are built.
     *
     * Built with NO parameters, which is the first open: a report is drawn before it is run, so
     * the defaults of its own declaration are what a page has to survive. Not serialized to XAML
     * afterwards, unlike a generated screen: nothing ever writes a report page as text - the
     * runtime renders the tree against data - so serializing it here would exercise a road that
     * does not exist.
     */
    private void ReportScreen(ReportEndpointMetadata endpoint, AppPlatformId platformId)
    {
        var builder = BaseReportBuilder.Create(_serviceProvider, endpoint, platformId);
        builder.SetGrouping(new ExpandoObject());
        builder.CreatePage();
        builder.CreateTemplate();
    }

    /* The 'Model' of every declared blank, resolved against the shape and turned into SQL. The
     * layout is never read, so this is the whole of what can be checked about paper - and the only
     * artifact with no other beam: a broken screen is seen by opening the page, a broken blank by
     * nobody until someone prints.
     */
    private void Prints(NormalEndpointMetadata endpoint)
    {
        foreach (var form in endpoint.Declaration.PrintForms)
        {
            var blank = PrintRequest.BlankOf(_codeProvider, endpoint, form.Path);
            new PrintSqlBuilder(endpoint.Storage, PrintModel.Parse(blank.Text)).Build();
        }
    }
}
