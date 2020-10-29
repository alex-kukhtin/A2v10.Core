using System;
using Portable.Xaml.Markup;

namespace A2v10.Xaml
{

    [ContentProperty("Content")]
    public class DataGridRowDetails : XamlElement
    {
        public UIElementBase Content { get; set; }
        public RowDetailsActivate Activate { get; set; }
        public Boolean Visible { get; set; }
    }
}
