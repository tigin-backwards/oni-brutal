using System;
using System.Runtime.Serialization;

namespace UnityEngine
{
    public partial class UnityException
    {
        const int Result = unchecked((int)0x80004003);

        public UnityException() : base("A Unity Runtime error occurred!") { HResult = Result; }
        public UnityException(string message) : base(message) { HResult = Result; }
        public UnityException(string message, Exception innerException) : base(message, innerException) { HResult = Result; }

#pragma warning disable SYSLIB0051 // Unity declares the serialization constructor.
        protected UnityException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#pragma warning restore SYSLIB0051
    }
}
