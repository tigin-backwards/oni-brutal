# newgame (scripts/m9-scenarios) with its QA tolerance.
source "$(dirname "${BASH_SOURCE[0]}")/../m9-scenarios/newgame.sh"
TOLERANCE=0.16 # three random Duplicants each run, and the story trait the panel shows depends on timing
