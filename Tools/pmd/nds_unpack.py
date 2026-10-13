#!/usr/bin/env python3
"""Unpack the filesystem of a Nintendo DS ROM (pure stdlib).

Usage: python3 -I nds_unpack.py <rom.nds> <out_dir>
Writes the NitroFS tree under <out_dir>/data, plus arm9.bin and overlay/*.bin.
"""
import struct
import sys
from pathlib import Path


def u16(b, o):
    return struct.unpack_from("<H", b, o)[0]


def u32(b, o):
    return struct.unpack_from("<I", b, o)[0]


def safe_name(name):
    if name in ("", ".", "..") or "/" in name or "\\" in name or "\0" in name:
        raise ValueError(f"unsafe name in ROM: {name!r}")
    return name


def main(rom_path, out_dir):
    rom = Path(rom_path).read_bytes()
    out = Path(out_dir)
    title = rom[0:12].rstrip(b"\0").decode("ascii", "replace")
    code = rom[12:16].decode("ascii", "replace")
    print(f"title={title} gamecode={code} size={len(rom)}")

    fnt_off, fat_off = u32(rom, 0x40), u32(rom, 0x48)

    def file_bytes(fid):
        start, end = u32(rom, fat_off + fid * 8), u32(rom, fat_off + fid * 8 + 4)
        return rom[start:end]

    count = 0

    def walk(dir_id, path):
        nonlocal count
        entry = fnt_off + (dir_id & 0xFFF) * 8
        sub = fnt_off + u32(rom, entry)
        fid = u16(rom, entry + 4)
        path.mkdir(parents=True, exist_ok=True)
        while True:
            length = rom[sub]
            sub += 1
            if length == 0:
                return
            n = length & 0x7F
            name = safe_name(rom[sub:sub + n].decode("ascii", "replace"))
            sub += n
            if length & 0x80:
                child = u16(rom, sub)
                sub += 2
                walk(child, path / name)
            else:
                (path / name).write_bytes(file_bytes(fid))
                fid += 1
                count += 1

    walk(0xF000, out / "data")

    arm9_off, arm9_size = u32(rom, 0x20), u32(rom, 0x2C)
    (out / "arm9.bin").write_bytes(rom[arm9_off:arm9_off + arm9_size])

    ovt_off, ovt_size = u32(rom, 0x50), u32(rom, 0x54)
    ov_dir = out / "overlay"
    ov_dir.mkdir(exist_ok=True)
    for i in range(ovt_size // 32):
        e = ovt_off + i * 32
        (ov_dir / f"overlay_{u32(rom, e):04d}.bin").write_bytes(file_bytes(u32(rom, e + 24)))

    print(f"files={count} overlays={ovt_size // 32}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
