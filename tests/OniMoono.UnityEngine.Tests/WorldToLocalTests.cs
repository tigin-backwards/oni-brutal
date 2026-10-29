using System;
using OniMoono.Engine;
using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    /// worldToLocalMatrix as logged in the original player (tools/UnityProbe InverseScale.cs,
    /// `invscale` scenario), as float bits in hex. Inverses: "scale,inverse" pairs (IS1, the
    /// edges from IS6). Chains: each level "localPosition;localRotation;localScale" (read back),
    /// then the leaf's worldToLocalMatrix, all 16 elements by rows (IS3: UI-like chains, IS4:
    /// general rotations and non-uniform, negative scales).
    public class WorldToLocalTests : EngineTest
    {
        static readonly string[] Inverses =
        {
            "c006aa5a,bef35436",
            "431a91dd,3bd3feaf",
            "403b3836,3eaf0642",
            "3dd03542,411d618c",
            "42d28bf6,3c1ba206",
            "3dade198,413c7353",
            "3e1f3319,40cdd466",
            "c3018d82,bbfcee84",
            "40ed1f2e,3e0a30cb",
            "c3edbf6a,bb09d3a8",
            "3f66ef8e,3f8de46b",
            "3b8d87f4,4367866f",
            "bb80212c,c37fbdba",
            "425e118e,3c938eda",
            "c1232de9,bdc8cf4c",
            "43d0efbe,3b1cd514",
            "3feb3e87,3f0b4b25",
            "42c17322,3c29633f",
            "c181da92,bd7c5868",
            "3b8f3178,4364d66f",
            "415cf784,3d944b33",
            "3e1a6db0,40d43058",
            "c120ae4d,bdcbeea5",
            "3f86eab0,3f72e02d",
            "c1282a52,bdc2db1b",
            "3ff49c8d,3f05f598",
            "c13640f3,bdb3cb17",
            "c2f4fb22,bc05c1e0",
            "00000000,00000000",
            "80000000,00000000",
            "00000001,00000000",
            "00000001,00000000",
            "000116c2,00000000",
            "006ce3ee,00000000",
            "00800000,00000000",
            "0da24260,00000000",
            "15f79688,00000000",
            "1c971da0,00000000",
            "1e3ce508,00000000",
            "1fec1e4a,00000000",
            "219392ef,00000000",
            "233877aa,00000000",
            "24e69595,00000000",
            "26901d7d,00000000",
            "283424dc,00000000",
            "29e12e13,00000000",
            "2b8cbccc,00000000",
            "2d2febff,00000000",
            "2edbe6ff,00000000",
            "3089705f,4e6e6b29",
            "322bcc77,4cbebc21",
            "33d6bf95,4b189680",
            "358637bd,49742400",
            "3727c5ac,47c35000",
            "7149f2ca,0da24260",
            "7cf0bdc2,02081cea",
            "7e967699,00000000",
            "7f61b1e6,00000000",
            "7f7fffff,00000000",
            "7f800000,00000000",
            "ff800000,80000000",
            "ffc00000,ffc00000",
            "9e3ce508,00000000",
            "aedbe6ff,00000000",
            "00800000,00000000",
            "00c00000,00000000",
            "00ffffff,00000000",
            "01000000,00000000",
            "01400000,00000000",
            "017fffff,00000000",
            "2f800000,00000000",
            "2fc00000,00000000",
            "2fffffff,00000000",
            "30000000,00000000",
            "30400000,00000000",
            "307fffff,00000000",
            "30800000,00000000",
            "30c00000,4e2aaaab",
            "30ffffff,4e000001",
            "3e800000,40800000",
            "3ec00000,402aaaab",
            "3effffff,40000001",
            "3f000000,40000000",
            "3f400000,3faaaaab",
            "3f7fffff,3f800001",
            "7f000000,00000000",
            "7f400000,00000000",
            "7f7fffff,00000000",
            "3089705c,00000000",
            "3089705d,00000000",
            "3089705e,00000000",
            "3089705f,4e6e6b29",
            "30897060,4e6e6b27",
            "30897061,4e6e6b25",
            "30897062,4e6e6b23",
        };

        static readonly string[][] Chains =
        {
            // IS3 2
            new[] {
                "414df874,bfa281c4,3eec3325;00000000,00000000,00000000,3f800000;43e737e4,43e737e4,3f800000",
                "bd55275f,446b5f4d,43f2d4b6;00000000,00000000,3dbe9de8,3f7ee385;3f800000,3f800000,3f800000",
                "c0e735bf,c0e7803a,40efcfe1;00000000,00000000,3f7d486f,3e14cba0;3f800000,3f800000,3f800000",
                "4088c4d4,4453145f,bf14388d;00000000,00000000,00000000,3f800000;3def4215,3def4215,3f800000",
                "3cc9565a,4023c3d9,40758e4b;00000000,00000000,00000000,3f800000;3f800000,3f800000,3f800000",
                "bc96cbf9,3afed568,00000000,c4672df7,bafed568,bc96cbf9,00000000,4430df33,00000000,00000000,3f800000,c3f87003,00000000,00000000,00000000,3f800000" },
            // IS3 3
            new[] {
                "bf78c5ed,c28e7ff5,c006a1bb;00000000,00000000,00000000,3f800000;3fa99697,3fa99697,3fa99697",
                "c0e42213,3db6f4a2,3edc4488;00000000,00000000,00000000,3f800000;c2ff4de3,c2ff4de3,3f800000",
                "3e766103,3f50d19a,3da298ee;00000000,00000000,00000000,3f800000;3e2de20a,3e2de20a,3f800000",
                "3f8e0495,c45a168f,c43a917b;00000000,00000000,00000000,3f800000;3f800000,3f800000,3f800000",
                "c04d2669,c101cdfa,beeacb8b;00000000,00000000,00000000,3f800000;3f800000,3f800000,3f800000",
                "43852ee1,3d951451,c0af8602;00000000,00000000,3f154ab7,3f4ff61e;3dbfdc6f,3dbfdc6f,3f800000",
                "bdf37366,beb44e70,00000000,45f7914b,3eb44e70,bdf37366,00000000,45b13be5,00000000,00000000,3f413890,443c52e0,00000000,00000000,00000000,3f800000" },
            // IS3 4
            new[] {
                "c28d8d09,42704322,3f614fa0;00000000,00000000,00000000,3f800000;3c4ffca8,3c4ffca8,3c4ffca8",
                "c0e2ad84,c030078b,c472369a;00000000,00000000,3f3d03ad,3f2ca78e;3f800000,3f800000,3f800000",
                "bf378850,bedecc0b,c2c2ad08;00000000,00000000,00000000,3f800000;3f800000,3f800000,3f800000",
                "3f2a6c03,4453f38a,bd8ef012;00000000,00000000,3f5ddafd,beff7837;40b8c8d7,40b8c8d7,3f800000",
                "bd3c6322,bd0dc0eb,c4224e44;00000000,00000000,00000000,3f800000;3f800000,3f800000,3f800000",
                "441542e0,3f15a6e7,c465032b;00000000,00000000,00000000,3f800000;3f800000,3f800000,3f800000",
                "4145e2e0,c0b830e7,00000000,443bf934,40b830e7,4145e2e0,00000000,c382b5d0,00000000,00000000,429d8c61,4520233d,00000000,00000000,00000000,3f800000" },
            // IS3 5
            new[] {
                "bd924221,bf71e10b,c0da111f;00000000,00000000,00000000,3f800000;3f84ad11,3f84ad11,3f84ad11",
                "bfe3fc1b,4441ff59,3f77e682;00000000,00000000,00000000,3f800000;3f800000,3f800000,3f800000",
                "4065932d,c29d792f,c1064c3d;00000000,00000000,3d709aee,bf7f8ed6;3f800000,3f800000,3f800000",
                "c20efcb5,3f55f241,bf390e21;00000000,00000000,00000000,3f800000;422ec251,422ec251,3f800000",
                "3cb3a5a9,bb29b93d,00000000,4029782d,3b29b93d,3cb3a5a9,00000000,c17da24c,00000000,00000000,3f76fa3f,416b8f86,00000000,00000000,00000000,3f800000" },
            // IS3 6
            new[] {
                "c18c1e60,437cb47e,41730da7;00000000,00000000,00000000,3f800000;3eccb5d4,3eccb5d4,3eccb5d4",
                "bcf56d9a,c05193ea,c11ac9ca;00000000,00000000,00000000,3f800000;3f800000,3f800000,3f800000",
                "bd76d34f,3d408208,c45eee4c;00000000,00000000,3f3a289e,bf2fbad2;3f800000,3f800000,3f800000",
                "bdc48e3a,4223bd82,42b5bbe0;00000000,00000000,00000000,3f800000;3f800000,3f800000,3f800000",
                "403acdc7,c2842c76,c262dd78;00000000,00000000,00000000,3f800000;3f800000,3f800000,3f800000",
                "bf953763,3f9d4dcb,3f67390f;00000000,00000000,3f710929,beac7c19;3f800000,3f800000,3f800000",
                "bfbc80d7,4001612b,00000000,c408ff67,c001612b,bfbc80d7,00000000,439d9d62,00000000,00000000,402011f4,444f1667,00000000,00000000,00000000,3f800000" },
            // IS3 11
            new[] {
                "4421af98,4446b9b6,4115338c;00000000,00000000,3ee4b43c,bf650a68;3f660f20,3f660f20,3f660f20",
                "3da3caaf,c2ba12d8,3dc990da;00000000,00000000,00000000,3f800000;3f5ddc19,3f5ddc19,3f800000",
                "bce1befa,bf372967,bdc8c477;00000000,00000000,3f7fd2bf,3d18317c;3f800000,3f800000,3f800000",
                "c0c72665,3d2e38f6,c062596e;00000000,00000000,00000000,3f800000;3f800000,3f800000,3f800000",
                "408475d8,c20c8f4b,41077276;00000000,00000000,3f492486,3f1e5bf2;3f800000,3f800000,3f800000",
                "c0c55796,3e84c548,c4546bba;00000000,00000000,00000000,3f800000;3f800000,3f800000,3f800000",
                "bf634b2c,bf6d732e,00000000,449b9540,3f6d732e,bf634b2c,00000000,42e19567,00000000,00000000,3f8e6ed8,44509828,00000000,00000000,00000000,3f800000" },
            // IS4 3
            new[] {
                "4254a6d8,3db7e826,beb35f01;00000000,00000000,3e0919b3,bf7db1f4;c25b79e8,3d1565b7,bd53293b",
                "bda2b635,42a37eab,40ef3be7;00000000,00000000,3f7303f4,3ea0fd9b;3eda90fd,42e67145,3ff24a39",
                "bdaf30c7,bf7091dc,c15b0c6d;00000000,00000000,3ede3e2e,bf66a014;422c4f84,bba3c2f3,3d3a3cb8",
                "3e1824ab,3f09b841,00000000,c11a111b,c4c6e15f,c5b40423,00000000,47c8cecb,00000000,00000000,c3616123,430747a2,00000000,00000000,00000000,3f800000" },
            // IS4 6
            new[] {
                "c0550d64,43c08014,3f68c8be;00000000,00000000,3f702902,3eb14d8f;3f800000,3f800000,3f800000",
                "bd108eb3,bd0aaf61,bfddad8b;bee61208,3ecefb33,be845f64,3f40e76e;40f33b44,3fb2a4f5,3c6ad00f",
                "4421943e,3b4f7a02,c0236ce8;00000000,00000000,00000000,3f800000;3f800000,3f800000,3f800000",
                "3d12fe51,bd4a7751,3d318d53;00000000,00000000,00000000,3f800000;3f800000,3f800000,3f800000",
                "c2885e0d,bd598136,4106b6f1;00000000,00000000,3f575cb2,bf0a66f6;3f800000,3f800000,3f800000",
                "428f35d7,c106170d,3eba4ca3;00000000,00000000,3f704dcb,3eb085c1;40141596,42ea20ba,3b933fea",
                "bd1bde36,3b689568,be0b28cc,c349e5cc,baedbfec,bb165269,bb9b0bd1,4053f568,c6650302,45394a16,4582ac80,c990f309,00000000,00000000,00000000,3f800000" },
            // IS4 8
            new[] {
                "4290a80c,43dc9400,441df097;3de350f0,bf26bbea,3f3a5e9c,be3b64a2;3e678483,c370e92c,434b9234",
                "c3d5a196,41391190,bdb0b227;00000000,00000000,00000000,3f800000;3de01d23,3faa5203,be133a8b",
                "3c968d76,3e40ac82,be8c370a;00000000,00000000,00000000,3f800000;c38ffc37,3e18d589,3da2b573",
                "43168b66,bf2caccb,c1e19220;00000000,00000000,00000000,3f800000;3f800000,3f800000,3f800000",
                "bd980b9e,3dd2e51d,41a46ef8;00000000,00000000,3f49f7e5,3f1d4e06;41b1d553,c0ff6e11,3ff62e9c",
                "bac6a1d1,ba125b28,3a4d388a,bf66fd8c,3c7c7313,3be765f1,3afea6a8,c1d98270,bdb72a0e,3e4fd347,bce88f8d,c27d2b75,00000000,00000000,00000000,3f800000" },
            // IS4 9
            new[] {
                "3fbfe07b,c2c2a893,c0aaf64b;3c2c1da0,3f0d8bec,3f533c92,3dec4bb0;c024d4e6,3fb72443,406f0b96",
                "40428715,3fdf366d,c109b659;00000000,00000000,3f665ba2,3edf5967;438de544,3ce2f9fa,3d6ebfa2",
                "3c8a00b1,3ec18ee8,bf35e254;00000000,00000000,3f7924a1,3e6b69ee;41cc9c83,40588d26,bd367f32",
                "be0db820,3e0d278c,be8af226,4165b58f,4005db2f,c005404b,4083271e,c358d8de,c16f0cf7,c2bba2e5,c2202421,c645b37f,00000000,00000000,00000000,3f800000" },
            // IS4 10
            new[] {
                "42bac552,bf7ed033,c1146119;00000000,00000000,00000000,3f800000;3e467106,bd7b420b,3f6009f2",
                "3e128e0c,3c3a72e6,4119dfb7;bec0edd4,bf34d69f,bee0235b,bed1765b;3f800000,3f800000,3f800000",
                "c2c16ae3,bd42a4a9,c1cc8616;00000000,00000000,3d51508f,bf7faa5f;becc5090,3fa00178,410ba644",
                "40a44ec9,420b366a,3f7a3d33,c42b4d1d,3f0df96b,c0b01097,3f51d1d6,c2429892,3f0969a1,bf144e19,bd17354a,c23f4875,00000000,00000000,00000000,3f800000" },
            // IS4 11
            new[] {
                "3dc7183e,bdb431d3,c2a565ac;00000000,00000000,00000000,3f800000;3f800000,3f800000,3f800000",
                "c2115e68,3d244b5a,3dba7504;bece6517,be896051,bf538851,be934591;c3c72179,3c6ed289,42f1e66b",
                "4261b6f4,411084b8,be729954;00000000,00000000,00000000,3f800000;3cc4a022,bf0d9848,4362e892",
                "c46bbed9,3f272f80,c10adacc;00000000,00000000,3f7e59eb,bde80fa8;bf3ce70b,bc68701c,3d0f6a1e",
                "424725c9,4313591c,3d82c1ce;bf13e102,3ec9a72d,3ebcfc5c,3f1cc1f0;3f800000,3f800000,3f800000",
                "c01cff65,c0d5501e,40ce352d,c4218158,42f76a9a,43a5203a,c3a159ff,c6957c6a,4509a354,45b7915d,c5b37590,c8b53a9b,00000000,00000000,00000000,3f800000" },
        };

        static float[] Floats(string hex) => Array.ConvertAll(hex.Split(','), h => BitConverter.Int32BitsToSingle(Convert.ToInt32(h, 16)));
        static Vector3 V3(string hex) { var f = Floats(hex); return new Vector3(f[0], f[1], f[2]); }
        static Quaternion Q(string hex) { var f = Floats(hex); return new Quaternion(f[0], f[1], f[2], f[3]); }
        static string Hex(float f) => BitConverter.SingleToInt32Bits(f).ToString("x8");

        static string All(Matrix4x4 m)
        {
            var s = new string[16];
            for (int i = 0; i < 16; i++) s[i] = Hex(m[i / 4, i % 4]);
            return string.Join(",", s);
        }

        /// The inverse scale: not 1/s rounded in about a third of values, and 0 at the edges.
        [Fact]
        public void InverseScaleIsTheOriginals()
        {
            foreach (var pair in Inverses)
            {
                var f = pair.Split(',');
                Assert.Equal(f[1], Hex(InverseScale.Of(Floats(f[0])[0])));
            }
        }

        /// The leaf's worldToLocalMatrix: each level's inverse, multiplied from the leaf up.
        [Fact]
        public void WorldToLocalMatchesTheOriginalBitForBit()
        {
            foreach (var chain in Chains)
            {
                Transform parent = null, root = null;
                for (int i = 0; i < chain.Length - 1; i++)
                {
                    var f = chain[i].Split(';');
                    var t = new GameObject("level").transform;
                    root ??= t;
                    if (parent != null) t.SetParent(parent, false);
                    t.localPosition = V3(f[0]);
                    t.localRotation = Q(f[1]);
                    t.localScale = V3(f[2]);
                    parent = t;
                }
                Assert.Equal(chain[^1], All(parent.worldToLocalMatrix));
                UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
        }
    }
}
