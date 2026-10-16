#!/usr/bin/env bash
# Conformance: every Unity engine type/member referenced by the consumer assemblies must resolve
# in out/engine (ARCHITECTURE §9). Writes out/apicheck.json and prints a per-module summary.
set -uo pipefail
source "$(dirname "$0")/env.sh"
dotnet build "$root/tools/ApiCheck" -c Release -v q -nologo >/dev/null
providers="$(cut -f1 "$root/src/Facades/unity-module-map.tsv" | sort -u | paste -sd,),UnityEngine,Unity.InternalAPIEngineBridge.004"
dotnet "$root/out/bin/ApiCheck/release/ApiCheck.dll" "$managed" "$root/out/engine" "$providers" \
  Assembly-CSharp.dll Assembly-CSharp-firstpass.dll UnityEngine.UI.dll Unity.TextMeshPro.dll Unity.Timeline.dll \
  FMODUnity.dll FMODUnityResonance.dll FMODUnityHaptics.dll ImGui.NET.dll ImGui.dll com.rlabrecque.steamworks.net.dll \
  --json "$root/out/apicheck.json" > "$root/out/apicheck.txt"
status=$?
head -2 "$root/out/apicheck.txt"
python3 -I - "$root/out/apicheck.json" <<'PY'
import json, sys, collections
d = json.load(open(sys.argv[1]))
mod = lambda k: k.split(']')[0][1:]
t = collections.Counter(mod(k) for k in d['missingTypes'])
m = collections.Counter(mod(k) for k in d['missingMembers'])
for name in sorted(set(t) | set(m)):
    print(f"  {name:45} types {t[name]:4}  members {m[name]:5}")
PY
exit $status
