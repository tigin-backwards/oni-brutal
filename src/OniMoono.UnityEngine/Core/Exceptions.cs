using System;
using System.Runtime.Serialization;

// Unity's public exception types that the engine never throws but mods may: a mod that names one
// (Advanced Mopping throws MissingComponentException from its filter panel) fails with a
// TypeLoadException when the method that names it is compiled. Shapes as in Unity's CoreModule.
namespace UnityEngine
{
    [Serializable]
    public class MissingComponentException : SystemException
    {
        const int Result = unchecked((int)0x80004003);

        public MissingComponentException() : base("A Unity Runtime error occurred!") { HResult = Result; }
        public MissingComponentException(string message) : base(message) { HResult = Result; }
        public MissingComponentException(string message, Exception innerException) : base(message, innerException) { HResult = Result; }

#pragma warning disable SYSLIB0051 // Unity declares the serialization constructor.
        protected MissingComponentException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#pragma warning restore SYSLIB0051
    }

    [Serializable]
    public class UnassignedReferenceException : SystemException
    {
        const int Result = unchecked((int)0x80004003);

        public UnassignedReferenceException() : base("A Unity Runtime error occurred!") { HResult = Result; }
        public UnassignedReferenceException(string message) : base(message) { HResult = Result; }
        public UnassignedReferenceException(string message, Exception innerException) : base(message, innerException) { HResult = Result; }

#pragma warning disable SYSLIB0051 // Unity declares the serialization constructor.
        protected UnassignedReferenceException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#pragma warning restore SYSLIB0051
    }

    public class PlayerPrefsException : Exception
    {
        public PlayerPrefsException(string error) : base(error) { }
    }
}

namespace UnityEngine.Assertions
{
    public class AssertionException : Exception
    {
        readonly string m_UserMessage;

        public AssertionException(string message, string userMessage) : base(message) { m_UserMessage = userMessage; }

        public override string Message => m_UserMessage != null ? m_UserMessage + "\n" + base.Message : base.Message;
    }
}

namespace UnityEngine.Rendering
{
    public class InvalidImportException : Exception
    {
        public InvalidImportException(string message) : base(message) { }
    }
}
