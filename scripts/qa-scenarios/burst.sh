# Clicks arriving together between two frames (a fast double-click at a low frame rate): two
# clicks on Options in one frame open the options screen once, then two on its Graphics button;
# the screens must come up as with single clicks, nothing left held.
FRAMES=900
DUMPS="560 760 880"
ARGS=(--input 420:burst:891,531,2 --input 620:burst:511,262,2 --input 800:click:150,700)
