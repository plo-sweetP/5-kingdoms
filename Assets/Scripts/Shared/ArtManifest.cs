using System;
using UnityEngine;

namespace FiveKingdoms
{
    /// <summary>
    /// What Tools/pixelart/build_art.py wrote next to the sprites (Resources/art_manifest.json): how the art's pieces
    /// fit together. Frame sizes, pivots and animations of every strip; for the heroes' rigs, the layers and where the
    /// head sits on each frame; the heroes' head parts; the armor sets' head pieces and colours; ring auras; the HUD
    /// pieces' 9-slice borders. All positions are in pixels from a frame's top-left corner.
    /// </summary>
    [Serializable]
    public sealed class ArtManifest
    {
        public int pixelsPerUnit;
        public StripInfo[] strips;
        public MonsterInfo[] monsters;
        public RigInfo[] rigs;
        public HeroInfo[] heroes;
        public ArmorInfo[] armors;
        public CosmeticInfo[] cosmetics;
        public AuraInfo[] auras;
        public UiInfo[] ui;

        static ArtManifest loaded;

        /// <summary>"Enter Play Mode" keeps statics: read the file again each session, it may have been rebuilt.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => loaded = null;

        public static ArtManifest Current
        {
            get
            {
                if (loaded != null) return loaded;
                var text = Resources.Load<TextAsset>("art_manifest");
                if (text == null)
                {
                    Debug.LogError("Resources/art_manifest.json is missing: run Tools/pixelart/build_art.py.");
                    loaded = new ArtManifest();
                }
                else
                {
                    loaded = JsonUtility.FromJson<ArtManifest>(text.text);
                }
                loaded.strips ??= Array.Empty<StripInfo>();
                loaded.monsters ??= Array.Empty<MonsterInfo>();
                loaded.rigs ??= Array.Empty<RigInfo>();
                loaded.heroes ??= Array.Empty<HeroInfo>();
                loaded.armors ??= Array.Empty<ArmorInfo>();
                loaded.cosmetics ??= Array.Empty<CosmeticInfo>();
                loaded.auras ??= Array.Empty<AuraInfo>();
                loaded.ui ??= Array.Empty<UiInfo>();
                return loaded;
            }
        }

        public StripInfo Strip(string name) => Array.Find(strips, strip => strip.name == name);
        public MonsterInfo Monster(string id) => Array.Find(monsters, monster => monster.id == id);
        public HeroInfo Hero(string id) => Array.Find(heroes, hero => hero.id == id);
        public ArmorInfo Armor(string id) => Array.Find(armors, armor => armor.id == id);
        public CosmeticInfo Cosmetic(string id) => Array.Find(cosmetics, cosmetic => cosmetic.id == id);
        public AuraInfo Aura(string id) => Array.Find(auras, aura => aura.id == id);
        public UiInfo Ui(string name) => Array.Find(ui, piece => piece.name == name);

        /// <summary>The rig a weapon is held on (the weapon type picks the body), or null for an unknown weapon.</summary>
        public RigInfo RigOf(string weaponId) =>
            Array.Find(rigs, rig => Array.Exists(rig.weapons, weapon => weapon.id == weaponId));
    }

    /// <summary>Frames of equal size in a grid, left to right, top to bottom.</summary>
    [Serializable]
    public sealed class StripInfo
    {
        public string name;
        public string path;
        public int w, h, columns, count;
        /// <summary>The point of a frame that stands on its tile's centre (or the effect's position).</summary>
        public int pivotX, pivotY;
        public bool loop;
        public int fps;
        /// <summary>The frame on which an attack lands, or -1.</summary>
        public int impact;
    }

    [Serializable]
    public sealed class MonsterInfo
    {
        public string id;
        public string portrait;
        public StripInfo shadow;
        public StripInfo[] anims;
    }

    [Serializable]
    public sealed class RigInfo
    {
        public string id;
        public int cellW, cellH, pivotX, pivotY;
        /// <summary>The weapon hand at rest, in the cell: where a ring twinkles.</summary>
        public int handX, handY;
        public RigAnim[] anims;
        /// <summary>Per frame of the rig (all animations in a row): where a head's origin is, in the cell.</summary>
        public int[] headX, headY;
        public RigLayer[] layers;
        public StripInfo shadow;
        public WeaponInfo[] weapons;

        public RigLayer Layer(string name) => Array.Find(layers, layer => layer.name == name);
    }

    [Serializable]
    public sealed class RigAnim
    {
        public string name;
        public int start, count;
        public bool loop;
        public int fps;
        public int impact;
    }

    /// <summary>One layer's frames, cropped to the box (x, y, w, h) of the rig's cell that the layer ever touches.</summary>
    [Serializable]
    public sealed class RigLayer
    {
        public string name;
        public string path;
        public int x, y, w, h, columns;
    }

    [Serializable]
    public sealed class WeaponInfo
    {
        public string id;
        public string name;
    }

    [Serializable]
    public sealed class HeroInfo
    {
        public string id;
        /// <summary>The default look until gear exists.</summary>
        public string weapon, armor;
        public bool skirt;
        public PartInfo[] parts;

        public PartInfo Part(string name) => Array.Find(parts, part => part.name == name);
    }

    /// <summary>A piece drawn in head space: (x, y) is its corner relative to the head's origin.</summary>
    [Serializable]
    public sealed class PartInfo
    {
        public string name;
        public string path;
        public int x, y, w, h;
    }

    [Serializable]
    public sealed class ArmorInfo
    {
        public string id;
        public string name;
        /// <summary>How much hair shows with the head piece on: "full", "bangs" or "side".</summary>
        public string hair;
        public string[] cloth;
        public string[] metal;
        public PartInfo piece;
        public PartInfo plume;
    }

    /// <summary>A head piece that isn't part of an armor set (a hair bow, a crown, a headband): worn instead of the set's.</summary>
    [Serializable]
    public sealed class CosmeticInfo
    {
        public string id;
        public string name;
        public string hair;
        public PartInfo piece;
    }

    /// <summary>A ring set's colour: it twinkles on its wearer's hand.</summary>
    [Serializable]
    public sealed class AuraInfo
    {
        public string id;
        public string name;
        public string color;
    }

    [Serializable]
    public sealed class UiInfo
    {
        public string name;
        public string path;
        public int left, bottom, right, top;
    }
}
