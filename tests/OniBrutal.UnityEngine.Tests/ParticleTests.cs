using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OniBrutal.Assets.Files;
using OniBrutal.Assets.TypeTrees;
using OniBrutal.Engine.Assets;
using OniBrutal.Engine.Particles;
using OniBrutal.Engine.Rendering;
using OniBrutal.Engine.Serialization;
using UnityEngine;
using Xunit;
using Object = UnityEngine.Object;

namespace OniBrutal.Tests
{
    public class ParticleTests : EngineTest
    {
        static string DataDir
        {
            get
            {
                var game = Environment.GetEnvironmentVariable("ONI_GAME_DIR")
                           ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "projects", "oni-linux");
                var dir = Path.Combine(game, "OxygenNotIncluded_Data");
                if (!Directory.Exists(dir)) throw new InvalidOperationException($"Game data not found at {dir}; set ONI_GAME_DIR.");
                return dir;
            }
        }

        sealed class FileResolver(SerializedFile file) : IReferenceResolver
        {
            public Object Resolve(PPtr pptr) => ObjectLoader.Resolve(file, pptr);
        }

        /// The game's particle components (ParticleSystem class 198, ParticleSystemRenderer 199)
        /// by owning GameObject. Their prefabs carry game scripts the tests cannot load, so the
        /// components' data is bound onto test GameObjects instead.
        static Dictionary<long, (Fields System, Fields Renderer)> GameParticleData(out SerializedFile file)
        {
            AssetSystem.Initialize(DataDir);
            file = AssetSystem.Get("sharedassets0.assets");
            var byOwner = new Dictionary<long, (Fields System, Fields Renderer)>();
            foreach (var info in file.Objects)
            {
                int classId = file.TypeOf(info).ClassId;
                if (classId is not (198 or 199)) continue;
                var f = TypeTreeReader.ReadNative(classId, file.OpenObject(info));
                long owner = f.PPtr("m_GameObject").PathId;
                byOwner.TryGetValue(owner, out var entry);
                byOwner[owner] = classId == 198 ? (f, entry.Renderer) : (entry.System, f);
            }
            return byOwner;
        }

        /// A scene GameObject carrying the particle system of the given game prefab GameObject.
        static ParticleSystem FromGame(long gameObjectPathId)
        {
            var data = GameParticleData(out var file)[gameObjectPathId];
            var go = new GameObject("fx" + gameObjectPathId);
            go.SetActive(false);
            var ps = go.AddComponent<ParticleSystem>();
            var resolver = new FileResolver(file);
            ps.Data.Bind(data.System, resolver);
            ps.GetComponent<ParticleSystemRenderer>().Native.Bind(data.Renderer, resolver);
            go.SetActive(true);
            return ps;
        }

        static readonly long[] GameEffects = [9843, 9341, 9342, 9343, 9886, 9887, 11829, 10330, 10331, 9792, 10135, 10136];

