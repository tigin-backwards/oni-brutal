# HDR check

Checks, on a monitor with HDR switched on, that Moono reaches the screen as bright as the
original does: Windows should show its white UI at the "SDR content brightness" level, not as HDR
content. It runs the original game, Moono presenting through Direct3D (DXGI, the default) and
Moono presenting through OpenGL (`--dxgi off`), one after another, fullscreen on that monitor at
the main menu, and captures the monitor each time in FP16 as Windows composes it.

Nothing is changed on the system: no HDR, display or driver setting. The runs use their own run
homes and the offline Steam double, as the other test runs.

## Running it

1. Close Oxygen Not Included if it is running.
2. Switch HDR on for the monitor (Settings → System → Display → Use HDR).
3. From WSL, in a checkout with the Windows player built (`scripts/build-windows.sh`) and the
   original's isolated copy set up (`scripts/original-windows.sh`):

       tools/HdrCheck/hdr-check.sh 1

   `1` is the monitor, numbered as the game's `-monitor` (1 is the primary one). The monitor
   shows each player for about a minute; leave it alone meanwhile.
4. Switch HDR off again if you want.

The captures and `summary.txt` land in `out/hdr-check/`. The summary has, for each player, the
median luminance (the menu's background) and the mean of the brightest 0.5% of pixels (the white
title and button text), in nits.

## Reading the result

- The two Moono rows should be close to the original's. The brightest 0.5% then sits near the
  "SDR content brightness" slider's level for all three.
- A row much brighter than the original's (hundreds of nits more) means Windows or the driver
  treated that player as HDR content: Auto HDR, RTX HDR, or NVIDIA's "layered on DXGI" OpenGL
  present. Expected, if anything, for the OpenGL row only.
- The `ours-dxgi` row's player must also have said so in its log: the script prints each Moono
  run's `present:` line (DXGI on the adapter, or why OpenGL was used).

## The capture tool

`dupcap.c` (built by `build.cmd` with the Visual Studio C++ build tools) captures one monitor
through DXGI Desktop Duplication, no admin rights needed:

    dupcap X Y sdr OUT.ppm                    8-bit, what an SDR monitor shows
    dupcap X Y fp16 OUT.pfm [RX RY RW RH]     FP16 scRGB (IDXGIOutput5::DuplicateOutput1);
                                              prints the region's mean and maximum in nits

X, Y is any desktop point on the monitor. With HDR off the monitor gives an 8-bit frame even
when FP16 is asked for; dupcap then says so and reports sRGB-decoded values with SDR white as
80 nits.
