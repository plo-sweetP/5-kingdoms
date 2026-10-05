using System;

namespace FiveKingdoms.Core
{
    /// <summary>What a skill does when used.</summary>
    public enum SkillEffect
    {
        /// <summary>
        /// A melee hit on the faced enemy (or any adjacent one): <see cref="SkillDefinition.Hits"/> hits of Power% ATK each
        /// (a basic attack is 200%). Can also pierce to the enemy behind, shove, stun or put a status on the target.
        /// </summary>
        Strike,

        /// <summary>
        /// Heals: the user (<see cref="HealTarget.Self"/>), the most hurt of the user and its neighbors, or every ally
        /// within Radius. Power is a percent of the user's or of each target's max HP (<see cref="SkillDefinition.HealsFromUser"/>).
        /// </summary>
        Heal,

        /// <summary>Moves the user up to Power tiles in a straight line in one quick motion.</summary>
        Dash,

        /// <summary>
        /// A ranged hit on any enemy in sight within Range tiles (walls and wall corners block it, allies don't). Can roll
        /// first (leaving a trap behind), knock the target back, slow it or put a status on it.
        /// </summary>
        Shot,

        /// <summary>The user and allies within Radius take Power% less damage until the user's next turn.</summary>
        Guard,

        /// <summary>
        /// No damage: marks a foe in sight within Range, which then takes Power% more damage from the user for StatusTurns
        /// of its turns. One mark per user; it jumps to the nearest foe when the marked one falls.
        /// </summary>
        Mark,

        /// <summary>
        /// Rains down on a foe in sight within Range (aimed, or the nearest), hitting every foe within Radius of it:
        /// <see cref="SkillDefinition.Hits"/> hits of Power% ATK each (Volley).
        /// </summary>
        Area,

        /// <summary>
        /// An aura around the user for StatusTurns of its own turns: the user and the allies next to it take StatusPower%
        /// less damage, and at the start of each of its turns it heals them all for Power% of its own max HP (Aura of
        /// Protection).
        /// </summary>
        Aura,
    }

    /// <summary>Who a heal reaches.</summary>
    public enum HealTarget
    {
        Self,

        /// <summary>The most hurt (by share of max HP) of the user and the allies next to it.</summary>
        SelfOrAdjacentAlly,

        /// <summary>The user and every ally within the skill's Radius.</summary>
        AlliesInRadius,
    }

    /// <summary>Floor traps a skill can leave behind (only enemies set them off).</summary>
    public enum TrapKind { None, Snare }

    /// <summary>
    /// One of a character's three skills, or its ultimate (PROGRESSION.md, "Skill resources"). There is no mana: a skill
    /// can't be used two turns in a row (a 1-turn cooldown), and the hero's weapon attack is always there instead. An
    /// ultimate has no cooldown but needs a full charge meter. Each skill also has its own action-value cost: a Quick
    /// skill (half a turn) brings the user's next turn sooner. Tags (physical or magic, melee, ranged or area, element)
    /// are what gear sets and weapon passives key off. Strikes and shots can also move the user first, shove or knock
    /// the target back, slow or stun it (both push its next turn back on the timeline), or put a status on it.
    /// </summary>
    public sealed class SkillDefinition
    {
        /// <summary>Skills at or under this AV cost are Quick (half a turn); the HUD tags them.</summary>
        public const int QuickCostPercent = 50;

        /// <summary>Every skill sits out the user's next turn after use, so the same one can't be used twice in a row.</summary>
        public const int DefaultCooldown = 1;

