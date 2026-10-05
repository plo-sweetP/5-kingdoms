"""
HUD art from the pack's UI kit (docs/design/ART.md, "The rest of the game"). The kit's panels, buttons, bars and
ribbons come as 3 x 3 (or 3 x 1) pieces with gaps between them; here they are put back together into contiguous
textures with 9-slice borders, so the game can stretch them to any size while the corners keep their pixels.
Gold and dark buttons are recolours of the blue ones.
"""
import pack as P
from draw import Canvas
from px import Image, hexc

KIT = P.FREE + '/UI Elements/UI Elements'

# The kit's blue button colours (big and tiny buttons use slightly different blues) and what replaces them.
GOLD = {hexc('#41919d'): hexc('#dcaa46'), hexc('#f2eadb'): hexc('#fffaba'), hexc('#4a6982'): hexc('#ac7a3a'),
        hexc('#425062'): hexc('#886049'), hexc('#bda291'): hexc('#e8ce91'), hexc('#4ea39c'): hexc('#e9c35a'),
        hexc('#987771'): hexc('#6e4a3c'),
        hexc('#47829d'): hexc('#dcaa46'), hexc('#485884'): hexc('#886049'), hexc('#b6f5bf'): hexc('#fffaba'),
        hexc('#7dd5bc'): hexc('#f2d98a')}
DARK = {hexc('#41919d'): hexc('#525b66'), hexc('#f2eadb'): hexc('#c4b2af'), hexc('#4a6982'): hexc('#444553'),
        hexc('#425062'): hexc('#33343f'), hexc('#bda291'): hexc('#987771'), hexc('#4ea39c'): hexc('#5f6875'),
        hexc('#987771'): hexc('#6e5a5a'),
        hexc('#47829d'): hexc('#525b66'), hexc('#485884'): hexc('#33343f'), hexc('#b6f5bf'): hexc('#c4b2af'),
        hexc('#7dd5bc'): hexc('#987771')}


class Piece:
    """A UI texture and its 9-slice border in pixels: (left, bottom, right, top); all zero for a plain image."""

    def __init__(self, name, image, border=(0, 0, 0, 0)):
        self.name, self.image, self.border = name, image, border


def runs(flags):
    """[(start, end)] of the runs of True; runs less than 12 apart count as one (a piece may have a notch)."""
    out, start = [], None
    for i, flag in enumerate(list(flags) + [False]):
        if flag and start is None:
            start = i
        elif not flag and start is not None:
            if out and start - out[-1][1] < 12:
                out[-1] = (out[-1][0], i)
            else:
                out.append((start, i))
            start = None
    return out


def filled(image):
    cols = [any(image.px[y * image.w + x] >> 24 for y in range(image.h)) for x in range(image.w)]
    rows = [any(image.px[y * image.w + x] >> 24 for x in range(image.w)) for y in range(image.h)]
    return runs(cols), runs(rows)


def assemble(image, name):
    """Join a kit image's pieces into one texture; the outer pieces become the 9-slice border."""
    cols, rows = filled(image)
    assert len(cols) in (1, 3) and len(rows) in (1, 3), '%s: %d x %d pieces' % (name, len(cols), len(rows))
    out = Image(sum(c1 - c0 for c0, c1 in cols), sum(r1 - r0 for r0, r1 in rows))
    y = 0
    for r0, r1 in rows:
        x = 0
        for c0, c1 in cols:
            out.paste(image.crop(c0, r0, c1 - c0, r1 - r0), x, y)
            x += c1 - c0
        y += r1 - r0
    left, right = (cols[0][1] - cols[0][0], cols[2][1] - cols[2][0]) if len(cols) == 3 else (0, 0)
    top, bottom = (rows[0][1] - rows[0][0], rows[2][1] - rows[2][0]) if len(rows) == 3 else (0, 0)
    return Piece(name, out, (left, bottom, right, top))