        /// A system with Unity's AddComponent defaults, then adjusted.
        static ParticleSystem Make(Action<ParticleConfig> configure, bool play = true)
        {
            var go = new GameObject("ps");
            go.SetActive(false);
            var ps = go.AddComponent<ParticleSystem>();
            configure(ps.Data.Config);
            go.SetActive(true);
            if (!play) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        static void Frames(int n, float dt = 1f / 60f)
        {
            for (int i = 0; i < n; i++) Frame(dt);
        }

        [Fact]
        public void EveryParticleSystemInTheGameBinds()
        {
            var data = GameParticleData(out _);
            Assert.Equal(12, data.Count);
            Assert.All(data.Values, d => Assert.NotNull(d.System));
            Assert.All(data.Values, d => Assert.NotNull(d.Renderer));
            foreach (long id in GameEffects)
            {
                var ps = FromGame(id);
                Assert.True(ps.main.duration > 0f);
                Assert.Equal(1, ps.subEmitters.subEmittersCount);
                Assert.Null(ps.subEmitters.GetSubEmitterSystem(0));
                // Every game effect plays on awake.
                Assert.True(ps.isPlaying);
                Assert.NotNull(ps.GetComponent<ParticleSystemRenderer>().sharedMaterial);
            }
        }

        [Fact]
        public void AddComponentAddsARendererAndUnityDefaults()
        {
            var go = new GameObject("ps");
            var ps = go.AddComponent<ParticleSystem>();
            Assert.NotNull(go.GetComponent<ParticleSystemRenderer>());
            Assert.Equal(5f, ps.main.duration);
            Assert.True(ps.main.loop);
            Assert.True(ps.isPlaying);
            Assert.True(ps.useAutoRandomSeed);
            Assert.Equal(ParticleSystemStopAction.None, ps.main.stopAction);
        }

        [Fact]
        public void MeteorShowerBackgroundEmitsAtItsRate()
        {
            var live = FromGame(9843); // fx_meteorshower_bg: looping, 10/s, lifetime 5 s
            Assert.True(live.main.loop);
            Assert.Equal(5f, live.main.duration);
            Assert.True(live.IsAlive());
            Frames(60);
            Assert.InRange(live.particleCount, 9, 11);
            Frames(60 * 4);
            Assert.InRange(live.particleCount, 49, 51);
            // Lifetime 5 s: births and deaths now balance.
            Frames(60 * 3);
            Assert.InRange(live.particleCount, 49, 51);
            Assert.InRange(live.time, 0f, 5f);
        }

        [Fact]
        public void ParticlesExpireAfterTheirLifetime()
        {
            var ps = Make(c =>
            {
                c.Looping = false;
                c.Duration = 1f;
                c.StartLifetime = MinMaxCurveData.Const(0.5f);
                c.RateOverTime = MinMaxCurveData.Const(20f);
            });
            Frames(61);
            Assert.False(ps.isEmitting);
            Assert.True(ps.isPlaying);
            Assert.InRange(ps.particleCount, 9, 11);
            Frames(31);
            Assert.Equal(0, ps.particleCount);
            Assert.True(ps.isStopped);
            Assert.False(ps.IsAlive());
            Assert.Equal(0f, ps.time);
        }

        [Fact]
        public void BurstsFireAtTheirTimesAndRepeat()
        {
            var ps = Make(c =>
            {
                c.RateOverTime = MinMaxCurveData.Const(0f);
                c.StartLifetime = MinMaxCurveData.Const(10f);
                c.Bursts = [new BurstData { Time = 0.5f, Count = MinMaxCurveData.Const(7f), CycleCount = 2, RepeatInterval = 0.25f, Probability = 1f }];
            });
            Frames(29); // 0.483 s
            Assert.Equal(0, ps.particleCount);
            Frames(2); // 0.517 s
            Assert.Equal(7, ps.particleCount);
            Frames(13); // 0.733 s
            Assert.Equal(7, ps.particleCount);
            Frames(2); // 0.767 s
            Assert.Equal(14, ps.particleCount);
            Frames(60);
            Assert.Equal(14, ps.particleCount);
            // The next loop (duration 5 s) fires them again.
            Frames(60 * 4);
            Assert.Equal(28, ps.particleCount);
        }

        [Fact]
        public void StopLetsParticlesDieThenRunsTheStopAction()
        {
            var ps = Make(c => c.StartLifetime = MinMaxCurveData.Const(1f));
            var go = ps.gameObject;
            Frames(30);
            Assert.True(ps.particleCount > 0);
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.Destroy;
            ps.Stop();
            Assert.False(ps.isEmitting);
            Assert.True(ps.isPlaying);
            Assert.True(ps.IsAlive());
            Frames(70);
            Assert.True(go == null);
        }

        [Fact]
        public void StopWithClearStopsImmediately()
        {
            var ps = Make(_ => { });
            Frames(30);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Assert.Equal(0, ps.particleCount);
            Assert.True(ps.isStopped);
            Assert.False(ps.IsAlive());
            ps.Play();
            Assert.True(ps.isPlaying);
        }

        [Fact]
        public void WithChildrenReachesChildSystems()
        {
            var parent = Make(_ => { });
            var child = Make(_ => { });
            child.transform.SetParent(parent.transform, false);
            Frames(30);
            parent.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            Assert.True(child.isPlaying);
            Assert.True(parent.IsAlive());
            Assert.False(parent.IsAlive(false));
            parent.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Assert.False(parent.IsAlive());
        }

        [Fact]
        public void DisabledEmissionEmitsNothing()
        {
            var ps = Make(_ => { });
            var emission = ps.emission;
            emission.enabled = false;
            Frames(60);
            Assert.Equal(0, ps.particleCount);
            emission.enabled = true;
            Frames(60);
            Assert.InRange(ps.particleCount, 9, 11);
        }

        [Fact]
        public void DeactivationClearsAndReactivationReplays()
        {
            var ps = Make(_ => { });
            Frames(60);
            Assert.True(ps.particleCount > 0);
            ps.gameObject.SetActive(false);
            Assert.Equal(0, ps.particleCount);
            Assert.True(ps.isStopped);
            ps.gameObject.SetActive(true);
            Assert.True(ps.isPlaying);
        }

        [Fact]
        public void SimulateFastForwardsAndPauses()
        {
            var ps = Make(c => c.StartLifetime = MinMaxCurveData.Const(10f), play: false);
            ps.useAutoRandomSeed = false;
            ps.randomSeed = 1234;
            ps.Simulate(2f, true, true, true);
            Assert.True(ps.isPaused);
            Assert.False(ps.isPlaying);
            Assert.InRange(ps.time, 1.99f, 2.01f);
            Assert.InRange(ps.particleCount, 19, 21);
            var first = ps.Data.Particles.ToArray().Select(p => p.Position).ToArray();
            Frames(10);
            Assert.Equal(first.Length, ps.particleCount);

            // A fixed seed replays identically.
            ps.Simulate(2f, true, true, true);
            Assert.Equal(first, ps.Data.Particles.ToArray().Select(p => p.Position).ToArray());

            ps.Play();
            Assert.True(ps.isPlaying);
        }

        [Fact]
        public void ShapeModuleOffsetsAndScalesBirthPositions()
        {
            var ps = Make(c =>
            {
                c.Shape.Type = ShapeKind.Box;
                c.StartSpeed = MinMaxCurveData.Const(0f);
                c.RateOverTime = MinMaxCurveData.Const(100f);
            });
            var shape = ps.shape;
            shape.position = new Vector3(10f, 0f, 0f);
            shape.scale = new Vector3(2f, 4f, 0f);
            Assert.Equal(new Vector3(10f, 0f, 0f), shape.position);
            Frames(30);
            Assert.True(ps.particleCount > 20);
            foreach (var p in ps.Data.Particles)
            {
                Assert.InRange(p.Position.x, 9f, 11f);
                Assert.InRange(p.Position.y, -2f, 2f);
                Assert.Equal(0f, p.Position.z, 4);
            }
        }

        [Fact]
        public void WorldSpaceParticlesStayBehindAMovingEmitter()
        {
            var ps = Make(c =>
            {
                c.SimulationSpace = 1;
                c.StartSpeed = MinMaxCurveData.Const(0f);
                c.Shape.Enabled = false;
            });
            Frames(30);
            int before = ps.particleCount;
            Assert.True(before > 0);
            ps.transform.position = new Vector3(100f, 0f, 0f);
            Frames(1);
            Assert.Equal(before, ps.Data.Particles.ToArray().Count(p => p.Position.x == 0f));
        }

        [Fact]
        public void RateOverDistanceEmitsAsTheEmitterMoves()
        {
            var ps = Make(c =>
            {
                c.SimulationSpace = 1;
                c.EmitterVelocityMode = 0;
                c.RateOverTime = MinMaxCurveData.Const(0f);
                c.RateOverDistance = MinMaxCurveData.Const(10f);
                c.StartLifetime = MinMaxCurveData.Const(100f);
                c.StartSpeed = MinMaxCurveData.Const(0f);
                c.Shape.Enabled = false;
            });
            Frames(10);
            Assert.Equal(0, ps.particleCount);
            for (int i = 1; i <= 60; i++)
            {
                ps.transform.position = new Vector3(i * 0.1f, 0f, 0f);
                Frame();
            }
            Assert.InRange(ps.particleCount, 59, 61);
            // Births are spread along the emitter's path.
            var xs = ps.Data.Particles.ToArray().Select(p => p.Position.x).OrderBy(x => x).ToArray();
            Assert.True(xs.Zip(xs.Skip(1), (a, b) => b - a).Max() < 0.15f);
        }

        [Fact]
        public void BillboardGeometryFacesTheCamera()
        {
            var ps = Make(c =>
            {
                c.StartSpeed = MinMaxCurveData.Const(0f);
                c.StartSizeX = MinMaxCurveData.Const(2f);
                c.Shape.Enabled = false;
            });
            Frames(30);
            int n = ps.particleCount;
            Assert.True(n > 0);
            var renderer = (ParticleSystemRendererNative)ps.GetComponent<ParticleSystemRenderer>().Native;
            var mesh = new MeshNative();
            var view = new ParticleView(new Vector3(0f, 0f, -10f), Quaternion.identity, null);
            Assert.True(renderer.BuildGeometry(view, mesh, out var objectToWorld));
            Assert.Equal(Matrix4x4.identity, objectToWorld);
            Assert.Equal(4 * n, mesh.VertexCount);
            Assert.Equal(6 * n, mesh.SubMeshes[0].Indices.Length);
            Assert.All(mesh.Positions, p => Assert.Equal(0f, p.z, 4));
            Assert.Equal(2f, mesh.Bounds.size.x, 3);
            Assert.Equal(2f, mesh.Bounds.size.y, 3);
            Assert.All(mesh.Normals, nrm => Assert.Equal(Vector3.back, nrm));
            // Renderer bounds hold the quad at any roll: its diagonal.
            Assert.Equal(2f * Mathf.Sqrt(2f), ps.GetComponent<ParticleSystemRenderer>().bounds.size.x, 3);

            // An orthographic camera with size 1 (viewport 2 units high) clamps the 2-unit
            // particles to maxParticleSize 0.5 × 2 = 1 unit.
            var ortho = Matrix4x4.Ortho(-1f, 1f, -1f, 1f, 0.3f, 100f);
            Assert.True(renderer.BuildGeometry(new ParticleView(new Vector3(0f, 0f, -10f), Quaternion.identity, ortho), mesh, out _));
            Assert.Equal(1f, mesh.Bounds.size.x, 3);

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Assert.False(renderer.BuildGeometry(view, mesh, out _));
        }

        [Fact]
        public void GameSystemsRunAndBuildGeometry()
        {
            foreach (long id in GameEffects)
            {
                var ps = FromGame(id);
                for (int i = 0; i < 90; i++)
                {
                    // Moving, for the effects that emit over distance.
                    ps.transform.position = new Vector3(i * 0.05f, 0f, 0f);
                    Frame();
                }
                Assert.True(ps.particleCount > 0, $"effect {id} emitted nothing");
                var renderer = (ParticleSystemRendererNative)ps.GetComponent<ParticleSystemRenderer>().Native;
                var mesh = new MeshNative();
                Assert.True(renderer.BuildGeometry(new ParticleView(new Vector3(0f, 0f, -20f), Quaternion.identity, null), mesh, out _));
                Assert.True(mesh.VertexCount >= ps.particleCount * 4);
                Assert.All(mesh.Positions, p => Assert.True(float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z)));
                Object.DestroyImmediate(ps.gameObject);
            }
        }

        [Fact]
        public void TemporalTearBeamDrawsTrails()
        {
            var ps = FromGame(11829);
            Frames(60);
            var renderer = (ParticleSystemRendererNative)ps.GetComponent<ParticleSystemRenderer>().Native;
            var mesh = new MeshNative();
            Assert.True(renderer.BuildGeometry(new ParticleView(new Vector3(0f, 10f, 0f), Quaternion.Euler(90f, 0f, 0f), null), mesh, out _));
            Assert.Equal(2, mesh.SubMeshes.Count);
            Assert.NotEmpty(mesh.SubMeshes[1].Indices);
            // Non-looping 5 s system, lifetime 5 s, trail points lasting 5 s: done after 15 s.
            Assert.True(ps.IsAlive());
            Frames(60 * 13);
            Assert.True(ps.IsAlive());
            Frames(60 * 2 + 5);
            Assert.False(ps.IsAlive());
        }
    }
}
