// Global registry of long-lived systems (services). A feature registers itself
// once and any other code can fetch it by interface/type without a scene reference.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimeKiller.Core
{
    public static class ServiceLocator
    {
        static readonly Dictionary<Type, object> services = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            if (services.ContainsKey(typeof(T)))
                Debug.LogWarning($"[ServiceLocator] {typeof(T).Name} registered twice — replacing.");
            services[typeof(T)] = service;
        }

        public static void Unregister<T>() where T : class => services.Remove(typeof(T));

        public static T Get<T>() where T : class
        {
            if (services.TryGetValue(typeof(T), out var s)) return (T)s;
            Debug.LogError($"[ServiceLocator] Requested missing service: {typeof(T).Name}");
            return null;
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            var found = services.TryGetValue(typeof(T), out var s);
            service = found ? (T)s : null;
            return found;
        }

        public static void Clear() => services.Clear();
    }
}
