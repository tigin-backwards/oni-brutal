# (Diagnosis) newgame to the Duplicant selection, with the UI packages in QA_UI_PACKAGES if set.
source "$(dirname "${BASH_SOURCE[0]}")/../m9-scenarios/newgame.sh"
FRAMES=2420; DUMPS="2400"; [ -n "${QA_UI_PACKAGES:-}" ] && ARGS+=(--ui-packages "$QA_UI_PACKAGES")
