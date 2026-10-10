#!/usr/bin/env python3
"""
Builds the game's art from the Tiny Swords packs (docs/design/ART.md). The packs stay outside the repo; this script
reads them, takes the units' layers apart, recolours, draws what the packs lack, and writes only what the game uses
into Assets/Art/Resources under our own names, with art_manifest.json telling the game how the pieces fit together
(frame sizes, pivots, animations, 9-slice borders, where a head sits on each frame of a body). It doubles as the list
of what we use: pack_files_used.txt is rewritten on every run (docs/THIRD_PARTY.md has the terms).
Python 3 standard library only.

Usage:
    python Tools/pixelart/build_art.py                      # write the game's art
    python Tools/pixelart/build_art.py --preview FOLDER     # also write the review sheets (heroes, weapons, armor...)
    python Tools/pixelart/build_art.py --only preview --preview FOLDER
    python Tools/pixelart/build_art.py --pack FOLDER        # the packs live somewhere else
"""
import argparse
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import anim  # noqa: E402
import fx  # noqa: E402
import heads  # noqa: E402
import icons  # noqa: E402
import looks  # noqa: E402
import monsters  # noqa: E402
import pack as packs  # noqa: E402
import rigs  # noqa: E402
import sheets  # noqa: E402
import terrain  # noqa: E402
import tree  # noqa: E402
import ui  # noqa: E402
from px import sheet, to_hex  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
RESOURCES = os.path.join(ROOT, 'Assets', 'Art', 'Resources')
SPRITES = os.path.join(RESOURCES, 'Sprites')
PIXELS_PER_UNIT = 64

# Which frame of an attack lands the hit, per rig animation (the game times the animation so this frame comes
# when the hit does), and whether an animation loops.
RIG_IMPACT = {('warrior', 'attack'): 2, ('warrior', 'attack2'): 2, ('archer', 'attack'): 5, ('monk', 'cast'): 3,
              ('pawn', 'attack'): 1, ('pawn', 'attack2'): 2}


class Writer:
    """Writes PNGs under Resources/Sprites and remembers them, so files from an earlier run that are no longer made can go."""

    def __init__(self):
        self.written = set()

    def save(self, relative, image):
        path = os.path.join(SPRITES, relative.replace('/', os.sep) + '.png')
        image.save(path)
        self.written.add(os.path.normcase(path))
        return relative

    def strip(self, relative, strip):
        self.save(relative, strip.image())
        return strip.entry(relative)

    def remove_stale(self):
        removed = 0
        for folder, _, files in os.walk(SPRITES):
            for name in files:
                path = os.path.join(folder, name)
                if name.endswith('.png') and os.path.normcase(path) not in self.written:
                    os.remove(path)
                    removed += 1
            for name in files:   # Unity's .meta files go with their sprites.
                path = os.path.join(folder, name)
                if name.endswith('.png.meta') and not os.path.exists(path[:-5]):
                    os.remove(path)
        for name in os.listdir(SPRITES):   # A folder nothing is written to any more goes too, with its .meta.
            path = os.path.join(SPRITES, name)
            if os.path.isdir(path) and not os.listdir(path):
                os.rmdir(path)
                if os.path.exists(path + '.meta'):
                    os.remove(path + '.meta')
        return removed


def part_entry(writer, relative, part):
    writer.save(relative, part.image)
    return {'path': relative, 'x': part.x, 'y': part.y, 'w': part.image.w, 'h': part.image.h}


def head_reach(hero_parts, armors, still_plumes):
    """How far any head part reaches from the head's origin, so every rig's frames leave room for it."""
    parts = [p for hero in hero_parts.values() for p in hero.values()]
    parts += [a.piece for a in armors if a.piece is not None] + list(still_plumes.values())
    parts += [c.piece for c in heads.cosmetics()]
    return (min(p.x for p in parts), min(p.y for p in parts),
            max(p.x + p.image.w for p in parts), max(p.y + p.image.h for p in parts))


