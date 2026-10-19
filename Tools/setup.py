#!/usr/bin/env python3
"""Rebuild every asset taken from the ROM, which the Git repository does not contain.

Usage (from the project root, Unity closed):  python3 Tools/setup.py [--unity <path to the Unity executable>]

Needs Rom/eos_eu.nds (Pokémon Mystery Dungeon: Explorers of Sky, EU, dumped from your own cartridge).
The repository keeps the .meta file of every generated asset: this script puts each asset back at the path of
its .meta, so Unity finds the same GUIDs and the scenes keep their references. Run it BEFORE opening the
project in Unity (Unity deletes a .meta whose asset is missing).
Each step is skipped when its output already exists; --force redoes everything.
"""
import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
import venv
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ROM = ROOT / "Rom/eos_eu.nds"
ROM_SHA1 = "c838a5adf1ed32d2da8454976e5b1a1aa189c139"
VENV = ROOT / "Tools/.venv"
PMD = ROOT / "Tools/pmd"
EXPORT = ROOT / "Rom/export"
SPRITES = ROOT / "Assets/Sprites/Pokemon"
PORTRAITS = ROOT / "Assets/Resources/Portraits"
ANIMSETS = ROOT / "Assets/Resources/AnimSets"
TILESETS = ROOT / "Assets/Tilesets"
# Folders whose content comes from the ROM (only their .meta files are versioned).
GENERATED = [SPRITES, PORTRAITS, ANIMSETS, TILESETS, ROOT / "Assets/Data", ROOT / "Assets/Resources/Data"]
# ScriptableObjects rebuilt by Unity: a placeholder keeps their GUID until the Build menus fill them.
PLACEHOLDERS = {ANIMSETS: "Assets/Scripts/Pmd/PmdAnimSet.cs", TILESETS: "Assets/Scripts/Dungeon/DungeonTileset.cs"}


def step(title):
    print(f"\n== {title}", flush=True)


def run(*args):
    subprocess.run([str(a) for a in args], cwd=ROOT, check=True)


def python():
    return VENV / ("Scripts/python.exe" if os.name == "nt" else "bin/python")


def check_rom():
    step("ROM")
    if not ROM.exists():
        sys.exit(f"Missing {ROM.relative_to(ROOT)}: dump your own Explorers of Sky (EU) cartridge there.")
    sha1 = hashlib.sha1(ROM.read_bytes()).hexdigest()
    if sha1 != ROM_SHA1:
        print(f"WARNING: unexpected ROM checksum {sha1} (expected {ROM_SHA1}); results may differ.")
    else:
        print("ok")


def setup_venv(force):
    step("Python environment (Tools/.venv)")
    if python().exists() and not force:
        print("already there")
        return
    venv.create(VENV, with_pip=True, clear=force)
    run(python(), "-m", "pip", "install", "-q", "-r", ROOT / "Tools/requirements.txt")


def extract(force):
    step("Unpack the ROM")
    if force or not (ROOT / "Rom/files").exists():
        run(python(), "-I", PMD / "nds_unpack.py", ROM, ROOT / "Rom/files")
    step("Game data (pokemon, moves, levels)")
    run(python(), "-I", PMD / "extract_data.py", ROOT / "Rom/files", ROOT / "Assets/Data")
    run(python(), "-I", PMD / "extract_levels.py", ROOT / "Rom/files", ROOT / "Assets/Resources/Data/levels_rt.bytes")
    run(python(), "-I", PMD / "generate_moves.py", ROOT)
    step("Sprites and portraits (about 8 minutes the first time)")
    if force or not (EXPORT / "sprites").exists():
        run(python(), "-I", PMD / "extract_sprites.py", ROM, EXPORT)
    else:
        print("Rom/export already there")


