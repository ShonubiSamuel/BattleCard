// ServiceLocator.cs
// A minimal, robust DI-lite container for Unity projects.
// Thread-safety is included, but most use will be on the main thread.

using System;
using System.Collections.Generic;
using System.Text;

namespace YGO.Duel.Foundation
{
    /// <summary>
    /// Lightweight service registry / resolver.
    /// Register managers once at boot and resolve them anywhere via <see cref="Get{T}"/>.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly object _lock = new();
        private static readonly Dictionary<Type, object> _services = new();

        /// <summary>Number of registered services.</summary>
        public static int Count
        {
            get { lock (_lock) return _services.Count; }
        }

        /// <summary>
        /// Register a service instance for its concrete type <typeparamref name="T"/>.
        /// Throws if a service for the same type already exists and <paramref name="overwrite"/> is false.
        /// </summary>
        public static void Register<T>(T instance, bool overwrite = false) where T : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            var key = typeof(T);
            lock (_lock)
            {
                if (_services.ContainsKey(key) && !overwrite)
                    throw new InvalidOperationException(
                        $"ServiceLocator: Service of type {key.Name} is already registered. " +
                        $"Use overwrite:true or Replace<T>().");

                _services[key] = instance!;
            }
        }

        /// <summary>
        /// Replace (or insert) a service instance for <typeparamref name="T"/> unconditionally.
        /// </summary>
        public static void Replace<T>(T instance) where T : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            lock (_lock) _services[typeof(T)] = instance!;
        }

        /// <summary>
        /// Try to get a service. Returns true only if a service is present.
        /// </summary>
        public static bool TryGet<T>(out T result) where T : class
        {
            lock (_lock)
            {
                if (_services.TryGetValue(typeof(T), out var obj) && obj is T ok)
                {
                    result = ok;
                    return true;
                }
            }

            result = null!;
            return false;
        }

        /// <summary>
        /// Get a service. Throws if the service is not registered.
        /// </summary>
        public static T Get<T>() where T : class
        {
            lock (_lock)
            {
                if (_services.TryGetValue(typeof(T), out var obj) && obj is T ok)
                    return ok;

                throw new InvalidOperationException(
                    $"ServiceLocator: No service registered for type {typeof(T).Name}.");
            }
        }

        /// <summary>
        /// Get a service if present, otherwise create via <paramref name="factory"/>, register, and return it.
        /// </summary>
        public static T GetOrCreate<T>(Func<T> factory) where T : class
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            lock (_lock)
            {
                if (_services.TryGetValue(typeof(T), out var obj) && obj is T ok)
                    return ok;

                var created = factory();
                if (created == null)
                    throw new InvalidOperationException($"Factory for {typeof(T).Name} returned null.");

                _services[typeof(T)] = created;
                return created;
            }
        }

        /// <summary>
        /// Returns true if a service for <typeparamref name="T"/> is registered.
        /// </summary>
        public static bool Contains<T>() where T : class
        {
            lock (_lock) return _services.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Remove a service if present. Returns true if a service was removed.
        /// </summary>
        public static bool Deregister<T>() where T : class
        {
            lock (_lock) return _services.Remove(typeof(T));
        }

        /// <summary>
        /// Remove all services. Use with care (e.g., when exiting a duel/session).
        /// </summary>
        public static void Clear()
        {
            lock (_lock) _services.Clear();
        }

        /// <summary>
        /// Returns a human-readable dump of registered services (type names).
        /// </summary>
        public static string DebugDump()
        {
            lock (_lock)
            {
                var sb = new StringBuilder();
                sb.AppendLine("ServiceLocator registry:");
                foreach (var kvp in _services)
                    sb.AppendLine($"  - {kvp.Key.FullName}");
                return sb.ToString();
            }
        }
    }
}
