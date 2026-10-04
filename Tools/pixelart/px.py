"""
A small pixel-art image library for the art tools: RGBA images held as flat arrays of 32-bit pixels, PNG reading
and writing, and the handful of operations the build script needs (crop, paste, flip, recolor, outline, scale).
Python 3 standard library only.

A pixel is one int: R in the lowest byte, then G, B and A, which is the byte order of a PNG's RGBA data on a
little-endian machine, so whole images convert to and from bytes without a loop.
"""
import os
import struct
import sys
import zlib
from array import array
from itertools import accumulate

assert sys.byteorder == 'little', 'px.py packs pixels little-endian'

CLEAR = 0


def rgba(r, g, b, a=255):
    return (r & 255) | ((g & 255) << 8) | ((b & 255) << 16) | ((a & 255) << 24)


def hexc(text, alpha=255):
    """'#rrggbb' or '#rrggbbaa' to a pixel."""
    h = text.lstrip('#')
    if len(h) == 8:
        alpha = int(h[6:8], 16)
    return rgba(int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), alpha)


def channels(p):
    return p & 255, (p >> 8) & 255, (p >> 16) & 255, (p >> 24) & 255


def to_hex(p):
    r, g, b, a = channels(p)
    return '#%02x%02x%02x' % (r, g, b) + ('' if a == 255 else '%02x' % a)


def with_alpha(p, a):
    return (p & 0x00FFFFFF) | ((a & 255) << 24)


def mix(p, q, t):
    """Blend from p (t = 0) to q (t = 1), alpha included."""
    pr, pg, pb, pa = channels(p)
    qr, qg, qb, qa = channels(q)
    f = lambda a, b: int(round(a + (b - a) * t))
    return rgba(f(pr, qr), f(pg, qg), f(pb, qb), f(pa, qa))


def shade(p, amount):
    """Lighter (amount > 0) or darker by a flat step per channel."""
    r, g, b, a = channels(p)
    c = lambda v: max(0, min(255, v + amount))
    return rgba(c(r), c(g), c(b), a)


def luma(p):
    r, g, b, _ = channels(p)
    return (r * 299 + g * 587 + b * 114) // 1000