def restore_pokemon():
    """Copy into Assets/ the sprites of every Pokémon folder of the repository (its id.txt gives the id)."""
    step("Copy sprites and portraits into Assets/")
    pokemon = {p["id"]: p for p in json.loads((ROOT / "Assets/Data/pokemon.json").read_text(encoding="utf-8"))}
    count = 0
    for id_file in sorted(SPRITES.glob("*/id.txt")):
        pid = int(id_file.read_text().strip())
        source = EXPORT / "sprites" / f"{pokemon[pid]['sprite_index']:04d}"
        for f in list(source.glob("*-Anim.png")) + [source / "AnimData.xml"]:
            shutil.copy(f, id_file.parent / f.name)
        portrait = EXPORT / "portraits" / f"{pid:04d}" / "00.png"
        if portrait.exists():
            shutil.copy(portrait, PORTRAITS / f"{pid}.png")
        count += 1
    print(f"{count} Pokémon")


def restore_tilesets():
    step("Dungeon tilesets")
    ids = [int(d.name) for d in sorted(TILESETS.iterdir()) if d.is_dir() and d.name.isdigit()]
    run(python(), "-I", PMD / "extract_tileset.py", ROM, TILESETS, *ids)


def script_guid(script):
    meta = (ROOT / (script + ".meta")).read_text()
    return next(line.split()[1] for line in meta.splitlines() if line.startswith("guid:"))


def placeholders():
    step("Placeholders for the assets Unity rebuilds")
    count = 0
    for folder, script in PLACEHOLDERS.items():
        guid = script_guid(script)
        for meta in folder.rglob("*.asset.meta"):
            asset = meta.with_suffix("")
            if asset.exists():
                continue
            asset.write_text(
                "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
                "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
                "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
                f"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n  m_Name: {asset.stem}\n"
                f"  m_EditorClassIdentifier: Assembly-CSharp::{Path(script).stem}\n")
            count += 1
    print(f"{count} created")


def missing_assets():
    """Versioned .meta files whose asset is absent (Unity would delete them)."""
    return [m for folder in GENERATED for m in folder.rglob("*.meta") if not m.with_suffix("").exists()]


def find_unity(path):
    if path:
        return Path(path)
    version = next(line.split()[1] for line in (ROOT / "ProjectSettings/ProjectVersion.txt").read_text().splitlines()
                   if line.startswith("m_EditorVersion:"))
    for candidate in (Path.home() / f"Unity/Hub/Editor/{version}/Editor/Unity",
                      Path(f"C:/Program Files/Unity/Hub/Editor/{version}/Editor/Unity.exe"),
                      Path(f"/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity")):
        if candidate.exists():
            return candidate
    return None


def build_in_unity(unity):
    step("Unity: Build Tilesets + Build Anim Sets (batch mode, first import takes a while)")
    if unity is None:
        print("Unity not found: pass --unity <path>, or open the project and run the menus\n"
              "Pokescrap > Build Tilesets, then Pokescrap > Build Anim Sets.")
        return
    log = ROOT / "Logs/setup_unity.log"
    log.parent.mkdir(exist_ok=True)
    result = subprocess.run([str(unity), "-batchmode", "-nographics", "-projectPath", str(ROOT),
                             "-executeMethod", "PmdSpriteImport.BuildEverything", "-quit", "-logFile", str(log)])
    if result.returncode != 0:
        sys.exit(f"Unity failed (code {result.returncode}), see {log.relative_to(ROOT)}. Is the editor closed?")
    print("ok")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--unity", help="path to the Unity executable (found in the Hub folders otherwise)")
    parser.add_argument("--no-unity", action="store_true", help="stop before the Unity build step")
    parser.add_argument("--force", action="store_true", help="redo every step")
    args = parser.parse_args()

    check_rom()
    setup_venv(args.force)
    extract(args.force)
    restore_pokemon()
    restore_tilesets()
    placeholders()
    missing = missing_assets()
    if missing:
        for m in missing[:20]:
            print("  missing:", m.with_suffix("").relative_to(ROOT))
        sys.exit(f"{len(missing)} assets missing: do not open Unity, it would delete their .meta.")
    if not args.no_unity:
        build_in_unity(find_unity(args.unity))
    step("Done: you can open the project in Unity.")


if __name__ == "__main__":
    main()