        public SkillDefinition(string id, string name, string shortName, SkillEffect effect, int power,
            int costPercent = 100, int cooldown = DefaultCooldown, bool ultimate = false,
            DamageKind kind = DamageKind.Physical, AttackReach reach = AttackReach.Melee, Element element = Element.None,
            int hits = 1, int range = 1, int radius = 0, int delayPercent = 0,
            StatusKind? status = null, int statusPower = 0, int statusTurns = 0,
            HealTarget healTarget = HealTarget.Self, bool healsFromUser = false,
            bool pierce = false, bool shove = false, int wallBonusPercent = 0, int knockback = 0,
            int stunChance = 0, int stunPercent = CombatRules.MaxDelayPercent, int rollTiles = 0, TrapKind leavesTrap = TrapKind.None,
            WeaponFamily weapon = WeaponFamily.None)
        {
            Id = id;
            Name = name;
            ShortName = shortName;
            Effect = effect;
            Power = power;
            IsUltimate = ultimate;
            CostPercent = costPercent;
            Cooldown = ultimate ? 0 : cooldown;
            Kind = kind;
            Reach = reach;
            Element = element;
            Hits = hits;
            Range = range;
            Radius = radius;
            DelayPercent = delayPercent;
            Status = status;
            StatusPower = statusPower;
            StatusTurns = statusTurns;
            HealTarget = healTarget;
            HealsFromUser = healsFromUser;
            Pierce = pierce;
            Shove = shove;
            WallBonusPercent = wallBonusPercent;
            Knockback = knockback;
            StunChance = stunChance;
            StunPercent = stunPercent;
            RollTiles = rollTiles;
            LeavesTrap = leavesTrap;
            Weapon = weapon;
        }

        /// <summary>
        /// A copy with something changed: how a class option upgrades a skill (PROGRESSION.md, "Classes": upgrades are
        /// modifiers on skill data). The catalog's own skill is never touched, and the copy keeps its id, so it still
        /// is that skill to the loadout, the saves and the view.
        /// </summary>
        public SkillDefinition Change(Action<SkillDefinition> change)
        {
            var copy = (SkillDefinition)MemberwiseClone();
            change(copy);
            return copy;
        }

        /// <summary>Stable key (saves, and for later: the manga-panel and cutscene hooks look skills up by it).</summary>
        public string Id { get; }
        public string Name { get; }

        /// <summary>Fits on a skill button.</summary>
        public string ShortName { get; }

        public SkillEffect Effect { get; }

        /// <summary>
        /// Damage per hit in percent of ATK (Strike, Shot, Area), heal percent of max HP (Heal, Aura), tiles (Dash),
        /// percent of damage blocked (Guard) or extra damage from the user in percent (Mark).
        /// </summary>
        public int Power { get; internal set; }

        /// <summary>An ultimate: used with a full charge meter instead of a cooldown (<see cref="ActorDefinition.Ultimate"/>).</summary>
        public bool IsUltimate { get; }

        /// <summary>Action-value cost in percent of a normal turn.</summary>
        public int CostPercent { get; internal set; }

        /// <summary>The user's own turns it sits out after use (1: not two turns in a row). Ultimates have none.</summary>
        public int Cooldown { get; internal set; }

        public DamageKind Kind { get; }
        public AttackReach Reach { get; }
        public Element Element { get; }

        /// <summary>Strikes: how many hits, each rolling damage and crit on its own.</summary>
        public int Hits { get; internal set; }

        /// <summary>Shots, marks and area skills: how far it reaches, in tiles.</summary>
        public int Range { get; internal set; }

        /// <summary>
        /// Heals, guards and auras: how close an ally must be to share it (0 = the user only). Area skills: the size of
        /// the area around the target (1 = 3x3).
        /// </summary>
        public int Radius { get; internal set; }

        /// <summary>
        /// Pushes each enemy hit back on the timeline by this percent of one of its turns (a slow), within the delay
        /// budget (<see cref="CombatRules.DelayCap"/>, and at most once per the enemy's own turn).
        /// </summary>
        public int DelayPercent { get; internal set; }

        /// <summary>A status put on each enemy hit, with its power and length in the enemy's own turns.</summary>
        public StatusKind? Status { get; internal set; }
        public int StatusPower { get; internal set; }
        public int StatusTurns { get; internal set; }

        public HealTarget HealTarget { get; internal set; }

        /// <summary>Heals: Power is a percent of the user's max HP (a tanky healer heals more) instead of each target's.</summary>
        public bool HealsFromUser { get; internal set; }

        /// <summary>Strikes: also hits the enemy standing right behind the target.</summary>
        public bool Pierce { get; internal set; }

        /// <summary>Strikes: shoves the target one tile away; if it can't move, the hit does <see cref="WallBonusPercent"/>% more.</summary>
        public bool Shove { get; internal set; }
        public int WallBonusPercent { get; internal set; }

        /// <summary>Shots: knocks the target back this many tiles, straight away from the shooter (onto a trap, say).</summary>
        public int Knockback { get; internal set; }

