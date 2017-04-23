using System.Collections;
using System.Collections.Generic;

namespace tterm.Extensions
{
    internal static class DictionaryExtensions
    {
        public static Dictionary<TKey, TValue> ToGeneric<TKey, TValue>(this IDictionary dict)
        {
            var genericDict = new Dictionary<TKey, TValue>();
            foreach (var key in dict.Keys)
            {
                genericDict[(TKey)key] = (TValue)dict[key];
            }
            return genericDict;
        }

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
