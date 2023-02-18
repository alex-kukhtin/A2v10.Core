// Copyright © 2015-2017 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace A2v10.Infrastructure
{
    public class ServiceLocator : IServiceLocator
    {
        private readonly Dictionary<Type, Object> _dynamicallyAddedServices;

        public ServiceLocator(IServiceCollection services)
            : this(services.BuildServiceProvider())
        {
            //do nothing
        }

        public ServiceLocator(IServiceProvider serviceProvider)
        {
            _dynamicallyAddedServices = new Dictionary<Type, Object>();
            ServiceProvider = serviceProvider;
        }

        public static IServiceLocator Current
        {
            get
            {
                return GetCurrentLocator();
            }
        }

        public static Func<IServiceLocator> GetCurrentLocator { get; set; }

        public IServiceProvider ServiceProvider { get; }

        public T GetService<T>() where T : class
        {
            T? result = GetServiceOrNull<T>();
            if (result == null)
            {
                throw new InvalidOperationException($"Service '{typeof(T).FullName}' not registered");
            }
            else
            {
                return result;
            }
        }

        public T GetService<T>(Func<IServiceLocator, T> func) where T : class
        {
            T? result = GetServiceOrNull<T>();
            if (result == null)
            {
                result = func(this);
                RegisterSingleton(result);
            }

            return result;
        }

        public Object GetService(Type type)
        {
            Object? result = ServiceProvider.GetService(type);
            if (result == null && !_dynamicallyAddedServices.TryGetValue(type, out result))
            {
                throw new InvalidOperationException(message: $"Service '{type}' not registered");
            }

            return result!;
        }

        public T? GetServiceOrNull<T>() where T : class
        {
            Object? result = ServiceProvider.GetService<T>();
            if (result == null)
            {
                _dynamicallyAddedServices.TryGetValue(key: typeof(T), out result);
            }

            return result as T;
        }

        public void RegisterSingleton<T>(T service) where T : class
        {
            if (!_dynamicallyAddedServices.ContainsKey(key: typeof(T)))
            {
                _dynamicallyAddedServices.Add(key: typeof(T), service);
            }
        }

        public Boolean IsServiceRegistered<T>() where T : class
        {
            return ServiceProvider.GetServices<T> != null && _dynamicallyAddedServices.ContainsKey(key: typeof(T));
        }

        public void Stop()
        {
            foreach (Object service in _dynamicallyAddedServices.Values)
            {
                if (service is ISupportStopService stopService)
                {
                    stopService.Stop();
                }
            }
        }
    }
}
