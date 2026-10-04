"""
The dungeon's ground, built from the pack's terrain tileset (docs/design/ART.md, "The rest of the game"): floors are
flat ground, walls are raised ground with cliff faces toward the rooms, and decorations sit on the raised ground.

The pack draws a plateau's cliff in the tile below its edge. A dungeon wall is often one tile thick and must not
cover the floor in front of it, so the wall tiles are put together here instead: a wall tile with floor to its south
shows the plateau's grass lip in its top part and a shortened cliff below it, all inside its own tile.

Wall tiles are named by which neighbours are walls too: wall_top_<N><E><W> where the tile to the south is a wall,
wall_face_<N><E><W> where it is floor (1 = wall, 0 = floor).
"""
import pack as P
from draw import outlined
from px import Image

TILE = 64
LIP = 24                 # Height of the grass lip on a wall's face tile; the cliff takes the rest.
TILESET = P.FREE + '/Terrain/Tileset/Tilemap_color%d.png'
GROUND_COLOR, WALL_COLOR = 4, 3   # Of the pack's five ground colours: dry ground to walk on, lush raised ground around it.

# Where the raised ground's tiles are in the pack's tileset (column, row), by which neighbours are raised too.
PLATEAU = {  # (north, east, south, west) -> (column, row)
    (0, 1, 1, 0): (5, 0), (0, 1, 1, 1): (6, 0), (0, 0, 1, 1): (7, 0),
    (1, 1, 1, 0): (5, 1), (1, 1, 1, 1): (6, 1), (1, 0, 1, 1): (7, 1),
    (1, 1, 0, 0): (5, 2), (1, 1, 0, 1): (6, 2), (1, 0, 0, 1): (7, 2),
    (0, 0, 1, 0): (8, 0), (1, 0, 1, 0): (8, 1), (1, 0, 0, 0): (8, 2),
    (0, 1, 0, 0): (5, 3), (0, 1, 0, 1): (6, 3), (0, 0, 0, 1): (7, 3), (0, 0, 0, 0): (8, 3),
}
CLIFF = {(1, 1): (6, 4), (0, 1): (5, 4), (1, 0): (7, 4), (0, 0): (8, 4)}   # (west, east) is a wall -> (column, row)
GROUND = (1, 1)


def tile(sheet, column, row):
    return sheet.crop(column * TILE, row * TILE, TILE, TILE)


def wall_tiles(pack, ground_color=GROUND_COLOR, wall_color=WALL_COLOR):
    """
    {name: 64x64 image} for the ground and every wall tile. The floor and the raised ground come from different
    colours of the pack's tileset, so what can be walked on reads at a glance.
    """
    sheet = pack.png(TILESET % wall_color)
    out = {'ground': tile(pack.png(TILESET % ground_color), *GROUND)}
    for north in (0, 1):
        for east in (0, 1):
            for west in (0, 1):
                key = '%d%d%d' % (north, east, west)
                out['wall_top_' + key] = tile(sheet, *PLATEAU[(north, east, 1, west)])
                edge = tile(sheet, *PLATEAU[(north, east, 0, west)])
                face = Image(TILE, TILE)
                if north:
                    face.paste(edge.crop(0, TILE - LIP, TILE, LIP), 0, 0)
                else:
                    # No raised ground behind it: the strip's far edge and near edge, pushed together.
                    face.paste(edge.crop(0, 0, TILE, 10), 0, 0)
                    face.paste(edge.crop(0, TILE - (LIP - 10), TILE, LIP - 10), 0, 10)
                cliff = tile(sheet, *CLIFF[(west, east)])
                under = Image(TILE, TILE)
                under.paste(cliff.crop(0, LIP, TILE, TILE - LIP), 0, LIP)
                under.paste(face)
                out['wall_face_' + key] = under
    return out


def static(pack, relative, frame_w=None, frame=0):
    """A decoration: the whole PNG, or one frame of a strip, trimmed. Returns (image, pivot x, pivot y)."""
    image = pack.png(relative)
    if frame_w:
        image = image.crop(frame * frame_w, 0, frame_w, image.h)
    cut, x, y = image.trimmed()
    return cut


def decorations(pack):
    """
    {name: image} of what stands on the raised ground. Each is trimmed; the game stands it on the middle of the
    tile's lower edge. Bushes and trees are the first frame of the pack's swaying strips.
    """
    deco = P.FREE + '/Terrain/Decorations'
    wood = P.FREE + '/Terrain/Resources/Wood/Trees'
    skull = P.ENEMY + '/Extra/Skull decorations'
    out = {}
    for i in (1, 2, 3, 4):
        out['bush_%d' % i] = static(pack, '%s/Bushes/Bushe%d.png' % (deco, i), 128)
        out['rock_%d' % i] = static(pack, '%s/Rocks/Rock%d.png' % (deco, i))
        out['tree_%d' % i] = static(pack, '%s/Tree%d.png' % (wood, i), 192)
    for i in (1, 2, 3):
        out['bones_%d' % i] = static(pack, '%s/Bones_0%d.png' % (skull, i))
    for i in (1, 2):
        out['skull_spike_%d' % i] = static(pack, '%s/Skull Spike_0%d.png' % (skull, i))
        out['stump_%d' % i] = static(pack, '%s/Stump %d.png' % (wood, i))
    return out


def cave_frames(pack):
    """
    The way down: the pack's cave entrance cut down to the stones around its mouth, so it fits the one tile the
    stairs take (the whole mound would cover the walkable tiles around it). Eight frames: eyes blink in the dark.
    Returns (frames, pivot x, pivot y): the pivot is the point that stands on the tile's centre.
    """
    ase = pack.ase(P.ENEMY + '/Extra/Cave/Cave.aseprite')
    cx, cy, rx, ry = 90.0, 127.0, 45.0, 37.0
    x0, y0, x1, y1 = int(cx - rx) - 3, int(cy - ry) - 3, int(cx + rx) + 3, int(cy + ry) + 3
    frames = []
    for f in range(ase.frame_count):
        full = ase.render(f, layers=['Rock', 'Eyes'])
        cut = Image(full.w, full.h)
        for y in range(y0, y1):
            for x in range(x0, x1):
                nx, ny = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
                if nx * nx + ny * ny <= 1.0:
                    p = full.get(x, y)
                    if p >> 24 == 255:           # The mound's soft shadow stays behind.
                        cut.px[y * full.w + x] = p
        ring = outlined(cut)
        frames.append(ring.crop(2 + x0 - 2, 2 + y0 - 2, x1 - x0 + 4, y1 - y0 + 4))
    return frames, int(cx) - (x0 - 2), 134 - (y0 - 2)
