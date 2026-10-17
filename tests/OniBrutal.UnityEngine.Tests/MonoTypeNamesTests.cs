using System;
using System.Collections.Generic;
using OniBrutal.Engine;
using Xunit;

namespace OniBrutal.Tests
{
    public class MonoTypeNamesTests
    {
        const string Mscorlib = "mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";

        static string AssemblyOf(Type t) => t.Assembly == typeof(object).Assembly ? Mscorlib : t.Assembly.FullName!;

        public class Outer<TA, TB>
        {
            public class Inner { }
        }

        [Fact]
        public void NonGenericTypesKeepTheirFullName()
        {
            Assert.Equal("System.Single", MonoTypeNames.FullName(typeof(float), AssemblyOf));
            Assert.Equal("System.Single[]", MonoTypeNames.FullName(typeof(float[]), AssemblyOf));
        }

        [Fact]
        public void TypeArgumentsNameTheOriginalAssemblies()
        {
            // As in a save from the original player: StateMachine`4+Signal+Context[[…],[System.Object, mscorlib, …]].
            var inner = typeof(Outer<MonoTypeNamesTests, object>.Inner);
            string self = typeof(MonoTypeNamesTests).Assembly.FullName!;
            Assert.Equal($"OniBrutal.Tests.MonoTypeNamesTests+Outer`2+Inner[[OniBrutal.Tests.MonoTypeNamesTests, {self}],[System.Object, {Mscorlib}]]",
                MonoTypeNames.FullName(inner, AssemblyOf));
            Assert.Equal($"System.Collections.Generic.Dictionary`2[[System.String, {Mscorlib}],[System.Collections.Generic.List`1[[System.Int32, {Mscorlib}]], {Mscorlib}]]",
                MonoTypeNames.FullName(typeof(Dictionary<string, List<int>>), AssemblyOf));
        }
    }
}