        /// <summary>Base chance in percent to stun, scaled by the user's Affinity and the target's Resist.</summary>
        public int StunChance { get; internal set; }

        /// <summary>
        /// A stun pushes the target's next turn back by this percent of a turn instead of skipping it (a boss by at most
        /// <see cref="CombatRules.MaxBossDelayPercent"/>), and never twice before the target acts: no stun-lock.
        /// </summary>
        public int StunPercent { get; internal set; }

        /// <summary>Moves (rolls) this many tiles the way it's aimed before acting.</summary>
        public int RollTiles { get; internal set; }

        /// <summary>A trap left on the tile the user rolled away from.</summary>
        public TrapKind LeavesTrap { get; internal set; }

        /// <summary>
        /// The weapon family it needs (PROGRESSION.md, "Classes"): it only goes in the loadout of a hero holding such a
        /// weapon. None: any hero can use it.
        /// </summary>
        public WeaponFamily Weapon { get; }

        public bool DealsDamage => Effect == SkillEffect.Strike || Effect == SkillEffect.Shot || Effect == SkillEffect.Area;

        /// <summary>Hits from a distance: weaker than an equivalent melee hit, and weaker still with a foe adjacent.</summary>
        public bool IsRanged => Reach != AttackReach.Melee;

        /// <summary>Aimed at a foe or a direction (strikes, shots, marks, areas, rolls, dashes); heals, guards and auras just happen.</summary>
        public bool NeedsAim => Effect != SkillEffect.Heal && Effect != SkillEffect.Guard && Effect != SkillEffect.Aura;

        /// <summary>Half a turn or less: the HUD shows a small "Quick" tag.</summary>
        public bool IsQuick => CostPercent <= QuickCostPercent;
    }

    /// <summary>
    /// Every skill. The starting kits are the approved table in PROGRESSION.md ("Starting kits"); numbers are first
    /// drafts to tune with -balance and playtests. Every skill has the 1-turn cooldown unless it says otherwise.
    /// </summary>
    public static class SkillCatalog
    {
        /// <summary>Ranged reach (PROGRESSION.md, "Ranged vs melee"): shots, marks and the Volley reach 5 tiles.</summary>
        public const int RangedReach = 5;

        // ---- Uzuki, Archer: utility ranged DPS built around traps. His Hunter Bow doubles up his shots (Multishot). ----

        /// <summary>Quick: marks a foe in sight; it takes 25% more damage from Uzuki for 3 of its turns, and the mark jumps on a kill.</summary>
        public static readonly SkillDefinition HuntersMark = new SkillDefinition("hunters_mark", "Hunter's Mark", "Mark",
            SkillEffect.Mark, power: 25, costPercent: SkillDefinition.QuickCostPercent, range: RangedReach,
            status: StatusKind.Mark, statusTurns: 3);

        /// <summary>A heavy shot (300% ATK) that knocks the target back a tile, e.g. onto a trap.</summary>
        public static readonly SkillDefinition PowerShot = new SkillDefinition("power_shot", "Power Shot", "Power",
            SkillEffect.Shot, power: 300, reach: AttackReach.Ranged, range: RangedReach, knockback: 1, weapon: WeaponFamily.Bow);

        /// <summary>
        /// Roll 2 tiles, then shoot (150% ATK) the nearest foe in sight: out of melee and attacking in one turn. A snare
        /// trap stays on the tile he left, rooting the first enemy that steps on it.
        /// </summary>
        public static readonly SkillDefinition RollingShot = new SkillDefinition("rolling_shot", "Rolling Shot", "Roll",
            SkillEffect.Shot, power: 150, reach: AttackReach.Ranged, range: RangedReach,
            rollTiles: 2, leavesTrap: TrapKind.Snare, weapon: WeaponFamily.Bow);

        /// <summary>Ultimate: arrows rain on a 3x3 area around a foe in sight, two hits of 200% ATK on every foe there.</summary>
        public static readonly SkillDefinition Volley = new SkillDefinition("volley", "Volley", "Volley",
            SkillEffect.Area, power: 200, ultimate: true, reach: AttackReach.Area, hits: 2, range: RangedReach, radius: 1, weapon: WeaponFamily.Bow);

        // ---- Haiden, Paladin: a tank first, with some healing. ----

