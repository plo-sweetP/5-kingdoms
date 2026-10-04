"""
The first dungeon's monsters, from the Enemy Pack (docs/design/ART.md, "The rest of the game"): the Spider, the Giant
Bat and the Troll, with their shadows taken off as separate sprites (the game flashes a sprite white when it is hit,
and the shadow must not flash with it) and a small portrait for the turn-order strip.
"""
import pack as P
from anim import cropped_strip, ground_pivot
from px import Image


class Monster:
    def __init__(self, monster_id, strips, shadow, shadow_pivot, portrait):
        self.id, self.strips, self.shadow, self.shadow_pivot, self.portrait = monster_id, strips, shadow, shadow_pivot, portrait


def _build(pack, monster_id, source, shadow_layer, anims, face, extra_sources=()):
    """
    anims: [(name, first frame, last frame, loops, impact frame or -1)] in the main source; extra_sources adds
    animations from other files: [(file, name, first, last, loops)]. face: (x, y, size) of the portrait's corner on
    the first idle frame.
    """
    ase = pack.ase(source)
    shadow_index = ase.layer(shadow_layer).index
    body = [l for l in ase.image_layers() if l.index != shadow_index]
    shadow_full = ase.layer_image(anims[0][1], shadow_layer)
    pivot = ground_pivot(shadow_full)
    strips = []
    for name, first, last, loops, impact in anims:
        frames = [ase.render(f, layers=body) for f in range(first, last + 1)]
        strips.append(cropped_strip(name, frames, pivot, loop=loops, impact=impact))
    for file, name, first, last, loops in extra_sources:
        other = pack.ase(file)
        layers = [l for l in other.image_layers() if l.name != shadow_layer]
        frames = [other.render(f, layers=layers) for f in range(first, last + 1)]
        strips.append(cropped_strip(name, frames, pivot, loop=loops))
    shadow, sx, sy = shadow_full.trimmed()
    # The portrait: a square of the first idle frame around the middle of the head.
    head = ase.layer_image(anims[0][1], 'Head').bbox()
    cx, cy = (head[0] + head[2]) // 2 + face[0], (head[1] + head[3]) // 2 + face[1]
    portrait = ase.render(anims[0][1], layers=body).crop(cx - face[2] // 2, cy - face[2] // 2, face[2], face[2])
    return Monster(monster_id, strips, shadow, (pivot[0] - sx, pivot[1] - sy), portrait)


def build(pack):
    """face: (dx, dy, size): the portrait's size and how far its middle is from the middle of the head layer."""
    return [
        _build(pack, 'spider', P.ENEMY + '/Spider/Spider.aseprite', 'Shadow',
               [('idle', 0, 7, True, -1), ('run', 8, 12, True, -1), ('attack', 13, 20, False, 4)], face=(0, 0, 48)),
        _build(pack, 'bat', P.ENEMY + '/Giant Bat/Giant Bat.aseprite', 'Shadow',
               [('idle', 1, 6, True, -1), ('run', 7, 10, True, -1), ('attack', 11, 17, False, 4)], face=(0, 0, 48)),
        _build(pack, 'troll', P.ENEMY + '/Troll/Troll.aseprite', 'Shadows',
               [('idle', 1, 12, True, -1), ('run', 13, 22, True, -1), ('windup', 23, 27, False, -1),
                ('attack', 28, 33, False, 2), ('recovery', 34, 43, False, -1)], face=(0, 6, 64),
               extra_sources=[(P.ENEMY + '/Extra/Troll Dead/Troll Dead.aseprite', 'dead', 0, 9, False)]),
    ]
