#!/usr/bin/env bash
# Runs a command with a private ALSA default device that records the mixed output to a file at
# real-time pace (48 kHz stereo s16), never touching the user's sound setup:
#   scripts/audio-sink/record.sh OUT.raw COMMAND [ARGS...]
# ALSA's file plugin pipes the PCM into pace.py, which drains it at the rate of a sound card, so
# FMOD's mixer runs in real time (a plain file sink lets it mix as fast as it can).
set -euo pipefail
out=$(realpath -m "$1"); shift
here=$(cd "$(dirname "$0")" && pwd)
conf=$(mktemp --suffix=.asound.conf)
trap 'rm -f "$conf"' EXIT
cat > "$conf" <<CONF
pcm.!default {
    type file
    slave.pcm "nullsink"
    file "|/usr/bin/python3 $here/pace.py $out"
    format "raw"
}
pcm.nullsink {
    type null
}
CONF
ALSA_CONFIG_PATH="$conf" "$@"