        /// <summary>Heals Haiden or the most hurt ally next to him for 20% of Haiden's max HP.</summary>
        public static readonly SkillDefinition PaladinHeal = new SkillDefinition("paladin_heal", "Heal", "Heal",
            SkillEffect.Heal, power: 20, healTarget: HealTarget.SelfOrAdjacentAlly, healsFromUser: true);

        /// <summary>A smite (250% ATK), Fire until a Light element is decided.</summary>
        public static readonly SkillDefinition DivineStrike = new SkillDefinition("divine_strike", "Divine Strike", "Smite",
            SkillEffect.Strike, power: 250, element: Element.Fire, weapon: WeaponFamily.Sword);

        /// <summary>
        /// Shoves the target a tile (120% ATK) and makes it attack Haiden on its next turn. Against a wall it can't move,
        /// so the hit does 50% more instead.
        /// </summary>
        public static readonly SkillDefinition ShoulderBash = new SkillDefinition("shoulder_bash", "Shoulder Bash", "Bash",
            SkillEffect.Strike, power: 120, shove: true, wallBonusPercent: 50,
            status: StatusKind.Taunt, statusTurns: 1, weapon: WeaponFamily.Sword);

        /// <summary>
        /// Ultimate: for 3 of Haiden's turns, he and the allies next to him take 30% less damage, and at the start of each
        /// of his turns he heals them all (himself too) for 10% of his max HP.
        /// </summary>
        public static readonly SkillDefinition AuraOfProtection = new SkillDefinition("aura_of_protection", "Aura of Protection", "Aura",
            SkillEffect.Aura, power: 10, ultimate: true, radius: 1, statusPower: 30, statusTurns: 3);

        // ---- Kristela, Monk: speed-build melee DPS. ----

        /// <summary>A punch (220% ATK) that also hits the enemy right behind the target.</summary>
        public static readonly SkillDefinition PiercingPunch = new SkillDefinition("piercing_punch", "Piercing Punch", "Pierce",
            SkillEffect.Strike, power: 220, pierce: true, weapon: WeaponFamily.Fists);

        /// <summary>Quick: Kristela heals herself for 25% of her max HP.</summary>
        public static readonly SkillDefinition KiHeal = new SkillDefinition("ki_heal", "Ki Heal", "Ki",
            SkillEffect.Heal, power: 25, costPercent: SkillDefinition.QuickCostPercent);

        /// <summary>
        /// 160% ATK with a 60% chance (Affinity against Resist) to stun: the target's next turn comes 50% of a turn later
        /// (a boss's 25%). No skipped turns, and nothing is delayed twice before it acts, so no stun-lock.
        /// </summary>
        public static readonly SkillDefinition StunStrike = new SkillDefinition("stun_strike", "Stun Strike", "Stun",
            SkillEffect.Strike, power: 160, stunChance: 60, stunPercent: 50, weapon: WeaponFamily.Fists);

        /// <summary>
        /// Ultimate: 5 rapid strikes of 80% ATK (moving on to another adjacent foe if the target falls), and it costs only
        /// 70% of a turn, so her next turn comes 30% sooner (the most any one effect may move a turn, PROGRESSION.md).
        /// </summary>
        public static readonly SkillDefinition FlurryOfBlows = new SkillDefinition("flurry_of_blows", "Flurry of Blows", "Flurry",
            SkillEffect.Strike, power: 80, ultimate: true, costPercent: 70, hits: 5, weapon: WeaponFamily.Fists);

        // ---- Milestone 1d's kit (Uzuki's before the party); kept for tests and future classes. ----

        /// <summary>A solid hit (240% ATK, a basic attack is 200%), but slow (125% of a turn).</summary>
        public static readonly SkillDefinition SpiritStrike = new SkillDefinition("spirit_strike", "Spirit Strike", "Strike",
            SkillEffect.Strike, power: 240, costPercent: 125);

        /// <summary>Heals the user for 50% of max HP.</summary>
        public static readonly SkillDefinition SecondWind = new SkillDefinition("second_wind", "Second Wind", "Heal",
            SkillEffect.Heal, power: 50);

        /// <summary>A unique effect: three tiles in half a turn, then a longer cooldown. Good for escaping a slam.</summary>
        public static readonly SkillDefinition Dash = new SkillDefinition("dash", "Dash", "Dash",
            SkillEffect.Dash, power: 3, costPercent: 50, cooldown: 4);
    }
}
