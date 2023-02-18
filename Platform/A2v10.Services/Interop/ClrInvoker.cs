// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using System.Threading.Tasks;

using A2v10.Infrastructure;
using A2v10.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace A2v10.Interop;

public class ClrInvoker
{
    private Boolean _enableThrow;
    private IRequestInfo _requestInfo;

    public static void CallInject(Object instance)
    {
        Type type = instance.GetType();
        MethodInfo? minject = type.GetMethod("Inject", BindingFlags.Public | BindingFlags.Instance);
        if (minject == null)
        {
            return;
        }

        ParameterInfo[] injprms = minject.GetParameters();
        List<object> injparsToCall = new List<Object>();

        foreach (ParameterInfo ip in injprms)
        {
            if (ip.ParameterType.IsInterface)
            {
                injparsToCall.Add(ServiceLocator.Current.GetService(ip.ParameterType));
            }
            else
            {
                throw new InteropException(message: "Invalid inject type");
            }
        }
        minject.Invoke(instance, injparsToCall.ToArray());
    }

    public static void CallSetRequestInfo(Object instance, IRequestInfo info)
    {
        if (info == null)
        {
            return;
        }

        Type type = instance.GetType();
        MethodInfo? miSetRI = type.GetMethod("SetRequestInfo", BindingFlags.Public | BindingFlags.Instance);
        if (miSetRI == null)
        {
            return;
        }

        miSetRI.Invoke(instance, new Object[] { info });
    }

    public void SetRequestInfo(IRequestInfo info)
    {
        _requestInfo = info;
    }

    public Object CreateInstance(String clrType)
    {
        (String assembly, String type) = ClrHelpers.ParseClrType(clrType);

        Object instance;
        try
        {
            instance = Activator.CreateInstance(assembly, type).Unwrap();
        }
        catch (Exception ex)
        {
            if (ex.InnerException != null)
            {
                ex = ex.InnerException;
            }

            throw new InteropException(message: $"Could not create type '{type}'. exception: '{ex.Message}'");
        }
        if (!(instance is IInvokeTarget))
        {
            throw new InteropException($"The type: '{type}' must implement interface 'IInvokeTarget'");
        }
        return instance;
    }

    public Object Invoke(String clrType, ExpandoObject parameters, Guid? guid = null)
    {
        Object instance = CreateInstance(clrType);
        CallInject(instance);
        CallSetRequestInfo(instance, _requestInfo);
        if (_enableThrow)
        {
            EnableThrowForInstance(instance);
        }

        return CallInvoke(instance, parameters, guid);
    }

    public async Task<Object> InvokeAsync(String clrType, ExpandoObject parameters, Guid? guid = null)
    {
        Object instance = CreateInstance(clrType);
        CallInject(instance);
        CallSetRequestInfo(instance, _requestInfo);
        if (_enableThrow)
        {
            EnableThrowForInstance(instance);
        }

        return await CallInvokeAsync(instance, parameters, guid);
    }

    public void EnableThrow()
    {
        _enableThrow = true;
    }

    private void EnableThrowForInstance(Object instance)
    {
        Type type = instance.GetType();
        MethodInfo? miEnableThrow = type.GetMethod("EnableThrow", BindingFlags.Public | BindingFlags.Instance);
        if (miEnableThrow != null)
        {
            miEnableThrow.Invoke(instance, null);
        }
    }

    private Object[] GetParameters(MethodInfo method, ExpandoObject parameters, Guid? guid)
    {
        ParameterInfo[] mtdParams = method.GetParameters();
        IDictionary<string, object> prmsD = parameters as IDictionary<String, Object>;

        List<Object> parsToCall = new List<Object>();

        for (Int32 paramNum = 0; paramNum < mtdParams.Length; paramNum++)
        {
            ParameterInfo param = mtdParams[paramNum];
            if (param.Name == "UserId" && param.ParameterType == typeof(Int64))
            {
                parsToCall.Add(parameters.Get<Int64>("UserId"));
            }
            else if (guid != null && param.Name == "Guid" && param.ParameterType == typeof(Guid))
            {
                parsToCall.Add(guid.Value);
            }
            else if (prmsD.TryGetValue(param.Name, out Object srcObj))
            {
                if (srcObj == null)
                {
                    parsToCall.Add(DefaultValue(param.ParameterType));
                }
                else if (srcObj is ExpandoObject && !param.ParameterType.IsPrimitive)
                {
                    string strJson = JsonConvert.SerializeObject(srcObj);
                    parsToCall.Add(JsonConvert.DeserializeObject(strJson, param.ParameterType));
                }
                else
                {
                    Type px = param.ParameterType;
                    if (px.IsNullableType())
                    {
                        px = px.GetNonNullableType();
                    }

                    if (param.ParameterType.IsAssignableFrom(srcObj.GetType()))
                    {
                        parsToCall.Add(srcObj);
                    }
                    else
                    {
                        parsToCall.Add(Convert.ChangeType(srcObj, param.ParameterType));
                    }
                }
            }
            else
            {
                parsToCall.Add(DefaultValue(param.ParameterType));
            }
        }

        return parsToCall.ToArray();
    }

    private async Task<Object> CallInvokeAsync(Object instance, ExpandoObject parameters, Guid? guid)
    {
        Type type = instance.GetType();
        MethodInfo? method = type.GetMethod("InvokeAsync", BindingFlags.Public | BindingFlags.Instance);
        if (method == null)
        {
            throw new InteropException(message: $"Method: 'InvokeAsync' is not found in type '{type.FullName}'");
        }

        object[] parsToCall = GetParameters(method, parameters, guid);
        return await (Task<Object>)method.Invoke(instance, parsToCall);
    }

    private Object CallInvoke(Object instance, ExpandoObject parameters, Guid? guid)
    {
        Type type = instance.GetType();
        MethodInfo? method = type.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance);
        if (method == null)
        {
            throw new InteropException(message: $"Method: 'Invoke' is not found in type '{type.FullName}'");
        }

        object[] parsToCall = GetParameters(method, parameters, guid);
        return method.Invoke(instance, parsToCall);
    }

    private Object? DefaultValue(Type tp)
    {
        return tp.IsValueType ? Activator.CreateInstance(tp) : null;
    }
}
