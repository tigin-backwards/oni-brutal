# The options screens: Graphics (the colour mode and resolution dropdowns opened and closed by the
# blocker, the UI scale slider dragged to 131%, which re-scales the whole UI), back to Options,
# then Audio (a volume slider clicked on its track, another dragged by its handle, a checkbox).
FRAMES=1620
DUMPS="700 800 980 1150 1300 1450 1600"
ARGS=(--input 420:click:891,531 --input 520:click:511,262
      --input 720:click:613,380 --input 830:click:150,700
      --input 900:click:546,241 --input 1000:click:150,700
      --input 1050:press:441,517 --input 1060:move:470,517 --input 1070:move:520,517 --input 1080:release:520,517
      --input 1200:click:511,630 --input 1320:click:511,276
      --input 1470:click:450,267
      --input 1500:press:725,218 --input 1510:move:680,218 --input 1520:move:600,218 --input 1530:release:600,218
      --input 1550:click:251,504)
