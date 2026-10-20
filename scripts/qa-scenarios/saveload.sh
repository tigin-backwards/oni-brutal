# Save and load: the DeCrew colony saved from the pause menu (over its own file, confirmed),
# then that save loaded back through the pause menu's load screen (the colony, its newest save),
# and the reloaded colony captured.
SAVE="${ONI_M9_SAVE:-$HOME/projects/onimaxxing/saves/DeCrew.sav}"
FRAMES=3200
DUMPS="1720 1960 2100 3150"
TOLERANCE=0.06
ARGS=(--input 620:click:891,197 --input 1600:click:511,500 --input 1640:key:Escape --input 1700:click:511,234
      --input 1760:click:511,396 --input 1880:click:511,316 --input 1990:click:300,190
      --input 2120:click:946,494 --input 3050:click:511,500)
