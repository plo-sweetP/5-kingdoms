"""
Where the Tiny Swords packs are and how to read them. The packs stay outside the repo (their license allows using and
changing the art in the game, not passing the files on); every file the build reads is recorded, so the build's
output doubles as the list of what the game uses.
"""
import os

from ase import AseFile
from px import Image, hexc

# An ordinary folder in the user's profile (C:\Users\<name>\5Kingdoms). Not under AppData: the Claude desktop app
# gives its sessions a private copy of AppData that Explorer, Unity and other programs can't see.
DEFAULT_ROOT = os.path.join(os.path.expanduser('~'), '5Kingdoms', 'ArtPacks', 'TinySwords')
FREE = 'Tiny Swords (Free Pack)'
ENEMY = 'Tiny Swords (Enemy Pack)/Enemy Pack'
UNITS = FREE + '/Units/Units (aseprite in Blue only)'

# The pack's palette. Units are drawn in Blue; the other team colours swap exactly these cloth and metal tones.
OUTLINE = hexc('#161c2e')
CLOTH_DARK, CLOTH_LIGHT = hexc('#485884'), hexc('#4697ac')
METAL_DARK, METAL, METAL_LIGHT = hexc('#688c8a'), hexc('#9cbeaa'), hexc('#d4edc2')
SKIN, SKIN_DARK = hexc('#efe1ab'), hexc('#c8a876')
BROWN_LIGHT, BROWN, BROWN_DARK, BROWN_DEEP = hexc('#ada081'), hexc('#977b6b'), hexc('#5e5455'), hexc('#474144')
WHITE = hexc('#ffffff')
SHADOW = hexc('#0f121a4f')


class Pack:
    def __init__(self, root=None):
        self.root = root or DEFAULT_ROOT
        if not os.path.isdir(os.path.join(self.root, FREE)):
            raise SystemExit('Tiny Swords packs not found under %s (pass --pack FOLDER)' % self.root)
        self.used = set()
        self._cache = {}

    def path(self, relative):
        return os.path.join(self.root, relative.replace('/', os.sep))

    def ase(self, relative):
        if relative not in self._cache:
            self._cache[relative] = AseFile(self.path(relative))
            self.used.add(relative)
        return self._cache[relative]

    def png(self, relative):
        if relative not in self._cache:
            self._cache[relative] = Image.load(self.path(relative))
            self.used.add(relative)
        return self._cache[relative]
