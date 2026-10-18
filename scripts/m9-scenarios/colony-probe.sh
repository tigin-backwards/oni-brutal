# Measurement: the colony scenario up to the Database search frame (1950), with extra host
# arguments from $PROBE (for example --dump-hierarchy or --dump-component).
SAVE="${ONI_M9_SAVE:-$HOME/projects/onimaxxing/saves/DeCrew.sav}"
FRAMES=1960
DUMPS="1950"
read -r -a probe <<< "${PROBE:-}"
ARGS=(--input 620:click:891,197 --input 1600:click:511,500 --input 1750:click:946,28 --input 1880:click:918,160 --input 1950:text:oxygenx "${probe[@]}")
