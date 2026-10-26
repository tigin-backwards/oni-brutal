// Particle systems built by script ("PROBE ... PE ..."), at the main menu (10 s in), as
// ManualMiner (Workshop 3638972299) builds its laser's sparks: AddComponent<ParticleSystem>, the
// main module set to world space without emission, particles made with Emit(EmitParams, count),
// a velocity limit, size and rotation over lifetime and trails. It records:
// - defaults: the modules' values on a new system (limit, dampen, drag, rotation, trails, the
//   renderer's mode), and what a setter's value reads back as;
// - emit: the particle Emit(EmitParams) makes, with every override set and with each one left
//   out (what the main and shape modules then give), on a playing system, a stopped one and one
//   that never played, and how many particles reach maxParticles;
// - limit: a particle's speed over steps of Simulate(dt, true, false, false) of 0.01, 0.02, 0.05
//   and 0.1 s under limits 1 and 3 with dampens 0.2, 0.5 and 1, and with separate axes;
// - rotation: the rotation a constant and a random rotation over lifetime give over time;
// - stopped: isPlaying, isStopped and particleCount over frames after Emit on a stopped system,
//   and whether its particles age there.
// With PROBE_SCENARIO=particleemit only this probe runs; it ends with a ParticleEmitProbeDone
// overlay canvas.
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SemanticsProbe
{
    public class ParticleEmit : MonoBehaviour
    {
        bool started;
        float sceneStart = -1f;

        static void L(string s) => Rec.Log("PE " + s);
        static string F(float f) => f.ToString("R", CultureInfo.InvariantCulture);
        static string V(Vector3 v) => "(" + F(v.x) + " " + F(v.y) + " " + F(v.z) + ")";
        static string C(ParticleSystem.MinMaxCurve c) => c.mode + ":" + F(c.constantMin) + "/" + F(c.constantMax) + "*" + F(c.curveMultiplier);
        static string Col(Color32 c) => c.r + "," + c.g + "," + c.b + "," + c.a;

        void Update()
        {
            if (started || SceneManager.GetActiveScene().name != "frontend") return;
            if (sceneStart < 0f) sceneStart = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - sceneStart < 10f) return;
            started = true;
            StartCoroutine(Run());
        }

        static ParticleSystem NewSystem(string name, bool configure)
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(100f, 200f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            if (configure)
            {
                var main = ps.main;
                main.loop = false;
                main.playOnAwake = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 1000;
                var emission = ps.emission;
                emission.enabled = false;
            }
            return ps;
        }

        static string Particles(ParticleSystem ps)
        {
            var buffer = new ParticleSystem.Particle[ps.particleCount];
            int n = ps.GetParticles(buffer);
            var sb = new System.Text.StringBuilder("n=" + n);
            for (int i = 0; i < n && i < 4; i++)
            {
                var p = buffer[i];
                sb.Append(" [pos=" + V(p.position) + " vel=" + V(p.velocity) + " size=" + F(p.startSize) + " size3=" + V(p.startSize3D)
                    + " life=" + F(p.startLifetime) + " left=" + F(p.remainingLifetime) + " rot=" + F(p.rotation) + " rot3=" + V(p.rotation3D)
                    + " ang=" + F(p.angularVelocity) + " color=" + Col(p.startColor) + " cur=" + F(p.GetCurrentSize(ps)) + " curcolor=" + Col(p.GetCurrentColor(ps)) + "]");
            }
            return sb.ToString();
        }

        static ParticleSystem.EmitParams Full()
        {
            var e = new ParticleSystem.EmitParams();
            e.position = new Vector3(110f, 205f, -3f);
            e.velocity = new Vector3(4f, 3f, 0f);
            e.startSize = 0.25f;
            e.startLifetime = 0.3f;
            e.startColor = new Color(0.1f, 0.9f, 1f, 1f) * 3f;
            e.rotation = 90f;
            return e;
        }

        IEnumerator Run()
        {
            L("=== particle emit");
            // Defaults of a new system.
            var d = NewSystem("Defaults", false);
            var main = d.main;
            L("default main playOnAwake=" + main.playOnAwake + " space=" + main.simulationSpace + " speed=" + C(main.startSpeed) + " size=" + C(main.startSize)
                + " life=" + C(main.startLifetime) + " rot=" + C(main.startRotation) + " max=" + main.maxParticles + " loop=" + main.loop + " playing=" + d.isPlaying
                + " shape=" + d.shape.enabled + "/" + d.shape.shapeType + "/" + F(d.shape.radius) + "/" + F(d.shape.angle));
            var lim = d.limitVelocityOverLifetime;
            L("default limit enabled=" + lim.enabled + " limit=" + C(lim.limit) + " mult=" + F(lim.limitMultiplier) + " dampen=" + F(lim.dampen) + " separate=" + lim.separateAxes
                + " drag=" + C(lim.drag) + " space=" + lim.space + " x=" + C(lim.limitX));
            var rot = d.rotationOverLifetime;
            L("default rotation enabled=" + rot.enabled + " z=" + C(rot.z) + " separate=" + rot.separateAxes + " x=" + C(rot.x));
            var tr = d.trails;
            L("default trails enabled=" + tr.enabled + " ratio=" + F(tr.ratio) + " life=" + C(tr.lifetime) + " minDist=" + F(tr.minVertexDistance) + " sizeWidth=" + tr.sizeAffectsWidth
                + " inherit=" + tr.inheritParticleColor + " width=" + C(tr.widthOverTrail) + " die=" + tr.dieWithParticles + " world=" + tr.worldSpace + " mode=" + tr.mode
                + " texture=" + tr.textureMode + " sizeLife=" + tr.sizeAffectsLifetime);
            {
                var size = d.sizeOverLifetime.size;
                var keys = new System.Text.StringBuilder();
                if (size.curve != null)
                    foreach (var k in size.curve.keys) keys.Append(" " + F(k.time) + ":" + F(k.value) + "/" + F(k.inTangent) + "/" + F(k.outTangent));
                L("default size curve=" + C(size) + " keys" + keys + " separate=" + d.sizeOverLifetime.separateAxes + " sizeMult=" + F(d.sizeOverLifetime.sizeMultiplier));
                L("default gradients colorOverLifetime=" + d.colorOverLifetime.color.mode + "/" + d.colorOverLifetime.color.color + " colorOverTrail=" + tr.colorOverTrail.mode + "/" + tr.colorOverTrail.color
                    + " trailColorOverLifetime=" + tr.colorOverLifetime.mode + "/" + tr.colorOverLifetime.color + " widthMult=" + F(tr.widthOverTrailMultiplier) + " lifeMult=" + F(tr.lifetimeMultiplier));
            }
            L("default size enabled=" + d.sizeOverLifetime.enabled + " color enabled=" + d.colorOverLifetime.enabled
                + " renderMode=" + d.GetComponent<ParticleSystemRenderer>().renderMode);
            // What setters read back as.
            lim.enabled = true;
            lim.limit = new ParticleSystem.MinMaxCurve(1f);
            lim.dampen = 0.2f;
            L("set limit limit=" + C(lim.limit) + " mult=" + F(lim.limitMultiplier) + " dampen=" + F(lim.dampen));
            lim.dampen = 1.5f;
            L("set dampen 1.5 reads " + F(lim.dampen));
            lim.dampen = -1f;
            L("set dampen -1 reads " + F(lim.dampen));
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-720f, 720f);
            L("set rotation z=" + C(rot.z) + " mult=" + F(rot.zMultiplier));
            tr.enabled = true;
            tr.ratio = 0.3f;
            tr.lifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            tr.minVertexDistance = 0.05f;
            tr.ratio = 1.5f;
            L("set trails ratio 1.5 reads " + F(tr.ratio) + " life=" + C(tr.lifetime));
            main.startSpeed = 0f;
            L("set speed 0 reads " + C(main.startSpeed));
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            L("set playOnAwake=" + main.playOnAwake + " space=" + main.simulationSpace);
            Destroy(d.gameObject);
            yield return null;

            // Emit on a playing system: every override, then each left out.
            var a = NewSystem("Emit", true);
            yield return null;
            yield return null;
            L("emit playing=" + a.isPlaying + " stopped=" + a.isStopped + " count=" + a.particleCount);
            a.Emit(Full(), 1);
            L("emit full " + Particles(a));
            a.Clear();
            string[] names = { "position", "velocity", "startSize", "startLifetime", "startColor", "rotation" };
            for (int i = 0; i < names.Length; i++)
            {
                var e = Full();
                if (i == 0) e.ResetPosition();
                if (i == 1) e.ResetVelocity();
                if (i == 2) e.ResetStartSize();
                if (i == 3) e.ResetStartLifetime();
                if (i == 4) e.ResetStartColor();
                if (i == 5) e.ResetRotation();
                a.Emit(e, 1);
                L("emit without " + names[i] + " " + Particles(a));
                a.Clear();
            }
            {
                var e = Full();
                e.applyShapeToPosition = true;
                a.Emit(e, 1);
                L("emit applyShape " + Particles(a));
                a.Clear();
            }
            a.Emit(new ParticleSystem.EmitParams(), 3);
            L("emit nothing set " + Particles(a));
            a.Clear();
            a.Emit(4);
            L("emit count " + Particles(a));
            a.Clear();
            {
                var m = a.main;
                m.maxParticles = 5;
                for (int i = 0; i < 4; i++) a.Emit(Full(), 2);
                L("emit max 5 count=" + a.particleCount);
                m.maxParticles = 1000;
                a.Clear();
            }
            // A playing system's emitted particles over frames.
            a.Emit(Full(), 1);
            for (int f = 0; f < 4; f++)
            {
                yield return null;
                L("emit frame " + f + " dt=" + F(Time.deltaTime) + " playing=" + a.isPlaying + " " + Particles(a));
            }
            yield return new WaitForSeconds(0.5f);
            L("emit after 0.5 s playing=" + a.isPlaying + " stopped=" + a.isStopped + " alive=" + a.IsAlive() + " count=" + a.particleCount);

            // Emit on a stopped system.
            var s = NewSystem("Stopped", true);
            yield return null;
            s.Stop();
            L("stopped after Stop playing=" + s.isPlaying + " stopped=" + s.isStopped + " emitting=" + s.isEmitting);
            yield return null;
            yield return null;
            L("stopped 2 frames later playing=" + s.isPlaying + " stopped=" + s.isStopped);
            s.Emit(Full(), 1);
            L("stopped emit playing=" + s.isPlaying + " stopped=" + s.isStopped + " emitting=" + s.isEmitting + " " + Particles(s));
            for (int f = 0; f < 3; f++)
            {
                yield return null;
                L("stopped frame " + f + " dt=" + F(Time.deltaTime) + " playing=" + s.isPlaying + " stopped=" + s.isStopped + " " + Particles(s));
            }
            yield return new WaitForSeconds(0.5f);
            L("stopped after 0.5 s playing=" + s.isPlaying + " stopped=" + s.isStopped + " count=" + s.particleCount);

            // Emit on a system that never played.
            var n = NewSystem("Never", false);
            {
                var m = n.main;
                m.playOnAwake = false;
            }
            n.gameObject.SetActive(false);
            n.gameObject.SetActive(true);
            yield return null;
            L("never playing=" + n.isPlaying + " stopped=" + n.isStopped);
            n.Emit(Full(), 1);
            L("never emit playing=" + n.isPlaying + " stopped=" + n.isStopped + " " + Particles(n));
            yield return null;
            yield return null;
            L("never 2 frames playing=" + n.isPlaying + " " + Particles(n));

            // Velocity limit, by Simulate steps.
            foreach (var limit in new[] { 1f, 3f })
                foreach (var dampen in new[] { 0.2f, 0.5f, 1f })
                    foreach (var dt in new[] { 0.01f, 0.02f, 0.05f, 0.1f })
                    {
                        var v = NewSystem("Limit", true);
                        var lv = v.limitVelocityOverLifetime;
                        lv.enabled = true;
                        lv.limit = new ParticleSystem.MinMaxCurve(limit);
                        lv.dampen = dampen;
                        var e = Full();
                        e.velocity = new Vector3(10f, 0f, 0f);
                        e.startLifetime = 5f;
                        v.Emit(e, 1);
                        v.Pause();
                        var sb = new System.Text.StringBuilder();
                        for (int k = 0; k < 6; k++)
                        {
                            v.Simulate(dt, true, false, false);
                            var buffer = new ParticleSystem.Particle[1];
                            v.GetParticles(buffer);
                            sb.Append(" " + F(buffer[0].velocity.x) + "@" + F(buffer[0].position.x));
                        }
                        L("limit " + F(limit) + " dampen " + F(dampen) + " dt " + F(dt) + ":" + sb);
                        Destroy(v.gameObject);
                    }
            {
                var v = NewSystem("LimitAxes", true);
                var lv = v.limitVelocityOverLifetime;
                lv.enabled = true;
                lv.limit = new ParticleSystem.MinMaxCurve(1f);
                lv.dampen = 0.5f;
                var e = Full();
                e.velocity = new Vector3(3f, 4f, 0f);
                e.startLifetime = 5f;
                v.Emit(e, 1);
                v.Pause();
                var sb = new System.Text.StringBuilder();
                for (int k = 0; k < 4; k++)
                {
                    v.Simulate(0.02f, true, false, false);
                    var buffer = new ParticleSystem.Particle[1];
                    v.GetParticles(buffer);
                    sb.Append(" " + V(buffer[0].velocity));
                }
                L("limit diagonal 1 dampen 0.5 dt 0.02:" + sb);
                Destroy(v.gameObject);
            }

            // Rotation over lifetime.
            foreach (var curve in new[] { new ParticleSystem.MinMaxCurve(2f), new ParticleSystem.MinMaxCurve(-720f, 720f) })
            {
                var r = NewSystem("Rotation", true);
                var ro = r.rotationOverLifetime;
                ro.enabled = true;
                ro.z = curve;
                var e = Full();
                e.rotation = 0f;
                e.startLifetime = 5f;
                r.Emit(e, 3);
                r.Pause();
                var sb = new System.Text.StringBuilder();
                for (int k = 0; k < 3; k++)
                {
                    r.Simulate(0.05f, true, false, false);
                    var buffer = new ParticleSystem.Particle[3];
                    r.GetParticles(buffer);
                    sb.Append(" [" + F(buffer[0].rotation) + " " + F(buffer[1].rotation) + " " + F(buffer[2].rotation) + " ang " + F(buffer[0].angularVelocity) + "]");
                }
                L("rotation " + C(curve) + ":" + sb);
                Destroy(r.gameObject);
            }

            // Size over lifetime on an emitted size.
            {
                var z = NewSystem("Size", true);
                var so = z.sizeOverLifetime;
                so.enabled = true;
                var curve = new AnimationCurve();
                curve.AddKey(0f, 0.5f);
                curve.AddKey(0.2f, 1f);
                curve.AddKey(1f, 0f);
                so.size = new ParticleSystem.MinMaxCurve(1f, curve);
                var e = Full();
                e.startLifetime = 1f;
                e.startSize = 2f;
                z.Emit(e, 1);
                z.Pause();
                var sb = new System.Text.StringBuilder();
                for (int k = 0; k < 4; k++)
                {
                    z.Simulate(0.1f, true, false, false);
                    var buffer = new ParticleSystem.Particle[1];
                    z.GetParticles(buffer);
                    sb.Append(" " + F(buffer[0].GetCurrentSize(z)));
                }
                L("size over lifetime:" + sb);
                Destroy(z.gameObject);
            }

            Destroy(a.gameObject);
            Destroy(s.gameObject);
            Destroy(n.gameObject);
            var done = new GameObject("ParticleEmitProbeDone", typeof(RectTransform));
            DontDestroyOnLoad(done);
            done.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            L("=== done");
        }
    }
}
