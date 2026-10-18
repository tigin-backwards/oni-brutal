# A new game from the main menu through embark: Survival, Standard, fixed coordinates typed into
# the destination screen, the story traits and game settings panels (a stepper and a checkbox),
# then start: world generation, the Duplicant selection and embark.
FRAMES=3220
DUMPS="620 800 1150 1250 1350 1450 1800 2400 3200"
ARGS=(--input 420:click:891,223 --input 640:click:428,360 --input 820:click:428,360
      --input 1000:click:335,326 --input 1010:keydown:LeftControl --input 1011:key:A --input 1014:keyup:LeftControl
      --input 1020:text:SNDST-A-12345-0-0-0 --input 1030:key:Return
      --input 1160:click:370,254 --input 1260:click:643,742
      --input 1370:click:686,388 --input 1390:click:483,293
      --input 1460:click:857,232 --input 1480:click:812,742
      --input 2420:click:848,699)