def round_button(image, name):
    """A round button, trimmed; its whole outline is border, so a bigger button grows from the middle."""
    cut, _, _ = image.trimmed()
    half_w, half_h = cut.w // 2 - 1, cut.h // 2 - 1
    return Piece(name, cut, (half_w, half_h, half_w, half_h))


def tightened(piece):
    """
    The kit's pieces are 45-64 px wide, mostly flat edge that could stretch. This keeps only what can't: the border
    shrinks to where the middle starts repeating, and the repeating middle is cut down to a few pixels. Small panels
    and buttons then keep whole pixels instead of having their corners squeezed. Textured middles are left alone.
    """
    image = piece.image
    left, bottom, right, top = piece.border

    def run(count, same):
        """The longest stretch of identical lines around the middle: (start, end)."""
        a = b = count // 2
        while a > 0 and same(a - 1, a):
            a -= 1
        while b < count - 1 and same(b, b + 1):
            b += 1
        return a, b + 1

    column = lambda x: [image.px[y * image.w + x] for y in range(image.h)]
    row = lambda y: list(image.px[y * image.w:(y + 1) * image.w])
    columns = [column(x) for x in range(image.w)]
    x0, x1 = run(image.w, lambda i, j: columns[i] == columns[j])
    if left and x0 < left and piece.image.w - x1 < right:
        image = _joined(image.crop(0, 0, x0 + 2, image.h), image.crop(x1 - 2, 0, image.w - (x1 - 2), image.h), across=True)
        left, right = x0, piece.image.w - x1
    rows = [row(y) for y in range(image.h)]
    y0, y1 = run(image.h, lambda i, j: rows[i] == rows[j])
    if top and y0 < top and image.h - y1 < bottom:
        height = image.h
        image = _joined(image.crop(0, 0, image.w, y0 + 2), image.crop(0, y1 - 2, image.w, height - (y1 - 2)), across=False)
        top, bottom = y0, height - y1
    return Piece(piece.name, image, (left, bottom, right, top))


def _joined(a, b, across):
    out = Image(a.w + b.w, a.h) if across else Image(a.w, a.h + b.h)
    out.paste(a)
    out.paste(b, a.w if across else 0, 0 if across else a.h)
    return out


def frame_piece():
    """
    A slim frame for the HUD's small panels (party cards, labels, tags), where the kit's paper corners are too big:
    the pack's outline around a light fill with a shaded foot, white so the game can tint it any colour.
    """
    white, shade = hexc('#ffffff'), hexc('#cfd6dc')
    c = Canvas(16, 16)
    c.rect(1, 0, 14, 16, white)
    c.rect(0, 1, 16, 14, white)
    c.rect(1, 13, 14, 3, shade, only=(white,))
    c.rect(0, 13, 16, 2, shade, only=(white,))
    part = c.part()
    return Piece('frame', part.image, (7, 8, 7, 7))


# The kit's button colours: (face, rim, side) up, then pressed.
BUTTON_COLORS = {
    'blue': (('#41919d', '#f2eadb', '#4a6982'), ('#4a6982', '#bda291', '#425062')),
    'red': (('#f65555', '#fff657', '#ba4954'), ('#ba4954', '#d39c66', '#8d4874')),
    'gold': (('#dcaa46', '#fffaba', '#ac7a3a'), ('#ac7a3a', '#e8ce91', '#886049')),
    'dark': (('#525b66', '#c4b2af', '#444553'), ('#444553', '#987771', '#33343f')),
}


