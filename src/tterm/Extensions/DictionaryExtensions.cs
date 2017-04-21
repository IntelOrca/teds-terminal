using System.Collections.Generic;

namespace tterm.Extensions
{
    internal static class DictionaryExtensions
    {
        public static IDictionary<TKey, TValue> OverwriteWith<TKey, TValue>(
            this IDictionary<TKey, TValue> target,
            IDictionary<TKey, TValue> source)
        {
            foreach (var kvp in source)
            {
                target[kvp.Key] = kvp.Value;
            }
            return target;
        }

        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
        {
            if (dict.TryGetValue(key, out TValue value))
            {
                return value;
            }
            return default(TValue);
        }
    }
}
