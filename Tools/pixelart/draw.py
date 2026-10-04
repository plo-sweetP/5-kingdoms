"""
Drawing helpers for the pieces we make ourselves in the Tiny Swords style: flat fills from text grids, polygons and
ellipses, then the pack's 2 px dark outline added around the finished shape. A Part is an image with a position, so
pieces drawn separately can be stacked.
"""
from array import array

from px import Image, hexc

OUTLINE = hexc('#161c2e')  # The pack's outline colour.

# The pack's outlines are 2 px thick with rounded corners: every pixel within this reach of a filled one.
OUTLINE_REACH = [(dx, dy) for dy in range(-2, 3) for dx in range(-2, 3) if 0 < dx * dx + dy * dy <= 5]
THIN_REACH = [(1, 0), (-1, 0), (0, 1), (0, -1)]


class Part:
    """An image and where its top-left corner sits in some shared space (a head, a tile, a unit's frame)."""
    __slots__ = ('image', 'x', 'y')

    def __init__(self, image, x=0, y=0):
        self.image, self.x, self.y = image, x, y

    def moved(self, dx, dy):
        return Part(self.image, self.x + dx, self.y + dy)

    def paste_on(self, canvas, ox=0, oy=0):
        canvas.paste(self.image, self.x + ox, self.y + oy)
        return canvas

    def trimmed(self):
        image, x, y = self.image.trimmed()
        return Part(image, self.x + x, self.y + y)

    def mapped(self, mapping):
        return Part(self.image.mapped(mapping), self.x, self.y)


def stack(parts, pad=0):
    """Flatten parts (first at the bottom) into one Part covering them all."""
    parts = [p for p in parts if p is not None]
    x0 = min(p.x for p in parts) - pad
    y0 = min(p.y for p in parts) - pad
    x1 = max(p.x + p.image.w for p in parts) + pad
    y1 = max(p.y + p.image.h for p in parts) + pad
    out = Image(x1 - x0, y1 - y0)
    for p in parts:
        out.paste(p.image, p.x - x0, p.y - y0)
    return Part(out, x0, y0)


def grid(rows, palette, x=0, y=0):
    """A Part from rows of text, one character per pixel; '.' and ' ' are clear."""
    width = max(len(r) for r in rows)
    image = Image(width, len(rows))
    for yy, row in enumerate(rows):
        for xx, ch in enumerate(row):
            if ch not in '. ':
                image.px[yy * width + xx] = palette[ch]
    return Part(image, x, y)


def outlined(part, color=OUTLINE, reach=OUTLINE_REACH):
    """The part with the pack's outline around everything drawn so far. Works on a Part or an Image."""
    is_part = isinstance(part, Part)
    src = part.image if is_part else part
    r = max(max(abs(dx), abs(dy)) for dx, dy in reach)
    w, h = src.w + 2 * r, src.h + 2 * r
    out = Image(w, h)
    out.paste(src, r, r)
    solid = [i for i, p in enumerate(out.px) if p >> 24]
    px = out.px
    for i in solid:
        x, y = i % w, i // w
        for dx, dy in reach:
            j = (y + dy) * w + x + dx
            if not px[j] >> 24:
                px[j] = color
    return Part(out, part.x - r, part.y - r) if is_part else out


class Canvas:
    """A drawing surface in its own coordinates: shapes go on, a Part comes out. (ox, oy) is where (0, 0) lands."""

    def __init__(self, w, h, ox=0, oy=0):
        self.image = Image(w, h)
        self.ox, self.oy = ox, oy

    def _i(self, x, y):
        x, y = x + self.ox, y + self.oy
        return y * self.image.w + x if 0 <= x < self.image.w and 0 <= y < self.image.h else -1

    def set(self, x, y, color):
        i = self._i(x, y)
        if i >= 0:
            self.image.px[i] = color

    def get(self, x, y):
        i = self._i(x, y)
        return self.image.px[i] if i >= 0 else 0

    def dots(self, color, points):
        for x, y in points:
            self.set(x, y, color)
        return self

    def rect(self, x, y, w, h, color, only=None):
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                self._put(xx, yy, color, only)
        return self

    def _put(self, x, y, color, only):
        i = self._i(x, y)
        if i < 0:
            return
        if only is None or (self.image.px[i] in only if only else False):
            self.image.px[i] = color

    def ellipse(self, cx, cy, rx, ry, color, only=None):
        """Filled ellipse; `only` limits it to pixels that already have one of those colours (for shading)."""
        for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
            for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
                nx, ny = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
                if nx * nx + ny * ny <= 1.0:
                    self._put(x, y, color, only)
        return self

    def polygon(self, points, color, only=None):
        """Filled polygon (even-odd), tested at pixel centres."""
        ys = [p[1] for p in points]
        n = len(points)
        for y in range(int(min(ys)) - 1, int(max(ys)) + 2):
            cy = y + 0.5
            xs = []
            for i in range(n):
                x0, y0 = points[i]
                x1, y1 = points[(i + 1) % n]
                if (y0 <= cy < y1) or (y1 <= cy < y0):
                    xs.append(x0 + (cy - y0) * (x1 - x0) / (y1 - y0))
            xs.sort()
            for k in range(0, len(xs) - 1, 2):
                for x in range(int(xs[k] + 0.5), int(xs[k + 1] + 0.5)):
                    self._put(x, y, color, only)
        return self

    def line(self, x0, y0, x1, y1, color, width=1):
        """A line of square dabs."""
        steps = max(abs(x1 - x0), abs(y1 - y0), 1)
        for s in range(int(steps) + 1):
            x = x0 + (x1 - x0) * s / steps
            y = y0 + (y1 - y0) * s / steps
            for dy in range(width):
                for dx in range(width):
                    self.set(int(round(x)) + dx - width // 2, int(round(y)) + dy - width // 2, color)
        return self

    def curve(self, points, color, width=2):
        """A polyline through the points."""
        for (x0, y0), (x1, y1) in zip(points, points[1:]):
            self.line(x0, y0, x1, y1, color, width)
        return self

    def text(self, rows, palette, x=0, y=0, only=None):
        """Stamp a text grid at (x, y)."""
        for yy, row in enumerate(rows):
            for xx, ch in enumerate(row):
                if ch not in '. ':
                    self._put(x + xx, y + yy, palette[ch], only)
        return self

    def erase(self, x, y, w, h):
        return self.rect(x, y, w, h, 0)

    def part(self, outline=True, reach=OUTLINE_REACH, color=OUTLINE):
        p = Part(self.image, -self.ox, -self.oy)
        return outlined(p, color, reach) if outline else p


def recolor(image, mapping):
    """Exact colour replacement given {'#hex' or pixel: '#hex' or pixel}."""
    conv = lambda c: hexc(c) if isinstance(c, str) else c
    return image.mapped({conv(a): conv(b) for a, b in mapping.items()})


def grow(image, color, reach=THIN_REACH):
    """Fatten a shape by one ring of `color` (same canvas size; pixels pushed off the edge are lost)."""
    out = image.copy()
    w, h = image.w, image.h
    for i, p in enumerate(image.px):
        if not p >> 24:
            continue
        x, y = i % w, i // w
        for dx, dy in reach:
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and not image.px[ny * w + nx] >> 24:
                out.px[ny * w + nx] = color
    return out
