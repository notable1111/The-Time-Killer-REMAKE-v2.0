// Central publish/subscribe hub. Systems communicate by publishing struct events
// here instead of referencing each other directly, so features stay decoupled
// (e.g. UI can react to player events without the player knowing UI exists).
using System;
using System.Collections.Generic;

namespace TimeKiller.Core
{
    public static class EventBus
    {
        static readonly Dictionary<Type, Delegate> handlers = new Dictionary<Type, Delegate>();

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handlers.TryGetValue(typeof(T), out var existing))
                handlers[typeof(T)] = Delegate.Combine(existing, handler);
            else
                handlers[typeof(T)] = handler;
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (!handlers.TryGetValue(typeof(T), out var existing)) return;
            var remaining = Delegate.Remove(existing, handler);
            if (remaining == null) handlers.Remove(typeof(T));
            else handlers[typeof(T)] = remaining;
        }

        public static void Publish<T>(T evt) where T : struct
        {
            if (handlers.TryGetValue(typeof(T), out var handler))
                ((Action<T>)handler)?.Invoke(evt);
        }

        // Called by GameBootstrap on play-mode start so stale subscriptions from a
        // previous session never survive (statics persist when domain reload is off).
        public static void Clear() => handlers.Clear();
    }
}
