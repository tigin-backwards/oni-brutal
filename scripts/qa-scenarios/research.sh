# research (scripts/m9-scenarios) with its QA tolerance.
source "$(dirname "${BASH_SOURCE[0]}")/../m9-scenarios/research.sh"
TOLERANCE=0.35 # the tree's zoom and scroll animate in real time (layout is checked by eye); U60 adds
               # a pan term in unscaled time too: 0.15-0.19 in U59 runs, 0.27 in U60's first
