"""
Reads Aseprite files (.aseprite / .ase): layers, cels, animation tags and the palette, enough to take a unit's
layers apart. Follows the published file format (aseprite/docs/ase-file-specs.md). Standard library only.
"""
import struct
import zlib
from array import array

from px import Image, rgba

LAYER_IMAGE, LAYER_GROUP, LAYER_TILEMAP = 0, 1, 2


class Layer:
    def __init__(self, index, name, flags, kind, child_level, blend, opacity):
        self.index, self.name, self.flags, self.kind = index, name, flags, kind
        self.child_level, self.blend, self.opacity = child_level, blend, opacity
        self.parent = None

    @property
    def visible(self):
        layer = self
        while layer is not None:
            if not layer.flags & 1:
                return False
            layer = layer.parent
        return True

    @property
    def path(self):
        return self.name if self.parent is None else self.parent.path + '/' + self.name

    def __repr__(self):
        return 'Layer(%d, %r%s)' % (self.index, self.path, '' if self.visible else ', hidden')


class Cel:
    def __init__(self, layer_index, x, y, opacity, z_index, image=None, link=None):
        self.layer_index, self.x, self.y, self.opacity, self.z_index = layer_index, x, y, opacity, z_index
        self.image, self.link = image, link


class Tag:
    def __init__(self, name, start, end, direction):
        self.name, self.start, self.end, self.direction = name, start, end, direction

    @property
    def frames(self):
        return list(range(self.start, self.end + 1))

    def __repr__(self):
        return 'Tag(%r, %d-%d)' % (self.name, self.start, self.end)


