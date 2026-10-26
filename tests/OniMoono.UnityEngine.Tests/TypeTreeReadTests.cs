using System;
using System.Collections.Generic;
using OniMoono.Assets.IO;
using OniMoono.Assets.TypeTrees;
using OniMoono.Engine.Serialization;
using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    public class TypeTreeReadTests
    {
        class BoolSample : ScriptableObject
        {
            public bool first = false;
            public float middle = 0f;
            public bool last = true;
        }

        static TypeTreeNode Leaf(string type, string name, int size, bool align) =>
            new() { TypeName = type, Name = name, ByteSize = size, MetaFlag = align ? TypeTreeNode.AlignBytesFlag : 0 };

        /// Unity 6 (6000.3) type trees name a script's bool fields UInt8 (Duplicant Stat Selector's
        /// dss_uiassets, uGUI layout groups' m_ChildControlHeight and others); they are still read
        /// into the bool fields, not dropped for the field's default.
        [Fact]
        public void BoolFieldsLaidOutAsUInt8AreRead()
        {
            var nodes = new List<TypeTreeNode> { Leaf("UInt8", "first", 1, true), Leaf("float", "middle", 4, false), Leaf("UInt8", "last", 1, true) };
            var data = new byte[12];
            data[0] = 1;
            BitConverter.GetBytes(2.5f).CopyTo(data, 4);
            data[8] = 0;
            var target = ScriptableObject.CreateInstance<BoolSample>();
            ScriptData.ReadFieldsByTree(target, SerializedLayout.ScriptFields(typeof(BoolSample)), nodes, new DataReader(data), null);
            Assert.True(target.first);
            Assert.Equal(2.5f, target.middle);
            Assert.False(target.last);
        }
    }
}