def over(src, dst):
    """Alpha-over of one pixel on another."""
    sa = src >> 24
    if sa == 255 or dst >> 24 == 0:
        return src
    if sa == 0:
        return dst
    sr, sg, sb, _ = channels(src)
    dr, dg, db, da = channels(dst)
    out_a = sa * 255 + da * (255 - sa)
    f = lambda s, d: (s * sa * 255 + d * da * (255 - sa) + out_a // 2) // out_a
    return rgba(f(sr, dr), f(sg, dg), f(sb, db), (out_a + 127) // 255)


class Image:
    __slots__ = ('w', 'h', 'px')

    def __init__(self, w, h, px=None, fill=CLEAR):
        self.w, self.h = w, h
        if px is None:
            px = array('I', bytes(4 * w * h)) if fill == CLEAR else array('I', [fill]) * (w * h)
        assert len(px) == w * h, 'pixel count %d != %dx%d' % (len(px), w, h)
        self.px = px

    # ---- pixels ----

    def inside(self, x, y):
        return 0 <= x < self.w and 0 <= y < self.h

    def get(self, x, y):
        return self.px[y * self.w + x] if 0 <= x < self.w and 0 <= y < self.h else CLEAR

    def set(self, x, y, color):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[y * self.w + x] = color

    def blend(self, x, y, color):
        if 0 <= x < self.w and 0 <= y < self.h:
            i = y * self.w + x
            self.px[i] = over(color, self.px[i])

    def copy(self):
        return Image(self.w, self.h, array('I', self.px))

    def row(self, y):
        return self.px[y * self.w:(y + 1) * self.w]

    # ---- geometry ----

    def crop(self, x, y, w, h):
        """A w x h window at (x, y); parts outside the image come out transparent."""
        out = Image(w, h)
        x0, x1 = max(0, x), min(self.w, x + w)
        if x0 >= x1:
            return out
        for yy in range(max(0, y), min(self.h, y + h)):
            start = yy * self.w
            dst = (yy - y) * w + (x0 - x)
            out.px[dst:dst + (x1 - x0)] = self.px[start + x0:start + x1]
        return out

    def bbox(self):
        """(x0, y0, x1, y1) of the non-transparent pixels, x1 and y1 exclusive; None when empty."""
        w = self.w
        top = bottom = None
        left, right = w, -1
        for y in range(self.h):
            row = self.px[y * w:(y + 1) * w]
            if not any(row):
                continue
            solid = [i for i, p in enumerate(row) if p >> 24]
            if not solid:
                continue
            top = y if top is None else top
            bottom = y
            left, right = min(left, solid[0]), max(right, solid[-1])
        return None if top is None else (left, top, right + 1, bottom + 1)

    def trimmed(self):
        """(image cropped to its bbox, x, y); an empty image gives (1x1 clear, 0, 0)."""
        box = self.bbox()
        if box is None:
            return Image(1, 1), 0, 0
        return self.crop(box[0], box[1], box[2] - box[0], box[3] - box[1]), box[0], box[1]

    def flipped_x(self):
        out = Image(self.w, self.h)
        w = self.w
        for y in range(self.h):
            row = self.px[y * w:(y + 1) * w]
            row.reverse()
            out.px[y * w:(y + 1) * w] = row
        return out

    def flipped_y(self):
        out = Image(self.w, self.h)
        w = self.w
        for y in range(self.h):
            out.px[(self.h - 1 - y) * w:(self.h - y) * w] = self.px[y * w:(y + 1) * w]
        return out

    def scaled(self, k):
        """Nearest-neighbour enlargement by a whole number."""
        if k == 1:
            return self.copy()
        out = array('I')
        w = self.w
        for y in range(self.h):
            wide = array('I', [p for p in self.px[y * w:(y + 1) * w] for _ in range(k)])
            for _ in range(k):
                out.extend(wide)
        return Image(self.w * k, self.h * k, out)

    def shifted(self, dx, dy):
        return self.crop(-dx, -dy, self.w, self.h)

    # ---- composition ----

    def paste(self, src, ox=0, oy=0, opacity=255):
        """Alpha-over src onto this image with its top-left corner at (ox, oy)."""
        x0, x1 = max(0, ox), min(self.w, ox + src.w)
        if x0 >= x1:
            return self
        w = self.w
        n = x1 - x0
        for y in range(max(0, oy), min(self.h, oy + src.h)):
            s0 = (y - oy) * src.w + (x0 - ox)
            srow = src.px[s0:s0 + n]
            if not any(srow):
                continue
            d0 = y * w + x0
            drow = self.px[d0:d0 + n]
            if opacity == 255:
                self.px[d0:d0 + n] = array('I', [
                    s if s >= 0xFF000000 else d if s < 0x01000000 else over(s, d) for s, d in zip(srow, drow)])
            else:
                self.px[d0:d0 + n] = array('I', [
                    d if s < 0x01000000 else over(with_alpha(s, (s >> 24) * opacity // 255), d)
                    for s, d in zip(srow, drow)])
        return self

    def paste_under(self, src, ox=0, oy=0):
        """Put src behind what is already here."""
        base = Image(self.w, self.h)
        base.paste(src, ox, oy)
        base.paste(self)
        self.px = base.px
        return self

    # ---- colour ----

    def mapped(self, mapping):
        """Replace exact colours: mapping is {pixel: pixel}. Pixels not in the mapping stay."""
        get = mapping.get
        return Image(self.w, self.h, array('I', [get(p, p) for p in self.px]))

    def mapped_fn(self, fn):
        """Replace every distinct opaque-ish colour through fn(pixel) -> pixel (called once per distinct colour)."""
        table = {p: fn(p) for p in set(self.px) if p >> 24}
        return self.mapped(table)

    def colors(self):
        """{pixel: count} of the non-transparent pixels."""
        counts = {}
        for p in self.px:
            if p >> 24:
                counts[p] = counts.get(p, 0) + 1
        return counts

    def silhouette(self, color):
        rgb = color & 0x00FFFFFF
        return Image(self.w, self.h, array('I', [(p & 0xFF000000) | rgb if p >> 24 else 0 for p in self.px]))

    def faded(self, opacity):
        return Image(self.w, self.h, array('I', [with_alpha(p, (p >> 24) * opacity // 255) if p >> 24 else 0 for p in self.px]))

    def masked(self, mask, keep=True):
        """Keep (or drop) the pixels where mask is non-transparent. Same size as this image."""
        assert (mask.w, mask.h) == (self.w, self.h)
        if keep:
            return Image(self.w, self.h, array('I', [p if m >> 24 else 0 for p, m in zip(self.px, mask.px)]))
        return Image(self.w, self.h, array('I', [0 if m >> 24 else p for p, m in zip(self.px, mask.px)]))

    def outlined(self, color, diagonal=False):
        """A copy with a 1 px outline on every transparent pixel touching the sprite."""
        out = self.copy()
        w, h, px = self.w, self.h, self.px
        steps = ((1, 0), (-1, 0), (0, 1), (0, -1)) + (((1, 1), (-1, 1), (1, -1), (-1, -1)) if diagonal else ())
        for y in range(h):
            for x in range(w):
                if px[y * w + x] >> 24:
                    continue
                for dx, dy in steps:
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < w and 0 <= ny < h and px[ny * w + nx] >> 24:
                        out.px[y * w + x] = color
                        break
        return out

    def padded(self, left, top, right, bottom):
        return self.crop(-left, -top, self.w + left + right, self.h + top + bottom)

    # ---- shapes ----

    def rect(self, x, y, w, h, color):
        for yy in range(max(0, y), min(self.h, y + h)):
            for xx in range(max(0, x), min(self.w, x + w)):
                self.px[yy * self.w + xx] = color
        return self

    def hline(self, x0, x1, y, color):
        return self.rect(min(x0, x1), y, abs(x1 - x0) + 1, 1, color)

    def vline(self, x, y0, y1, color):
        return self.rect(x, min(y0, y1), 1, abs(y1 - y0) + 1, color)

    def line(self, x0, y0, x1, y1, color):
        """Bresenham line."""
        dx, dy = abs(x1 - x0), -abs(y1 - y0)
        sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
        err = dx + dy
        while True:
            self.set(x0, y0, color)
            if x0 == x1 and y0 == y1:
                break
            e2 = 2 * err
            if e2 >= dy:
                err += dy
                x0 += sx
            if e2 <= dx:
                err += dx
                y0 += sy
        return self

    def ellipse(self, cx, cy, rx, ry, color):
        """Filled ellipse around a centre given in pixel coordinates (may be fractional)."""
        for y in range(max(0, int(cy - ry - 1)), min(self.h, int(cy + ry + 2))):
            for x in range(max(0, int(cx - rx - 1)), min(self.w, int(cx + rx + 2))):
                nx, ny = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
                if nx * nx + ny * ny <= 1.0:
                    self.px[y * self.w + x] = color
        return self

    # ---- files ----

    def to_bytes(self):
        return self.px.tobytes()

    def save(self, path):
        folder = os.path.dirname(path)
        if folder:
            os.makedirs(folder, exist_ok=True)
        data = self.px.tobytes()
        stride = self.w * 4
        raw = bytearray()
        for y in range(self.h):
            raw.append(0)  # PNG filter type: none.
            raw += data[y * stride:(y + 1) * stride]

        def chunk(tag, body):
            return struct.pack('>I', len(body)) + tag + body + struct.pack('>I', zlib.crc32(tag + body) & 0xFFFFFFFF)

        png = b'\x89PNG\r\n\x1a\n'
        png += chunk(b'IHDR', struct.pack('>IIBBBBB', self.w, self.h, 8, 6, 0, 0, 0))
        png += chunk(b'IDAT', zlib.compress(bytes(raw), 9))
        png += chunk(b'IEND', b'')
        with open(path, 'wb') as f:
            f.write(png)
        return path

    @staticmethod
    def from_bytes(w, h, data):
        px = array('I')
        px.frombytes(bytes(data))
        return Image(w, h, px)

    @staticmethod
    def load(path):
        with open(path, 'rb') as f:
            data = f.read()
        return decode_png(data)


def _unfilter(raw, width_bytes, bpp, height):
    """Undo PNG scanline filters; returns the image bytes without the filter-type bytes."""
    out = bytearray()
    prev = bytes(width_bytes)
    pos = 0
    for _ in range(height):
        kind = raw[pos]
        line = bytearray(raw[pos + 1:pos + 1 + width_bytes])
        pos += 1 + width_bytes
        if kind == 1:
            for c in range(bpp):
                line[c::bpp] = bytes(v & 255 for v in accumulate(line[c::bpp]))
        elif kind == 2:
            line = bytearray((a + b) & 255 for a, b in zip(line, prev))
        elif kind == 3:
            for i in range(width_bytes):
                left = line[i - bpp] if i >= bpp else 0
                line[i] = (line[i] + ((left + prev[i]) >> 1)) & 255
        elif kind == 4:
            for i in range(width_bytes):
                a = line[i - bpp] if i >= bpp else 0
                b = prev[i]
                c = prev[i - bpp] if i >= bpp else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[i] = (line[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        elif kind != 0:
            raise ValueError('unknown PNG filter %d' % kind)
        out += line
        prev = line
    return out


def decode_png(data):
    if data[:8] != b'\x89PNG\r\n\x1a\n':
        raise ValueError('not a PNG')
    pos = 8
    idat = bytearray()
    palette = trns = None
    w = h = depth = ctype = interlace = None
    while pos < len(data):
        length, tag = struct.unpack('>I4s', data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + length]
        pos += 12 + length
        if tag == b'IHDR':
            w, h, depth, ctype, _, _, interlace = struct.unpack('>IIBBBBB', body)
        elif tag == b'PLTE':
            palette = body
        elif tag == b'tRNS':
            trns = body
        elif tag == b'IDAT':
            idat += body
        elif tag == b'IEND':
            break
    if interlace:
        raise ValueError('interlaced PNGs are not supported')
    if depth != 8:
        raise ValueError('only 8-bit PNGs are supported (got %d)' % depth)
    bpp = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[ctype]
    flat = _unfilter(zlib.decompress(bytes(idat)), w * bpp, bpp, h)
    if ctype == 6:
        return Image.from_bytes(w, h, flat)
    px = array('I', bytes(4 * w * h))
    if ctype == 2:
        key = tuple(trns[1::2]) if trns else None
        for i in range(w * h):
            r, g, b = flat[i * 3], flat[i * 3 + 1], flat[i * 3 + 2]
            px[i] = 0 if key == (r, g, b) else rgba(r, g, b)
    elif ctype == 3:
        table = []
        for i in range(len(palette) // 3):
            a = trns[i] if trns and i < len(trns) else 255
            table.append(rgba(palette[i * 3], palette[i * 3 + 1], palette[i * 3 + 2], a) if a else 0)
        px = array('I', [table[v] for v in flat])
    elif ctype == 0:
        px = array('I', [rgba(v, v, v) for v in flat])
    elif ctype == 4:
        px = array('I', [rgba(flat[i], flat[i], flat[i], flat[i + 1]) if flat[i + 1] else 0 for i in range(0, len(flat), 2)])
    return Image(w, h, px)


def sheet(frames, columns=None, gap=0, background=CLEAR):
    """Lay equally sized images out in a grid (a horizontal strip by default)."""
    if not frames:
        return Image(1, 1)
    columns = columns or len(frames)
    fw, fh = frames[0].w, frames[0].h
    rows = (len(frames) + columns - 1) // columns
    out = Image(columns * fw + (columns - 1) * gap, rows * fh + (rows - 1) * gap, fill=background)
    for i, frame in enumerate(frames):
        out.paste(frame, (i % columns) * (fw + gap), (i // columns) * (fh + gap))
    return out


def strip_frames(image, frame_w, frame_h=None):
    """Cut a horizontal strip (or a grid) into frames, left to right, top to bottom."""
    frame_h = frame_h or image.h
    return [image.crop(x, y, frame_w, frame_h) for y in range(0, image.h, frame_h) for x in range(0, image.w, frame_w)]