class AseFile:
    def __init__(self, path):
        with open(path, 'rb') as f:
            data = f.read()
        self.path = path
        (_, magic, frame_count, self.width, self.height, self.depth, self.flags, _speed, _, _,
         self.transparent_index, _colors) = struct.unpack_from('<IHHHHHIHIIB3xH', data, 0)
        if magic != 0xA5E0:
            raise ValueError('%s is not an Aseprite file' % path)
        self.layers = []
        self.tags = []
        self.palette = [0] * 256
        self.durations = []
        self.cels = []  # Per frame: {layer index: Cel}.
        pos = 128
        for _ in range(frame_count):
            frame_bytes, frame_magic, old_chunks, duration, new_chunks = struct.unpack_from('<IHHH2xI', data, pos)
            if frame_magic != 0xF1FA:
                raise ValueError('bad frame header in %s' % path)
            self.durations.append(duration)
            cels = {}
            self.cels.append(cels)
            chunk_pos = pos + 16
            for _ in range(new_chunks or old_chunks):
                size, kind = struct.unpack_from('<IH', data, chunk_pos)
                body = data[chunk_pos + 6:chunk_pos + size]
                if kind == 0x2004:
                    self._read_layer(body)
                elif kind == 0x2005:
                    cel = self._read_cel(body)
                    if cel is not None:
                        cels[cel.layer_index] = cel
                elif kind == 0x2018:
                    self._read_tags(body)
                elif kind == 0x2019:
                    self._read_palette(body)
                elif kind == 0x0004 and not any(self.palette):
                    self._read_old_palette(body)
                chunk_pos += size
            pos += frame_bytes

    # ---- chunks ----

    @staticmethod
    def _string(body, pos):
        length, = struct.unpack_from('<H', body, pos)
        return body[pos + 2:pos + 2 + length].decode('utf-8', 'replace'), pos + 2 + length

    def _read_layer(self, body):
        flags, kind, child_level, _, _, blend, opacity = struct.unpack_from('<HHHHHHB3x', body, 0)
        name, _ = self._string(body, 16)
        if not self.flags & 1:
            opacity = 255
        layer = Layer(len(self.layers), name, flags, kind, child_level, blend, opacity)
        for earlier in reversed(self.layers):  # The nearest earlier group one level up is the parent.
            if earlier.child_level < child_level:
                if earlier.kind == LAYER_GROUP and earlier.child_level == child_level - 1:
                    layer.parent = earlier
                break
        self.layers.append(layer)

    def _read_cel(self, body):
        layer_index, x, y, opacity, kind, z_index = struct.unpack_from('<HhhBHh5x', body, 0)
        if kind == 1:
            link, = struct.unpack_from('<H', body, 16)
            return Cel(layer_index, x, y, opacity, z_index, link=link)
        if kind not in (0, 2):
            return None  # Tilemaps aren't used by the packs.
        w, h = struct.unpack_from('<HH', body, 16)
        raw = body[20:] if kind == 0 else zlib.decompress(body[20:])
        return Cel(layer_index, x, y, opacity, z_index, image=self._pixels(w, h, raw))

    def _pixels(self, w, h, raw):
        if self.depth == 32:
            return Image.from_bytes(w, h, raw[:w * h * 4])
        if self.depth == 16:
            return Image(w, h, array('I', [rgba(raw[i], raw[i], raw[i], raw[i + 1]) if raw[i + 1] else 0
                                           for i in range(0, w * h * 2, 2)]))
        table = list(self.palette)
        table[self.transparent_index] = 0
        return Image(w, h, array('I', [table[v] for v in raw[:w * h]]))

    def _read_tags(self, body):
        count, = struct.unpack_from('<H', body, 0)
        pos = 10
        for _ in range(count):
            start, end, direction = struct.unpack_from('<HHB', body, pos)
            name, pos = self._string(body, pos + 17)
            self.tags.append(Tag(name, start, end, direction))

    def _read_palette(self, body):
        size, first, last = struct.unpack_from('<III', body, 0)
        if size > len(self.palette):
            self.palette += [0] * (size - len(self.palette))
        pos = 20
        for i in range(first, last + 1):
            flags, r, g, b, a = struct.unpack_from('<HBBBB', body, pos)
            pos += 6
            if flags & 1:
                _, pos = self._string(body, pos)
            self.palette[i] = rgba(r, g, b, a)

    def _read_old_palette(self, body):
        packets, = struct.unpack_from('<H', body, 0)
        pos, index = 2, 0
        for _ in range(packets):
            skip, count = body[pos], body[pos + 1] or 256
            pos += 2
            index += skip
            for _ in range(count):
                self.palette[index] = rgba(body[pos], body[pos + 1], body[pos + 2])
                pos += 3
                index += 1

    # ---- queries ----

    @property
    def frame_count(self):
        return len(self.cels)

    def tag(self, name):
        for tag in self.tags:
            if tag.name == name:
                return tag
        raise KeyError('%s has no tag %r (it has %s)' % (self.path, name, [t.name for t in self.tags]))

    def layer(self, name):
        """A layer by name or by its path through groups ('Group/Name')."""
        found = [l for l in self.layers if l.name == name or l.path == name]
        if len(found) != 1:
            raise KeyError('%s: %d layers match %r (layers: %s)' % (self.path, len(found), name, [l.path for l in self.layers]))
        return found[0]

    def image_layers(self):
        return [l for l in self.layers if l.kind == LAYER_IMAGE]

    def cel(self, frame, layer):
        """The cel of a layer at a frame with links followed, or None."""
        index = layer.index if isinstance(layer, Layer) else layer
        cel = self.cels[frame].get(index)
        if cel is not None and cel.link is not None:
            target = self.cels[cel.link].get(index)
            if target is None:
                return None
            return Cel(index, target.x, target.y, cel.opacity, cel.z_index, image=target.image)
        return cel

    def layer_image(self, frame, layer, opacity=True):
        """One layer of one frame on a canvas-sized image (clear where the layer has no cel)."""
        layer = self.layer(layer) if isinstance(layer, str) else layer
        out = Image(self.width, self.height)
        cel = self.cel(frame, layer)
        if cel is not None and cel.image is not None:
            alpha = (cel.opacity * layer.opacity // 255) if opacity else 255
            out.paste(cel.image, cel.x, cel.y, alpha)
        return out

    def render(self, frame, layers=None, hidden=False):
        """
        The frame flattened in drawing order. `layers` limits it to those layers (names, paths or Layer objects);
        hidden layers are skipped unless `hidden` is set or they are named.
        """
        if layers is not None:
            wanted = [self.layer(l) if isinstance(l, str) else l for l in layers]
        else:
            wanted = [l for l in self.image_layers() if hidden or l.visible]
        order = []
        for layer in wanted:
            cel = self.cel(frame, layer)
            if cel is not None and cel.image is not None:
                order.append((layer.index + cel.z_index, cel.z_index, layer, cel))
        order.sort(key=lambda item: (item[0], item[1]))
        out = Image(self.width, self.height)
        for _, _, layer, cel in order:
            out.paste(cel.image, cel.x, cel.y, cel.opacity * layer.opacity // 255)
        return out

    def describe(self):
        lines = ['%s: %dx%d, %d frames, depth %d' % (self.path, self.width, self.height, self.frame_count, self.depth)]
        for layer in self.layers:
            used = sum(1 for cels in self.cels if layer.index in cels)
            kind = {LAYER_IMAGE: '', LAYER_GROUP: ' [group]', LAYER_TILEMAP: ' [tilemap]'}[layer.kind]
            lines.append('  layer %2d %s%s%s opacity %d blend %d, %d cels' % (
                layer.index, '  ' * layer.child_level, layer.name, kind + ('' if layer.flags & 1 else ' (hidden)'),
                layer.opacity, layer.blend, used))
        for tag in self.tags:
            lines.append('  tag %-24s frames %d-%d (%d)' % (tag.name, tag.start, tag.end, tag.end - tag.start + 1))
        lines.append('  durations: %s' % sorted(set(self.durations)))
        return '\n'.join(lines)
