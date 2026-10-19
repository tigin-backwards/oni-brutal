#!/usr/bin/env python3
"""TEST INFRASTRUCTURE: reference texels for the engine's block decoders (BlockDecodingTests).

Random blocks of each format (BC6H and BC7 with every mode forced in turn), decoded by
texture2ddecoder (AssetStudio's decoders, pip install texture2ddecoder) and, for BC6H and BC7, by
Pillow as a second reference. Output (little-endian): per format, its name (byte length, ASCII),
the block size, the block count, then per block its bytes and the references' 4x4 texels (BGRA8,
row-major, top row first; Pillow's after texture2ddecoder's, RGBA8, or zeros where Pillow has no
decoder).

Usage: make.py OUT
"""
import random, struct, sys
import texture2ddecoder as t
from PIL import Image

rng = random.Random(20261019)

def blocks(size, count, force=None):
    for _ in range(count):
        b = bytearray(rng.getrandbits(8) for _ in range(size))
        if force: force(b)
        yield bytes(b)

def bc7_mode(m):
    def force(b):
        b[0] = (b[0] & ~((2 << m) - 1) & 0xFF) | (1 << m)
    return force

def bc6_mode(bits, width):
    def force(b):
        mask = (1 << width) - 1
        b[0] = (b[0] & ~mask & 0xFF) | bits
    return force

def pillow(block, n):
    im = Image.frombytes("RGBA" if n == 7 else "RGB", (4, 4), block, "bcn", (n,)).convert("RGBA")
    return im.tobytes()

formats = []
bc7 = [b for m in range(8) for b in blocks(16, 64, bc7_mode(m))] + list(blocks(16, 8, lambda b: b.__setitem__(0, 0)))
formats.append(("BC7", 16, bc7, t.decode_bc7, 7))
bc6_codes = [(0, 2), (1, 2)] + [(c, 5) for c in (0x02, 0x06, 0x0A, 0x0E, 0x12, 0x16, 0x1A, 0x1E, 0x03, 0x07, 0x0B, 0x0F, 0x13, 0x17, 0x1B, 0x1F)]
bc6 = [b for c, w in bc6_codes for b in blocks(16, 64, bc6_mode(c, w))]
formats.append(("BC6H", 16, bc6, t.decode_bc6, 6))
for name, size, fn in [("ETC_RGB4", 8, t.decode_etc1), ("ETC2_RGB", 8, t.decode_etc2), ("ETC2_RGBA1", 8, t.decode_etc2a1),
                       ("ETC2_RGBA8", 16, t.decode_etc2a8), ("EAC_R", 8, t.decode_eacr), ("EAC_R_SIGNED", 8, t.decode_eacr_signed),
                       ("EAC_RG", 16, t.decode_eacrg), ("EAC_RG_SIGNED", 16, t.decode_eacrg_signed)]:
    formats.append((name, size, list(blocks(size, 200)), fn, None))

with open(sys.argv[1], "wb") as out:
    for name, size, items, fn, pil in formats:
        out.write(struct.pack("<B", len(name)) + name.encode() + struct.pack("<ii", size, len(items)))
        for block in items:
            out.write(block)
            out.write(fn(block, 4, 4))
            out.write(pillow(block, pil) if pil else bytes(64))