def button_pieces():
    """
    Rectangular buttons in the look and colours of the kit's (a light rim around the face, a darker side below, the
    pack's outline) but with corners small enough for the HUD's low buttons; the kit's own are built from 45 px
    corners. Pressed, the button sinks onto its side.
    """
    out = []
    for color, states in BUTTON_COLORS.items():
        for pressed, (face, rim, side) in enumerate(states):
            face, rim, side = hexc(face), hexc(rim), hexc(side)
            c = Canvas(20, 22)
            top = 3 if pressed else 0

            def block(x, y, w, h, colour):
                c.rect(x + 2, y, w - 4, h, colour)
                c.rect(x + 1, y + 1, w - 2, h - 2, colour)
                c.rect(x, y + 2, w, h - 4, colour)

            block(0, top, 20, 22 - top, side)
            block(0, top, 20, 18, rim)
            block(2, top + 2, 16, 14, face)
            # Both states keep the same size, so the pressed one sits lower in the same box.
            out.append(Piece('button_%s%s' % (color, '_down' if pressed else ''), c.part().image, (9, 11, 9, 12 if pressed else 9)))
    return out


def bar_pieces():
    """A slim bar's trough for HP on the party cards and the boss: dark, in the pack's outline (the game colours the fill)."""
    trough = Canvas(8, 4)
    trough.rect(0, 0, 8, 4, hexc('#383032'))
    back = trough.part()
    return [Piece('bar_slim', back.image, (3, 3, 3, 3))]


def brackets(pack):
    """The kit's selection brackets as a frame: they close around whatever is being aimed (a button, a card)."""
    cursor = pack.png(KIT + '/Cursors/Cursor_04.png')
    cols, rows = filled(cursor)
    out = Image(sum(c1 - c0 for c0, c1 in cols) + 4, sum(r1 - r0 for r0, r1 in rows) + 4)
    y = 0
    for r0, r1 in rows:
        x = 0
        for c0, c1 in cols:
            out.paste(cursor.crop(c0, r0, c1 - c0, r1 - r0), x, y)
            x += c1 - c0 + 4
        y += r1 - r0 + 4
    return Piece('brackets', out, (cols[0][1] - cols[0][0], rows[-1][1] - rows[-1][0], cols[-1][1] - cols[-1][0], rows[0][1] - rows[0][0]))


def dpad_pieces():
    """
    The touch D-pad: a dark see-through disc in the pack's outline, a hub for its middle, and an arrow head pointing
    up (white, tinted in the game and turned to the eight directions).
    """
    size = 208
    disc = Image(size, size)
    hub = Image(72, 72)
    for image, radius, fill in ((disc, size / 2.0 - 2, hexc('#161c2e78')), (hub, 34.0, hexc('#ffffff26'))):
        middle = image.w / 2.0
        for y in range(image.h):
            for x in range(image.w):
                d = ((x + 0.5 - middle) ** 2 + (y + 0.5 - middle) ** 2) ** 0.5
                if d <= radius - 2:
                    image.px[y * image.w + x] = fill
                elif d <= radius:
                    image.px[y * image.w + x] = hexc('#161c2ed0')
    c = Canvas(28, 24)
    c.polygon([(14, 2), (26, 20), (2, 20)], hexc('#ffffff'))
    c.polygon([(14, 12), (22, 20), (6, 20)], hexc('#cedce1'), only=(hexc('#ffffff'),))
    arrow = c.part().trimmed()
    # The diagonal one is drawn on its own (pointing up and right): turning pixel art by 45 degrees would smear it.
    d = Canvas(22, 22)
    d.polygon([(2, 2), (20, 2), (20, 20)], hexc('#ffffff'))
    d.polygon([(10, 6), (16, 6), (16, 12)], hexc('#cedce1'), only=(hexc('#ffffff'),))
    diagonal = d.part().trimmed()
    return [Piece('dpad', disc), Piece('dpad_hub', hub), Piece('dpad_arrow', arrow.image), Piece('dpad_arrow_diagonal', diagonal.image)]


