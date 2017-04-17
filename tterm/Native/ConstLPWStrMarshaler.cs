using System;
using System.Runtime.InteropServices;

namespace tterm.Native
{
    /// <summary>
    /// Marshals a LPWStr (const wchar_t *) to a string without destroying the LPWStr.
    /// </summary>
    internal class ConstLPWStrMarshaler : ICustomMarshaler
    {
        private static readonly ICustomMarshaler Instance = new ConstLPWStrMarshaler();

        public static ICustomMarshaler GetInstance(string cookie)
        {
            return Instance;
        }

        public object MarshalNativeToManaged(IntPtr pNativeData)
        {
            return Marshal.PtrToStringUni(pNativeData);
        }

        public void CleanUpNativeData(IntPtr pNativeData)
        {
        }

        public int GetNativeDataSize()
        {
            throw new NotSupportedException();
        }

        public IntPtr MarshalManagedToNative(object ManagedObj)
        {
            throw new NotSupportedException();
        }

        public void CleanUpManagedData(object ManagedObj)
        {
            throw new NotSupportedException();
        }
    }
}
