using System;
using System.Collections.Generic;

namespace Core.Events
{
    // Lightweight, ordered event bus for cross-manager communication.
    // Priority: lower value executes earlier. Subscribers are stored per event type.
    // Exceptions inside handlers are swallowed to avoid cascading failures; optionally surfaced via OnHandlerException.
    public static class EventRouter
    {
        private class Subscriber
        {
            public int Priority;
            public Delegate Handler;
        }

        private static readonly Dictionary<Type, List<Subscriber>> _subscribers = new();

        // Optional external hook for logging
        public static Action<Exception, Type> OnHandlerException;

        public static void Subscribe<T>(Action<T> handler, int priority = 0)
        {
            if (handler == null) return;
            var type = typeof(T);
            if (!_subscribers.TryGetValue(type, out var list))
            {
                list = new List<Subscriber>();
                _subscribers[type] = list;
            }
            list.Add(new Subscriber { Priority = priority, Handler = handler });
            list.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            var type = typeof(T);
            if (!_subscribers.TryGetValue(type, out var list)) return;
            list.RemoveAll(s => s.Handler == (Delegate)handler);
            if (list.Count == 0)
            {
                _subscribers.Remove(type);
            }
        }

        public static void Publish<T>(T evt)
        {
            var type = typeof(T);
            if (!_subscribers.TryGetValue(type, out var list) || list.Count == 0) return;
            // Create a copy to avoid modification during iteration
            var snapshot = list.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i].Handler is Action<T> action)
                {
                    try { action.Invoke(evt); }
                    catch (Exception ex) { OnHandlerException?.Invoke(ex, type); }
                }
            }
        }

        // Safe publish that returns whether any subscriber existed
        public static bool TryPublish<T>(T evt)
        {
            var type = typeof(T);
            if (!_subscribers.TryGetValue(type, out var list) || list.Count == 0) return false;
            Publish(evt);
            return true;
        }

        public static void ClearAll()
        {
            _subscribers.Clear();
        }
    }
}
