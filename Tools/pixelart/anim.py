"""
Animation strips as the game reads them: equally sized frames laid out in a grid, cropped to what the animation
actually uses, with the point that stands on the tile's centre (the pivot) given in the cropped frame.
"""
from px import Image, sheet

MAX_TEXTURE = 2048        # Keep every texture inside what the oldest phones can load.
GROUND_DROP = 24          # A unit's shadow sits this far below the centre of its tile.


class Strip:
    """One animation: frames (all the same size), the pivot inside a frame, and how it plays."""

    def __init__(self, name, frames, pivot, loop=True, fps=10, impact=-1):
        self.name, self.frames, self.pivot, self.loop, self.fps, self.impact = name, frames, pivot, loop, fps, impact

    @property
    def size(self):
        return self.frames[0].w, self.frames[0].h

    def columns(self):
        return max(1, min(len(self.frames), MAX_TEXTURE // self.frames[0].w))

    def image(self):
        return sheet(self.frames, columns=self.columns())

    def entry(self, path):
        """The manifest entry for this strip saved at `path` (relative to Resources/Sprites, no extension)."""
        w, h = self.size
        return {'name': self.name, 'path': path, 'w': w, 'h': h, 'columns': self.columns(), 'count': len(self.frames),
                'pivotX': self.pivot[0], 'pivotY': self.pivot[1], 'loop': self.loop, 'fps': self.fps, 'impact': self.impact}


def union_box(images, margin=0):
    """(x0, y0, x1, y1) around the opaque pixels of all the images, or None."""
    x0 = y0 = 10 ** 9
    x1 = y1 = -1
    for image in images:
        box = image.bbox()
        if box:
            x0, y0, x1, y1 = min(x0, box[0]), min(y0, box[1]), max(x1, box[2]), max(y1, box[3])
    if x1 < 0:
        return None
    return x0 - margin, y0 - margin, x1 + margin, y1 + margin


def cropped_strip(name, images, pivot, loop=True, fps=10, impact=-1, include=None):
    """
    A Strip from canvas-sized frames: cropped to their common box (grown to include the point `include`, so a pivot
    outside the drawing still lands inside the frame), with `pivot` (a canvas point) moved into the crop.
    """
    box = union_box(images)
    if box is None:
        box = (pivot[0], pivot[1], pivot[0] + 1, pivot[1] + 1)
    x0, y0, x1, y1 = box
    if include is not None:
        x0, y0, x1, y1 = min(x0, include[0]), min(y0, include[1]), max(x1, include[0] + 1), max(y1, include[1] + 1)
    frames = [image.crop(x0, y0, x1 - x0, y1 - y0) for image in images]
    return Strip(name, frames, (pivot[0] - x0, pivot[1] - y0), loop, fps, impact)


def box_centre(image):
    box = image.bbox()
    return ((box[0] + box[2]) // 2, (box[1] + box[3]) // 2) if box else None


def ground_pivot(shadow):
    """The canvas point that stands on the tile's centre, from a unit's shadow drawing."""
    cx, cy = box_centre(shadow)
    return cx, cy - GROUND_DROP
