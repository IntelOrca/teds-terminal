using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using tterm.Extensions;
using static tterm.Native.Win32;

namespace tterm
{
    internal class EnvironmentVariableHelper
    {
        public IDictionary<string, string> ExpandVariables(IDictionary<string, string> target, IDictionary<string, string> source)
        {
            foreach (var kvp in source)
            {
                string key = ExpandVariables(kvp.Key, target);
                string value = ExpandVariables(kvp.Value, target);
                target[key] = value;
            }
            return target;
        }

        public string ExpandVariables(string s, IDictionary<string, string> env)
        {
            var sb = new StringBuilder();
            int index = 0;
            for (;;)
            {
                int start = s.IndexOf('%', index);
                if (start != -1)
                {
                    int end = s.IndexOf('%', start + 1);
                    if (end != -1)
                    {
                        string varName = s.Substring(start + 1, end - start - 1);
                        string varValue = env.GetValueOrDefault(varName);

                        sb.Append(s.Substring(index, start - index));
                        sb.Append(varValue);

                        index = end + 1;
                        continue;
                    }
                }
                sb.Append(s.Substring(index));
                break;
            }
            return sb.ToString();
        }

        public Dictionary<string, string> GetSystem()
        {
            Dictionary<string, string> result = null;
            IntPtr env = IntPtr.Zero;
            try
            {
                if (CreateEnvironmentBlock(out env, IntPtr.Zero, false))
                {
                    result = FromNTSA(env);
                }
            }
            finally
            {
                DestroyEnvironmentBlock(env);
            }
            return result;
        }

        public Dictionary<string, string> GetUser()
        {
            Dictionary<string, string> result = null;
            IntPtr userToken = UserUtility.GetPrimaryToken();
            IntPtr env = IntPtr.Zero;
            try
            {
                if (CreateEnvironmentBlock(out env, userToken, false))
                {
                    result = FromNTSA(env);
                }
            }
            finally
            {
                DestroyEnvironmentBlock(env);
                CloseHandle(userToken);
            }
            return result;
        }

        private static Dictionary<string, string> FromNTSA(IntPtr env)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string var;
            while (!string.IsNullOrEmpty(var = Marshal.PtrToStringUni(env)))
            {
                var kvp = GetKVPFromVar(var);
                if (!string.IsNullOrEmpty(kvp.Key))
                {
                    dict[kvp.Key] = kvp.Value;
                }

                // Add UTF-16 string length + 2 for null terminator
                env = env + (var.Length * 2) + 2;
            }
            return dict;
        }

        private static KeyValuePair<string, string> GetKVPFromVar(string var)
        {
            string key = null;
            string value = null;
            int eqIndex = var.IndexOf('=');
            if (eqIndex != -1)
            {
                key = var.Substring(0, eqIndex);
                value = var.Substring(eqIndex + 1);
            }
            return new KeyValuePair<string, string>(key, value);
        }
    }
}
