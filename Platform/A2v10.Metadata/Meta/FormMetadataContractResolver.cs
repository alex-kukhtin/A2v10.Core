// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Reflection;

using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

namespace A2v10.Metadata;

public class FormMetadataContractResolver : DefaultContractResolver
{
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var prop = base.CreateProperty(member, memberSerialization);
        if (prop.PropertyType == typeof(String))
        {
            prop.ShouldSerialize = instance =>
            {
                if (member is PropertyInfo miProp)
                {
                    var val = miProp.GetValue(instance, null);
                    if (val is String strVal)
                        return !String.IsNullOrWhiteSpace(strVal);
                }
                return true;
            };
        }
        else if (prop.DeclaringType == typeof(FormItem) && prop.PropertyType == typeof(FormItemGrid))
        {
            prop.ShouldSerialize = instance =>
            {
                var item = (FormItem)instance;
                return item.Grid?.IsEmpty != true;
            };
        }
        else if (prop.DeclaringType == typeof(FormItem) && prop.PropertyType == typeof(FormItemCommand))
        {
            prop.ShouldSerialize = instance =>
            {
                var item = (FormItem)instance;
                return item.Command?.IsEmpty != true;
            };
        }
        else if (prop.DeclaringType == typeof(FormItem) && prop.PropertyType == typeof(FormItemProps))
        {
            prop.ShouldSerialize = instance =>
            {
                var item = (FormItem)instance;
                return item.Props?.IsEmpty != true;
            };
        }
        else if (prop.DeclaringType == typeof(FormItem) && prop.PropertyName == "Items")
        {
            prop.ShouldSerialize = instance =>
            {
                var item = (FormItem)instance;
                return item.Items != null && item.Items.Length > 0;
            };
        }
        else if (prop.DeclaringType == typeof(Form) && prop.PropertyName == "Buttons")
        {
            prop.ShouldSerialize = instance =>
            {
                var item = (Form)instance;
                return item.Buttons != null && item.Buttons.Length > 0;
            };
        }
        return prop;
    }
}
