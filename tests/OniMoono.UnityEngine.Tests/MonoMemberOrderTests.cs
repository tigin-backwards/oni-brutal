using System;
using System.Linq;
using System.Reflection;
using OniMoono.Engine;
using Xunit;

#pragma warning disable CS0649, CS0169, CS0414
namespace OniMoono.Tests
{
    /// Fields and properties in Mono's order (ARCHITECTURE D39): the type's own members, then each
    /// base class's, each class's in metadata order. The types are tools/UnityProbe's
    /// (MemberOrder.cs) and the expected orders the original player's for them
    /// (`PROBE_SCENARIO=memberorder`).
    public sealed class MonoMemberOrderTests
    {
        const BindingFlags Every = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
        const BindingFlags States = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        public class Mixed
        {
            public int i0;
            public static int s0;
            public const int c0 = 0;
            public int i1;
            public static readonly int r0 = 1;
            public const int c1 = 1;
            private int i2;
            private static int s1;
            public int I2 => i2 + s1;
        }

        public class Root<T>
        {
            public int rootA;
            public static int rootStatic;
            public object masterTarget;
            public const int RootConst = 3;
            protected int rootB;
            private int rootPrivate;
            public int P => rootPrivate;
        }

        public class Middle<T> : Root<T>
        {
            public static int middleStatic;
            public object middleField;
            public new int rootA;
        }

        public class Leaf : Middle<Leaf>
        {
            public object state0;
            public static int leafStatic;
            public const int LeafConst = 2;
            private object remaining;
            public object state1;
            public object R => remaining;
        }

        public class PropsBase
        {
            public int A { get; set; }
            public virtual int B { get; set; }
            public static int S { get; set; }
            public int C { get; set; }
        }

        public class Props : PropsBase
        {
            public new string C { get; set; }
            public static int T { get; set; }
            public override int B { get; set; }
            public int D { get; set; }
            public int this[int i] => i;
        }

        static string Names<T>(Type type, T[] members) where T : MemberInfo
        {
            MonoMemberOrder.Sort(type, members);
            return string.Join("|", members.Select(m => m.DeclaringType!.Name + "." + m.Name));
        }

        // As Harmony's MonoMod leaves a type's cached members once a patched method names it.
        static T[] ByToken<T>(T[] members) where T : MemberInfo => members.OrderBy(m => m.MetadataToken).ToArray();

        [Fact]
        public void FieldsOfAClassComeInMetadataOrder()
        {
            Assert.Equal("Mixed.i0|Mixed.s0|Mixed.c0|Mixed.i1|Mixed.r0|Mixed.c1|Mixed.i2|Mixed.s1", Names(typeof(Mixed), typeof(Mixed).GetFields(Every)));
            Assert.Equal("Mixed.i0|Mixed.s0|Mixed.c0|Mixed.i1|Mixed.r0|Mixed.c1", Names(typeof(Mixed), typeof(Mixed).GetFields()));
        }

        [Fact]
        public void AClasssFieldsComeBeforeItsBasesFields()
        {
            const string every = "Leaf.state0|Leaf.leafStatic|Leaf.LeafConst|Leaf.remaining|Leaf.state1|Middle`1.middleStatic|Middle`1.middleField|Middle`1.rootA|Root`1.rootA|Root`1.rootStatic|Root`1.masterTarget|Root`1.RootConst|Root`1.rootB";
            const string states = "Leaf.state0|Leaf.remaining|Leaf.state1|Middle`1.middleField|Middle`1.rootA|Root`1.rootA|Root`1.masterTarget|Root`1.rootB";
            Assert.Equal(every, Names(typeof(Leaf), typeof(Leaf).GetFields(Every)));
            Assert.Equal(states, Names(typeof(Leaf), typeof(Leaf).GetFields(States)));
            Assert.Equal("Leaf.state0|Leaf.leafStatic|Leaf.LeafConst|Leaf.state1|Middle`1.middleField|Middle`1.rootA|Root`1.rootA|Root`1.masterTarget", Names(typeof(Leaf), typeof(Leaf).GetFields()));
            // Whatever order MonoMod's sort left them in.
            Assert.Equal(every, Names(typeof(Leaf), ByToken(typeof(Leaf).GetFields(Every))));
            Assert.Equal(states, Names(typeof(Leaf), ByToken(typeof(Leaf).GetFields(States))));
        }

        [Fact]
        public void PropertiesComeAsFieldsDo()
        {
            const string every = "Props.C|Props.T|Props.B|Props.D|Props.Item|PropsBase.A|PropsBase.S|PropsBase.C";
            Assert.Equal(every, Names(typeof(Props), typeof(Props).GetProperties(Every)));
            Assert.Equal(every, Names(typeof(Props), ByToken(typeof(Props).GetProperties(Every))));
            Assert.Equal("Props.<C>k__BackingField|Props.<T>k__BackingField|Props.<B>k__BackingField|Props.<D>k__BackingField",
                Names(typeof(Props), typeof(Props).GetFields(Every)));
        }

        [Fact]
        public void ListsInMonosOrderAreLeftAsTheyAre()
        {
            var fields = typeof(Leaf).GetFields(States);
            MonoMemberOrder.Sort(typeof(Leaf), fields);
            var again = (FieldInfo[])fields.Clone();
            MonoMemberOrder.Sort(typeof(Leaf), again);
            Assert.Equal(fields, again);
            Assert.Empty(Names(typeof(int[]), typeof(int[]).GetFields(Every)));
        }
    }
}
