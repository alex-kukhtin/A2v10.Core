
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class FormBuilder
{
    private async Task<Form> CreateBrowseDialogAsync()
    {
        var tableMeta = await _metaProvider.GetSchemaAsync(_dataSource, _schema, _table);
        var appMeta = await _metaProvider.GetAppMetadataAsync(_dataSource);

        return new Form()
        {
            Is = FormItemIs.Dialog,
            UseCollectionView = true,   
            Items = [
                new FormItem()
                {
                    Is = FormItemIs.Grid,
                    Rows = "auto 1fr auto",
                    Items = [
                        new FormItem() {
                            Is = FormItemIs.Toolbar,
                            row = 1
                        },
                        new FormItem() {
                            Is = FormItemIs.DataGrid,
                            row = 2
                        },
                        new FormItem() {
                            Is = FormItemIs.Pager,
                            row = 3,
                            Data = "Parent.Pager"
                        }
                    ]                        
                }
            ]
        };
    }
}