def export_rig(writer, wardrobe, rig, head_box):
    slots = dict(rig.all_slots())
    slots.pop('shadow')
    if rig.id != 'monk':
        slots['body_skirt'] = wardrobe.skirt_body(rig)
    cell = cell_of(rig, slots, head_box)
    layers = []
    written = {}   # A layer several weapons share (the same slash arcs) is written once.
    for name, frames in sorted(slots.items()):
        box = anim.union_box(frames)
        if box is None:
            continue
        cut = [f.crop(box[0], box[1], box[2] - box[0], box[3] - box[1]) for f in frames]
        columns = max(1, min(len(cut), anim.MAX_TEXTURE // cut[0].w))
        image = sheet(cut, columns=columns)
        key = (image.w, image.h, image.to_bytes())
        if key not in written:
            written[key] = writer.save('Heroes/%s_%s' % (rig.id, name), image)
        layers.append({'name': name, 'path': written[key], 'x': box[0] - cell[0], 'y': box[1] - cell[1],
                       'w': cut[0].w, 'h': cut[0].h, 'columns': columns})
    shadow, sx, sy = rig.slots['shadow'][0].trimmed()
    writer.save('Heroes/%s_shadow' % rig.id, shadow)
    anims, start = [], 0
    for name, frames, loops in rig.anims:
        anims.append({'name': name, 'start': start, 'count': len(frames), 'loop': loops, 'fps': rig.fps,
                      'impact': RIG_IMPACT.get((rig.id, name), -1)})
        start += len(frames)
    return {
        'id': rig.id, 'cellW': cell[2] - cell[0], 'cellH': cell[3] - cell[1],
        'pivotX': rig.pivot[0] - cell[0], 'pivotY': rig.pivot[1] - cell[1],
        'handX': rig.hand[0] - cell[0], 'handY': rig.hand[1] - cell[1],
        'anims': anims,
        'headX': [x - cell[0] for x, _ in rig.head], 'headY': [y - cell[1] for _, y in rig.head],
        'layers': layers,
        'shadow': {'name': 'shadow', 'path': 'Heroes/%s_shadow' % rig.id, 'w': shadow.w, 'h': shadow.h, 'columns': 1,
                   'count': 1, 'pivotX': rig.pivot[0] - sx, 'pivotY': rig.pivot[1] - sy, 'loop': False, 'fps': 10, 'impact': -1},
        'weapons': [{'id': w.id, 'name': w.name} for w in rig.weapons],
    }


def cell_of(rig, slots, head_box):
    box = anim.union_box([f for frames in slots.values() for f in frames])
    x0, y0, x1, y1 = box
    for hx, hy in rig.head:
        x0, y0 = min(x0, hx + head_box[0]), min(y0, hy + head_box[1])
        x1, y1 = max(x1, hx + head_box[2]), max(y1, hy + head_box[3])
    return x0, y0, x1, y1


def export(pack, wardrobe):
    writer = Writer()
    manifest = {'pixelsPerUnit': PIXELS_PER_UNIT}

    # The ground.
    for name, image in terrain.wall_tiles(pack).items():
        writer.save('Tiles/' + name, image)
    strips = []
    for name, image in terrain.decorations(pack).items():
        strips.append(writer.strip('Deco/' + name, anim.Strip('Deco/' + name, [image], (image.w // 2, image.h))))
    cave, pivot_x, pivot_y = terrain.cave_frames(pack)
    strips.append(writer.strip('Deco/cave', anim.Strip('Deco/cave', cave, (pivot_x, pivot_y), loop=True, fps=6)))

    # Effects, items, markers.
    for strip in fx.build(pack):
        strip.name = 'Effects/' + strip.name
        strips.append(writer.strip(strip.name, strip))
    manifest['strips'] = strips

    # Monsters.
    manifest['monsters'] = []
    for monster in monsters.build(pack):
        base = 'Monsters/' + monster.id
        writer.save(base + '_portrait', monster.portrait)
        shadow = anim.Strip('shadow', [monster.shadow], monster.shadow_pivot, loop=False)
        manifest['monsters'].append({
            'id': monster.id, 'portrait': base + '_portrait', 'shadow': writer.strip(base + '_shadow', shadow),
            'anims': [writer.strip('%s_%s' % (base, s.name), s) for s in monster.strips]})

    # Heroes: the rigs' layers, the heads, the armor sets.
    armors = list(wardrobe.armors.values())
    head_box = head_reach(wardrobe.heroes, armors, wardrobe.still_plumes)
    manifest['rigs'] = [export_rig(writer, wardrobe, rig, head_box) for rig in wardrobe.rigs.values()]
    manifest['heroes'] = []
    for hero, parts in wardrobe.heroes.items():
        weapon, armor = looks.DEFAULT_LOOKS[hero]
        merged = dict(parts)
        merged['base'] = heads.stack([merged.pop('skull'), merged.pop('side')])   # Always worn together.
        manifest['heroes'].append({
            'id': hero, 'weapon': weapon, 'armor': armor, 'skirt': hero in looks.SKIRTED,
            'parts': [dict(part_entry(writer, 'Heads/%s_%s' % (hero, name), part), name=name) for name, part in sorted(merged.items())]})
    manifest['armors'] = []
    for armor in armors:
        entry = {'id': armor.id, 'name': armor.name, 'hair': armor.hair,
                 'cloth': [to_hex(c) for c in armor.cloth], 'metal': [to_hex(c) for c in armor.metal],
                 'piece': part_entry(writer, 'Heads/piece_' + armor.id, armor.piece),
                 'plume': part_entry(writer, 'Heads/plume_' + armor.id, wardrobe.still_plumes[armor.id])
                 if armor.id in wardrobe.still_plumes else {'path': '', 'x': 0, 'y': 0, 'w': 0, 'h': 0}}
        manifest['armors'].append(entry)
    manifest['cosmetics'] = [{'id': c.id, 'name': c.name, 'hair': c.hair, 'piece': part_entry(writer, 'Heads/cosmetic_' + c.id, c.piece)}
                             for c in wardrobe.cosmetics.values()]
    manifest['auras'] = [{'id': sheets.ring_id(name), 'name': name, 'color': color} for name, color in sheets.RING_AURAS]

    # Icons for the gear and hero screens to come: Icons/weapon_<id>, Icons/armor_<set>_<slot>, Icons/ring_<set>.
    for name, image in icons.build(wardrobe, [(sheets.ring_id(n), c) for n, c in sheets.RING_AURAS]).items():
        writer.save(name, image)

    # The HUD.
    manifest['ui'] = []
    for piece in ui.build(pack) + tree.pieces():
        writer.save('UI/' + piece.name, piece.image)
        left, bottom, right, top = piece.border
        manifest['ui'].append({'name': piece.name, 'path': 'UI/' + piece.name, 'left': left, 'bottom': bottom, 'right': right, 'top': top})
    # The skill icons: Icons/skill_<skill id> (a weapon attack by its weapon), listed with the HUD art so the screens
    # that show them load them the same way.
    for skill_id, image in sorted(icons.skill_icons().items()):
        writer.save('Icons/skill_' + skill_id, image)
        manifest['ui'].append({'name': 'skill_' + skill_id, 'path': 'Icons/skill_' + skill_id, 'left': 0, 'bottom': 0, 'right': 0, 'top': 0})

    removed = writer.remove_stale()
    with open(os.path.join(RESOURCES, 'art_manifest.json'), 'w', newline='\n') as f:
        json.dump(manifest, f, indent=1, sort_keys=True)
        f.write('\n')
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'pack_files_used.txt'), 'w', newline='\n') as f:
        f.write('# Files of the Tiny Swords packs that build_art.py reads (rewritten on every run).\n')
        f.write('\n'.join(sorted(pack.used)) + '\n')
    print('Wrote %d sprites to %s (%d stale removed)' % (len(writer.written), SPRITES, removed))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--pack', help='folder holding the unpacked Tiny Swords packs (default: %s)' % packs.DEFAULT_ROOT)
    parser.add_argument('--preview', help='write the review sheets into this folder')
    parser.add_argument('--only', choices=('preview',), help='skip everything else')
    args = parser.parse_args()

    started = time.time()
    pack = packs.Pack(args.pack)
    wardrobe = looks.Wardrobe(rigs.build(pack), {hero: make() for hero, make in heads.HEROES.items()}, heads.armors(pack),
                              heads.cosmetics())
    print('Rigs, heads and armor built in %.1fs' % (time.time() - started))

    if args.only != 'preview':
        export(pack, wardrobe)
    if args.preview:
        for path in sheets.build(wardrobe, args.preview, pack):
            print('Preview: %s' % path)
    print('Done in %.1fs; %d pack files read' % (time.time() - started, len(pack.used)))


if __name__ == '__main__':
    main()