def glyph_pieces():
    """
    The glyphs on the HUD's top-right buttons, in the pack's outline: an hourglass (Wait), a pair of berries (the
    Berry button, with its count beside them) and a pause sign.
    """
    white, shade, sand = hexc('#ffffff'), hexc('#cedce1'), hexc('#e8c170')
    h = Canvas(14, 18)
    h.rect(0, 0, 14, 2, white).rect(0, 16, 14, 2, shade)
    h.polygon([(1, 2), (13, 2), (8, 9), (6, 9)], white)
    h.polygon([(6, 9), (8, 9), (13, 16), (1, 16)], white)
    h.polygon([(4, 5), (10, 5), (8, 8), (6, 8)], sand, only=(white,))
    h.polygon([(5, 13), (9, 13), (12, 16), (2, 16)], sand, only=(white,))
    h.rect(6, 9, 2, 4, sand, only=(white,))

    p = Canvas(12, 14)
    for x in (0, 8):
        p.rect(x, 0, 4, 14, white).rect(x, 11, 4, 3, shade)

    red, red_dark, red_light = hexc('#e76161'), hexc('#924159'), hexc('#f9856c')
    leaf, leaf_dark = hexc('#6fae5f'), hexc('#3f6b4a')
    b = Canvas(20, 18)
    b.polygon([(8, 7), (10, 3), (12, 4), (10, 8)], leaf_dark)
    b.polygon([(10, 4), (15, 1), (18, 3), (15, 6), (11, 6)], leaf)
    b.polygon([(13, 4), (18, 3), (15, 6)], leaf_dark, only=(leaf,))
    for cx, cy in ((6.0, 11.5), (13.0, 12.5)):
        b.ellipse(cx, cy, 4.5, 4.5, red)
        b.ellipse(cx + 1.0, cy + 1.2, 3.6, 3.3, red_dark, only=(red,))
        b.ellipse(cx - 0.3, cy - 0.3, 3.2, 3.2, red, only=(red_dark,))
        b.dots(red_light, [(int(cx) - 2, int(cy) - 3), (int(cx) - 1, int(cy) - 3), (int(cx) - 2, int(cy) - 2)])
    return [Piece('icon_wait', h.part().image), Piece('icon_pause', p.part().image), Piece('icon_berry', b.part().trimmed().image)]


def build(pack):
    """
    Every HUD texture the game uses, as Pieces: only what DungeonHud, PartyPanel, HoldButton and DPad ask for by name
    goes into the app. The kit has more (paper panels, bars, more ribbon and button colours, twelve icons); add them
    here when a screen needs them.
    """
    png = lambda rel: pack.png(KIT + '/' + rel)
    pieces = []

    def add(piece, recolors=()):
        pieces.append(piece)
        for suffix, mapping in recolors:
            pieces.append(Piece(piece.name.replace('blue', suffix), piece.image.mapped(mapping), piece.border))

    # Rectangular buttons: Wait, Berry, Auto and Pause (dark), Descend and Auto while on (gold), Try Again (red).
    pieces.extend(piece for piece in button_pieces() if not piece.name.startswith('button_blue'))
    # Round buttons: the weapon attack (the kit's small red one), the skills (tiny blue) and the ultimate (dark while
    # it charges, gold when ready).
    add(round_button(png('Buttons/SmallRedRoundButton_Regular.png'), 'round_red'))
    add(round_button(png('Buttons/SmallRedRoundButton_Pressed.png'), 'round_red_down'))
    add(round_button(png('Buttons/TinyRoundBlueButton.png'), 'tiny_blue'), (('gold', GOLD), ('dark', DARK)))
    add(tightened(assemble(png('Papers/SpecialPaper.png'), 'panel')))       # The end-of-run panel.
    add(frame_piece())                                                      # Party cards, labels, tags.
    add(bar_pieces()[0])                                                    # HP bars' trough.
    add(brackets(pack))                                                     # Around the button being aimed.
    ribbons = png('Ribbons/BigRibbons.png')                                 # The dungeon's name on a floor change.
    _, rows = filled(ribbons)
    add(assemble(ribbons.crop(0, rows[0][0], ribbons.w, rows[0][1] - rows[0][0]), 'ribbon_blue'))
    for piece in dpad_pieces() + glyph_pieces():
        add(piece)
    return pieces
