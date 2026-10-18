# The DeCrew colony's management screens, opened with their hotkeys and closed with Escape:
# Priorities (L), Vitals (V), Consumables (F), Skills (J), Research (R) and Schedule (.), with
# the scroll wheel used in Priorities and Research.
SAVE="${ONI_M9_SAVE:-$HOME/projects/onimaxxing/saves/DeCrew.sav}"
FRAMES=2620
DUMPS="1720 1860 2000 2140 2290 2440 2600"
ARGS=(--input 620:click:891,197 --input 1600:click:511,500
      --input 1640:key:L --input 1680:move:512,420 --input 1690:scroll:0,-3 --input 1740:key:Escape
      --input 1780:key:V --input 1880:key:Escape
      --input 1920:key:F --input 2020:key:Escape
      --input 2060:key:J --input 2160:key:Escape
      --input 2200:key:R --input 2240:move:512,400 --input 2250:scroll:0,-5 --input 2310:key:Escape
      --input 2350:key:Period --input 2460:key:Escape)
