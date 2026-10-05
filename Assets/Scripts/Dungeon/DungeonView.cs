using System.Collections;
using System.Collections.Generic;
using FiveKingdoms.Core;
using FiveKingdoms.UI;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FiveKingdoms.Dungeon
{
    /// <summary>
    /// Draws a DungeonRun and animates the GameEvents each action produces. The ground is the Tiny Swords terrain
    /// (docs/design/ART.md): floors are flat ground, walls are raised ground with cliff faces toward the rooms,
    /// decorations stand on the raised ground, the cave entrance is the way down. Actors play their art's animations
    /// (steps, attacks, shots, guards, the boss's wind-up, slam and recovery) with the pack's effects on top (dust,
    /// fire, heal light, shock rings), plus name pop-ups, statuses, the ultimate's charge and floor changes. Also shows
    /// the aiming highlight while the player picks a target. Holds no rules of its own: everything it shows comes from
    /// the run's state and events, and an animation never takes longer than the turn it belongs to.
    /// </summary>
    public sealed class DungeonView : MonoBehaviour
    {
        const float StepTime = 0.11f;
        const float DashTimePerTile = 0.05f;
        const float ArrowTimePerTile = 0.035f;
        const float LungeDistance = 0.24f;
        const float HeavyDrawTime = 0.2f;   // Power Shot's long draw.
        const float SlamImpactTime = 0.2f;
        const int WallPadding = 14; // Extra wall drawn past the map edge so the camera never shows the void.
        const int StairsOrder = 10;
        const int TrapOrder = 15;
        const int ItemOrder = 20;

        static readonly Color CritColor = new Color(1f, 0.85f, 0.2f);
        static readonly Color HeroHurtColor = new Color(1f, 0.5f, 0.45f);
        static readonly Color HealColor = new Color(0.45f, 1f, 0.5f);
        static readonly Color GoldColor = new Color(1f, 0.85f, 0.3f);

        static readonly Color WarningColor = new Color(1f, 0.62f, 0.3f);
        static readonly Color BossBurstColor = new Color(0.55f, 0.7f, 0.35f);

        static readonly Color SpiritColor = new Color(0.55f, 0.85f, 1f);
        static readonly Color SkillTextColor = new Color(0.7f, 0.9f, 1f);
        static readonly Color DashGhostColor = new Color(0.6f, 0.85f, 1f, 0.55f);
        static readonly Color FlurryGhostColor = new Color(1f, 0.9f, 0.6f, 0.6f);
        static readonly Color DustColor = new Color(0.78f, 0.7f, 0.58f);
        static readonly Color FireColor = new Color(1f, 0.6f, 0.25f);
        static readonly Color SlowColor = new Color(0.6f, 0.85f, 1f);
        static readonly Color GuardColor = new Color(0.55f, 0.75f, 1f);
        static readonly Color MarkColor = new Color(1f, 0.45f, 0.4f);
        static readonly Color GuardTint = new Color(0.75f, 0.85f, 1f);
        static readonly Color MarkTint = new Color(1f, 0.78f, 0.78f);
        static readonly Color SnareColor = new Color(0.9f, 0.75f, 0.45f);
        static readonly Color StunColor = new Color(1f, 0.88f, 0.35f);
        static readonly Color UltimateTextColor = new Color(1f, 0.82f, 0.3f);
        static readonly Color PunchTint = new Color(1f, 0.95f, 0.8f);
        static readonly Color ReachColor = new Color(0.45f, 0.72f, 1f, 0.22f);
        static readonly Color AreaColor = new Color(1f, 0.55f, 0.25f, 0.34f);
        static readonly Color TargetColor = new Color(1f, 0.5f, 0.4f, 0.9f);
        static readonly Color ChosenColor = new Color(1f, 0.92f, 0.45f);

        readonly Dictionary<int, ActorView> actors = new Dictionary<int, ActorView>();
        SkillDefinition activeSkill; // The skill whose effects are playing, if any.
        int activeSkillUser = -1;    // Whose it is: nobody else's blows are part of it.
        int strikesShown;            // Its strikes shown so far: past the skill's own, an attack is a plain one again.
        int counterBy = -1;          // The hero whose next blow is a Riposte's counter.
        int dancer = -1;             // The hero in a Blade Dance, until its last strike has been shown.
        int danceEndsAt;             // Where that strike is in the events being played.
        int danceStrikes;
        readonly HashSet<int> stances = new HashSet<int>(); // The heroes holding a counter stance's pose.

        static readonly Color BladeColor = new Color(1f, 0.98f, 0.88f);
        static readonly Color DanceGhostColor = new Color(1f, 0.86f, 0.45f, 0.8f);
        readonly Dictionary<int, SpriteRenderer> items = new Dictionary<int, SpriteRenderer>();
        readonly Dictionary<int, SpriteRenderer> traps = new Dictionary<int, SpriteRenderer>();
        readonly List<GameObject> warnings = new List<GameObject>();
        readonly List<GameObject> aimReach = new List<GameObject>();
        readonly List<GameObject> aimMarks = new List<GameObject>();
        AimInfo shownAim; // The aim whose reach is lit, so picking another target only redraws the marks.
        DungeonHud hud;
        PixelCamera pixelCamera;
        Tilemap ground;
        Tilemap walls;
        Transform actorRoot;
        Transform itemRoot;
        Transform effectRoot;
        Transform decorationRoot;
        SpriteRenderer stairs;
        Tile groundTile;
        readonly Tile[] wallTops = new Tile[8];  // Indexed by which neighbours are walls: north 4, east 2, west 1.
        readonly Tile[] wallFaces = new Tile[8];

        public static Vector3 TileCenter(GridPos p) => new Vector3(p.X + 0.5f, p.Y + 0.5f, 0f);

        public void Init(DungeonHud dungeonHud, PixelCamera camera)
        {
            hud = dungeonHud;
            pixelCamera = camera;

            var grid = new GameObject("Grid", typeof(Grid)).transform;
            grid.SetParent(transform, false);
            ground = CreateTilemap(grid, "Ground", 0);
            walls = CreateTilemap(grid, "Walls", 1);
            decorationRoot = CreateChild("Decorations");
            actorRoot = CreateChild("Actors");
            itemRoot = CreateChild("Items");
            effectRoot = CreateChild("Effects");

            groundTile = MakeTile("Tiles/ground", new Color(0.45f, 0.5f, 0.3f));
            for (int i = 0; i < 8; i++)
            {
                string key = $"{(i >> 2) & 1}{(i >> 1) & 1}{i & 1}";
                wallTops[i] = MakeTile("Tiles/wall_top_" + key, new Color(0.3f, 0.55f, 0.35f));
                wallFaces[i] = MakeTile("Tiles/wall_face_" + key, new Color(0.35f, 0.5f, 0.5f));
            }

            // The way down: the pack's cave entrance, eyes blinking in the dark.
            stairs = NewSprite("Stairs", transform, null, StairsOrder);
            stairs.gameObject.AddComponent<LoopingSprite>().Play(SpriteLibrary.Strip("Deco/cave"));
        }

        /// <summary>Redraws everything from the run's current state (new run or new floor).</summary>
        public void Rebuild(DungeonRun run)
        {
            foreach (var view in actors.Values) Destroy(view.gameObject);
            actors.Clear();
            foreach (var item in items.Values) Destroy(item.gameObject);
            items.Clear();
            foreach (var trap in traps.Values) Destroy(trap.gameObject);
            traps.Clear();
            lastWaitNote.Clear();
            ClearWarnings();
            ClearAim();

            DrawTerrain(run.Map);
            stairs.gameObject.SetActive(run.Map.InBounds(run.Map.Stairs));
            stairs.transform.position = TileCenter(run.Map.Stairs);
            stances.Clear();
            foreach (var actor in run.Actors) AddActor(actor);
            HeroComposer.ReleaseSources();
            foreach (var item in run.Items) AddItem(item);
            foreach (var trap in run.Traps) AddTrap(trap.Id, trap.Pos);

            var boss = run.Boss;
            if (boss != null) hud.ShowBoss(boss.Name, boss.Hp, boss.MaxHp);
            else hud.HideBoss();

            if (actors.TryGetValue(run.Hero.Id, out var hero))
            {
                pixelCamera.Target = hero.transform;
                pixelCamera.SnapToTarget();
            }
            UpdateSeeThrough(run);
        }

        /// <summary>Banner subtitle for the current floor, e.g. "B3F" or "B5F: Boss".</summary>
        public static string FloorTitle(DungeonRun run) => run.IsBossFloor ? $"B{run.Floor}F: Boss" : $"B{run.Floor}F";

        /// <summary>Animates one action's events in order. Everyone who walked this turn moves together, like Mystery Dungeon.</summary>
        public IEnumerator Play(DungeonRun run, IReadOnlyList<GameEvent> events)
        {
            activeSkill = null;
            activeSkillUser = counterBy = dancer = -1;
            bool expLogged = false;
            for (int i = 0; i < events.Count; i++)
            {
                switch (events[i])
                {
                    case LeaderChangedEvent leaderChanged:
                        if (actors.TryGetValue(leaderChanged.ActorId, out var newLeader))
                        {
                            pixelCamera.Target = newLeader.transform;
                            hud.AddMessage($"{newLeader.DisplayName} takes the lead.", GoldColor);
                        }
                        break;
                    case HeroWaitedEvent waited:
                        ShowWait(run, waited);
                        break;
                    case SwappedEvent swapped:
                        ShowSwap(swapped);
                        break;
                    case StatusAppliedEvent status:
                        ShowStatus(run, status);
                        break;
                    case StatusEndedEvent ended:
                        if (actors.TryGetValue(ended.ActorId, out var cleared))
                        {
                            ShowStatuses(cleared, run.FindActor(ended.ActorId));
                            if (ended.Kind == StatusKind.Riposte && stances.Remove(ended.ActorId)) cleared.SetStance(false);
                        }
                        break;
                    case CounterEvent counter:
                        counterBy = counter.ActorId;
                        if (actors.TryGetValue(counter.ActorId, out var riposter))
                        {
                            hud.ShowFloatingText(riposter.TextAnchor, "Riposte!", UltimateTextColor, 0.9f);
                            Effects.Burst(effectRoot, riposter.transform.position + Vector3.up * 0.3f, Color.white, 8, 2.5f); // The flash.
                            hud.AddMessage($"{riposter.DisplayName} turns the blow aside and strikes back!", GuardColor);
                        }
                        break;
                    case ChargeChangedEvent charge:
                        ShowCharge(run, charge);
                        break;
                    case AreaAttackEvent area:
                        if (activeSkill != null && activeSkill.Effect == SkillEffect.SharedStrikes) BeginDance(run, events, i, area);
                        else yield return AnimateVolley(run, area);
                        break;
                    case TrapPlacedEvent placed:
                        AddTrap(placed.TrapId, placed.Pos);
                        break;
                    case TrapTriggeredEvent triggered:
                        yield return AnimateTrap(triggered);
                        break;
                    case PushedEvent pushed when pushed.Blocked:
                        if (actors.TryGetValue(pushed.ActorId, out var pinned))
                            hud.ShowFloatingText(pinned.TextAnchor, pinned.IsBoss ? "Won't budge!" : "Pinned!", WarningColor, 0.8f);
                        break;
                    case TurnDelayedEvent delayed:
                        if (actors.TryGetValue(delayed.ActorId, out var slowed))
                        {
                            var color = delayed.Stun ? StunColor : SlowColor;
                            hud.ShowFloatingText(slowed.TextAnchor, delayed.Stun ? "Stunned!" : "Slowed", color, 0.8f);
                            if (delayed.Stun) Effects.Burst(effectRoot, slowed.TextAnchor, StunColor, 8, 2f); // Stars knocked loose.
                            hud.AddMessage($"{Subject(slowed)} is {(delayed.Stun ? "stunned" : "slowed")}: its next turn comes {delayed.Percent}% of a turn later.", color);
                            // The icon stays until its delayed turn comes: until then nothing can push it back again.
                            slowed.SetStatuses(run.FindActor(delayed.ActorId)?.Statuses, delayed: true);
                        }
                        break;
                    case SkillUsedEvent used:
                        activeSkill = used.Skill;
                        activeSkillUser = used.ActorId;
                        strikesShown = 0;
                        RingFlash(used.ActorId); // Stands in for a ring bonus firing until the gear rules exist.
                        yield return AnimateSkillUse(used);
                        break;
                    case DashedEvent dash:
                        yield return AnimateDash(dash);
                        break;
                    case ItemUsedEvent used:
                        if (actors.TryGetValue(used.ActorId, out var eater)) hud.AddMessage($"{eater.DisplayName} ate a {ItemName(used.Kind)}.");
                        break;
                    case MovedEvent _:
                    {
                        var moves = new List<MovedEvent>();
                        while (i < events.Count && events[i] is MovedEvent move)
                        {
                            moves.Add(move);
                            i++;
                        }
                        i--;
                        yield return AnimateMoves(run, moves);
                        break;
                    }
                    case CombatStartedEvent _:
                        hud.AddMessage("A fight begins! Faster fighters act more often.", WarningColor);
                        break;
                    case CombatEndedEvent _:
                        if (run.State == RunState.InProgress) hud.AddMessage("The fight is over.", DungeonHud.HintColor);
                        break;
                    case FacingChangedEvent facing:
                        if (actors.TryGetValue(facing.ActorId, out var turner)) turner.SetFacing(facing.Direction);
                        break;
                    case AttackEvent attack:
                    {
                        int at = i;
                        var hit = i + 1 < events.Count ? events[i + 1] as DamageEvent : null;
                        if (hit != null && hit.TargetId == attack.TargetId) i++;
                        else hit = null;
                        if (attack.AttackerId == dancer)
                        {
                            yield return AnimateDanceStrike(run, attack, hit);
                            if (at >= danceEndsAt) dancer = -1;
                        }
                        else yield return AnimateAttack(run, attack, hit);
                        break;
                    }
                    case DamageEvent damage:
                        ShowDamage(run, damage, Vector3.zero);
                        break;
                    case DiedEvent died:
                        yield return AnimateDeath(died);
                        break;
                    case ExpGainedEvent exp:
                        // Everyone standing gets the same EXP: say it once.
                        if (!expLogged) hud.AddMessage(run.Party.Count > 1 ? $"The party gained {exp.Amount} EXP." : $"Gained {exp.Amount} EXP.");
                        expLogged = true;
                        break;
                    case LevelUpEvent levelUp:
                        if (actors.TryGetValue(levelUp.ActorId, out var leveled))
                        {
                            hud.ShowFloatingText(leveled.TextAnchor, "LEVEL UP!", GoldColor, 1.1f);
                            Effects.Sparkle(effectRoot, leveled.transform.position, GoldColor);
                        }
                        hud.AddMessage($"{leveled?.DisplayName ?? run.Hero.Name} grew to Lv {levelUp.Level}!", GoldColor);
                        yield return new WaitForSeconds(0.35f);
                        break;
                    case HealedEvent heal:
                        if (actors.TryGetValue(heal.ActorId, out var healed))
                        {
                            hud.ShowFloatingText(healed.TextAnchor, "+" + heal.Amount, HealColor, 1.1f);
                            Effects.Heal(effectRoot, healed.transform.position); // The pack's heal light, on whoever is healed.
                            if (healed.IsHero) hud.SetMemberHp(heal.ActorId, heal.HpAfter, MaxHpOf(run, heal.ActorId));
                            hud.AddMessage($"{healed.DisplayName} recovered {heal.Amount} HP.", HealColor);
                        }
                        yield return new WaitForSeconds(0.25f);
                        break;
                    case ItemPickedUpEvent pickup:
                        if (items.TryGetValue(pickup.ItemId, out var itemRenderer))
                        {
                            items.Remove(pickup.ItemId);
                            StartCoroutine(PopAndDestroy(itemRenderer));
                        }
                        hud.AddMessage($"Picked up a {ItemName(pickup.Kind)}.");
                        break;
                    case ActorSpawnedEvent spawned:
                        var newcomer = run.FindActor(spawned.ActorId);
                        if (newcomer != null) AddActor(newcomer).FadeIn();
                        break;
                    case BossActionEvent bossAction when bossAction.Action == BossAction.Slam:
                    {
                        var hits = new List<DamageEvent>();
                        while (i + 1 < events.Count && events[i + 1] is DamageEvent hit)
                        {
                            hits.Add(hit);
                            i++;
                        }
                        yield return AnimateSlam(run, bossAction.ActorId, hits);
                        break;
                    }
                    case BossActionEvent bossAction:
                        yield return bossAction.Action == BossAction.Charge
                            ? AnimateCharge(run, bossAction.ActorId)
                            : AnimateSummon(bossAction.ActorId);
                        break;
                    case FloorStartedEvent _:
                        yield return AnimateFloorChange(run);
                        break;
                    case RunEndedEvent _:
                        yield return new WaitForSeconds(0.4f); // A beat before the controller shows the end panel.
                        break;
                }
            }
            activeSkill = null;
            SyncToState(run);
        }

        static string ItemName(ItemKind kind) => kind.ToString().ToLowerInvariant();

        // ---- Animations ----

        IEnumerator AnimateMoves(DungeonRun run, List<MovedEvent> moves)
        {
            var movers = new List<(ActorView view, Vector3 from, Vector3 to, float duration)>();
            float longest = 0f;
            foreach (var move in moves)
            {
                if (!actors.TryGetValue(move.ActorId, out var view)) continue;
                view.SetFacing(move.Direction);
                float duration = StepDuration(run.FindActor(move.ActorId)?.Speed ?? ActorDefinition.DefaultSpeed);
                longest = Mathf.Max(longest, duration);
                movers.Add((view, TileCenter(move.From), TileCenter(move.To), duration));
            }
            for (float t = 0f; t < longest; t += Time.deltaTime)
            {
                foreach (var (view, from, to, duration) in movers)
                {
                    view.Place(Vector3.Lerp(from, to, Mathf.Clamp01(t / duration)));
                    view.SetMoving();
                }
                yield return null;
            }
            foreach (var (view, _, to, _) in movers) view.Place(to);
            UpdateSeeThrough(run);
        }

        /// <summary>Walk time for one tile: a little quicker for fast actors, a little slower for slow ones.</summary>
        static float StepDuration(int speed) =>
            StepTime * Mathf.Clamp(Mathf.Sqrt(ActorDefinition.DefaultSpeed / (float)Mathf.Max(1, speed)), 0.8f, 1.25f);

        /// <summary>
        /// A shot: the draw (the bow's own animation, its release timed to the moment the arrow leaves), an arrow flying
        /// straight to the tile it lands on (at any angle, not only along the 8 directions), then the hit there. Power
        /// Shot draws longer and its arrow trails; a skill's arrow glows.
        /// </summary>
        IEnumerator AnimateShot(DungeonRun run, ActorView shooter, AttackEvent attack, DamageEvent hit)
        {
            var to = TileCenter(attack.To);
            var direction = (to - shooter.transform.position).normalized;
            bool skillShot = activeSkill != null && activeSkill.Effect == SkillEffect.Shot && attack.AttackerId == activeSkillUser;
            bool heavy = skillShot && activeSkill.Knockback > 0;
            float draw = heavy ? HeavyDrawTime : ActorView.LungeTime;
            shooter.Play("attack", draw, "cast");
            if (heavy)
            {
                Effects.Sparkle(effectRoot, shooter.transform.position, SpiritColor);
                yield return new WaitForSeconds(draw - ActorView.LungeTime);
            }
            yield return shooter.Lunge(-direction, 0.08f);
            var from = shooter.transform.position + direction * 0.4f + Vector3.up * 0.15f;
            yield return Effects.Arrow(effectRoot, from, to, skillShot ? SpiritColor : Color.white,
                ArrowTimePerTile * Mathf.Max(1f, (to - from).magnitude), trail: heavy);
            StartCoroutine(shooter.Recover(0.08f));
            if (hit == null)
            {
                Effects.Dust(effectRoot, to);
                yield break;
            }
            ShowDamage(run, hit, direction);
            if (skillShot) Effects.Burst(effectRoot, to, SpiritColor, 8, 2.5f);
            if (heavy) Effects.Ring(effectRoot, to, SpiritColor);
            if (actors.TryGetValue(hit.TargetId, out var target))
                hud.AddMessage(DescribeHit(shooter, target, hit), target.IsHero ? HeroHurtColor : DungeonHud.TextColor);
            yield return new WaitForSeconds(hit.Critical ? 0.12f : 0.05f);
        }

        // ---- Notes on what the party's AI is up to (PROGRESSION.md, "A note when a hero waits") ----

        /// <summary>Leader turns between two log lines about the same hero waiting: a hold that goes on isn't said again.</summary>
        const int WaitNoteEvery = 4;

        // {0} is the hero. A few of each, taken in turn, so the log doesn't repeat itself.
        static readonly string[] HoldLines =
        {
            "{0} holds the doorway: \"One at a time, please!\"",
            "{0} plants both feet in the doorway. Let them come!",
            "{0} waits at the doorway, sizing up the fight ahead.",
        };

        static readonly string[] RestLines =
        {
            "{0} calls a breather: heal up first, then onward.",
            "{0} waits while the party patches itself up.",
            "{0} is in no hurry: everyone catches their breath.",
        };

        // {0} takes the front, {1} is the hurt one who steps back.
        static readonly string[] RotateLines =
        {
            "{0} steps up to the front; {1} falls back to catch a breath.",
            "{1} tags out. {0} takes the front!",
        };

        static readonly string[] SafetyLines =
        {
            "{1} ducks behind {0}!",
            "{1} is badly hurt and slips behind {0}.",
        };

        readonly Dictionary<int, int> lastWaitNote = new Dictionary<int, int>();
        int noteCount;

        string NextLine(string[] lines, params object[] names) => string.Format(lines[noteCount++ % lines.Length], names);

        /// <summary>
        /// A hero's AI stands still on purpose: a word over its head every turn it does, and a line in the log when it
        /// starts, so nobody thinks it is stuck.
        /// </summary>
        void ShowWait(DungeonRun run, HeroWaitedEvent waited)
        {
            if (!actors.TryGetValue(waited.ActorId, out var hero)) return;
            bool holds = waited.Reason == WaitReason.HoldsTheDoor;
            hud.ShowFloatingText(hero.TextAnchor, holds ? "Holding the door" : "Resting", holds ? GuardColor : HealColor, 0.7f);
            if (lastWaitNote.TryGetValue(waited.ActorId, out int last) && run.Turn - last < WaitNoteEvery && run.Turn >= last) return;
            lastWaitNote[waited.ActorId] = run.Turn;
            hud.AddMessage(NextLine(holds ? HoldLines : RestLines, hero.DisplayName), holds ? GuardColor : HealColor);
        }

        /// <summary>Two heroes traded places for a reason worth saying: the front rotated, or a badly hurt one ran to safety.</summary>
        void ShowSwap(SwappedEvent swapped)
        {
            if (swapped.Reason != SwapReason.Rotate && swapped.Reason != SwapReason.Safety) return;
            int freshId = swapped.HurtId == swapped.ActorId ? swapped.OtherId : swapped.ActorId;
            if (!actors.TryGetValue(freshId, out var fresh) || !actors.TryGetValue(swapped.HurtId, out var hurt)) return;
            bool rotate = swapped.Reason == SwapReason.Rotate;
            hud.ShowFloatingText(fresh.TextAnchor, rotate ? "My turn!" : "Get behind me!", GuardColor, 0.7f);
            hud.AddMessage(NextLine(rotate ? RotateLines : SafetyLines, fresh.DisplayName, hurt.DisplayName), GuardColor);
        }

        /// <summary>
        /// A status landed: a word over the actor, a line in the log, its icon over the head, and a lasting tint (blue
        /// guard, red mark) or glow (an aura). A mark is stamped on with brackets, an aura and a guard spread a ring.
        /// </summary>
        void ShowStatus(DungeonRun run, StatusAppliedEvent status)
        {
            if (!actors.TryGetValue(status.ActorId, out var view)) return;
            actors.TryGetValue(status.SourceId, out var source);
            string sourceName = source != null ? source.DisplayName : "";
            switch (status.Kind)
            {
                case StatusKind.Guard:
                    hud.ShowFloatingText(view.TextAnchor, "Guard", GuardColor, 0.75f);
                    if (status.ActorId == status.SourceId)
                    {
                        Effects.Ring(effectRoot, view.transform.position, GuardColor);
                        hud.AddMessage($"{sourceName} raises a guard rune.", GuardColor);
                    }
                    break;
                case StatusKind.Taunt:
                    hud.ShowFloatingText(view.TextAnchor, "Taunted", WarningColor, 0.75f);
                    hud.AddMessage($"{Subject(view)} is taunted into attacking {sourceName}.", WarningColor);
                    break;
                case StatusKind.Mark:
                    hud.ShowFloatingText(view.TextAnchor, "Marked", MarkColor, 0.75f);
                    Destroy(Effects.Reticle(effectRoot, view.transform.position, MarkColor, pulse: true), 0.45f);
                    hud.AddMessage($"{Subject(view)} is marked: it takes more damage.", MarkColor);
                    break;
                case StatusKind.Rooted:
                    hud.ShowFloatingText(view.TextAnchor, "Snared", SnareColor, 0.75f);
                    break;
                case StatusKind.Riposte:
                    // The rig's guard pose, held, with a glint on the blade; the skill's name is over her head already.
                    stances.Add(status.ActorId);
                    view.SetStance(true);
                    Effects.Glint(effectRoot, view.transform.position + new Vector3(0.3f, 0.75f, 0f), Color.white);
                    hud.AddMessage($"{sourceName} takes a counter stance: the first foe to strike gets an answer.", GuardColor);
                    break;
                case StatusKind.Aura:
                    hud.ShowFloatingText(view.TextAnchor + Vector3.up * 0.1f, "Aura of Protection", UltimateTextColor, 0.75f);
                    Effects.Ring(effectRoot, view.transform.position, UltimateTextColor);
                    hud.AddMessage($"{sourceName}'s aura shields and heals him and the allies next to him.", UltimateTextColor);
                    break;
            }
            ShowStatuses(view, run.FindActor(status.ActorId));
        }

        /// <summary>
        /// Brings an actor's status icons, tint and aura glow in line with its statuses. A stun icon shows while its
        /// coming turn is pushed back (it can't be delayed again until it acts).
        /// </summary>
        static void ShowStatuses(ActorView view, Actor actor)
        {
            view.SetStatusTint(StatusTint(actor));
            view.SetStatuses(actor?.Statuses, delayed: actor != null && actor.IsDelayed);
            view.SetAura(actor?.FindStatus(StatusKind.Aura) != null);
        }

        /// <summary>Keeps the heroes' ultimate meters in step; when one fills up, it says so over the hero.</summary>
        void ShowCharge(DungeonRun run, ChargeChangedEvent charge)
        {
            hud.SetMemberCharge(charge.ActorId, charge.ChargeAfter);
            if (charge.Amount <= 0 || charge.ChargeAfter < CombatRules.MaxCharge || !actors.TryGetValue(charge.ActorId, out var hero)) return;
            var ultimate = PartyMember(run, charge.ActorId)?.Ultimate;
            hud.ShowFloatingText(hero.transform.position + new Vector3(-0.7f, 0.6f, 0f), "ULT ready!", UltimateTextColor, 0.75f);
            if (ultimate != null) hud.AddMessage($"{hero.DisplayName}'s {ultimate.Name} is ready!", UltimateTextColor);
        }

        // ---- Aiming (PROGRESSION.md, "Targeting and input") ----

        /// <summary>
        /// Highlights where the action reaches, marks every valid target (the chosen one pulses) and, for an area skill,
        /// the area around the chosen target. Stays until <see cref="ClearAim"/>. Called again for the same aim when the
        /// player picks another target: then only the marks are redrawn, not the (up to 120) reach tiles. The camera
        /// moves to keep the hero and every target on screen (ART.md, "View size").
        /// </summary>
        public void ShowAim(DungeonRun run, AimInfo aim, int chosen)
        {
            if (shownAim != aim)
            {
                ClearAim();
                shownAim = aim;
                foreach (var tile in aim.Reach) aimReach.Add(Effects.TileHighlight(effectRoot, TileCenter(tile), ReachColor));
            }
            var points = new List<Vector3> { TileCenter(run.Hero.Pos) };
            foreach (var option in aim.Options) points.Add(TileCenter(option.Tile));
            pixelCamera.Frame(points, chosen >= 0 && chosen < aim.Options.Count ? TileCenter(aim.Options[chosen].Tile) : (Vector3?)null);
            foreach (var mark in aimMarks)
                if (mark != null) Destroy(mark);
            aimMarks.Clear();
            for (int i = 0; i < aim.Options.Count; i++)
            {
                var option = aim.Options[i];
                aimMarks.Add(Effects.Reticle(effectRoot, TileCenter(option.Tile), i == chosen ? ChosenColor : TargetColor, pulse: i == chosen));
                if (i != chosen || aim.AreaRadius <= 0) continue;
                for (int dy = -aim.AreaRadius; dy <= aim.AreaRadius; dy++)
                    for (int dx = -aim.AreaRadius; dx <= aim.AreaRadius; dx++)
                    {
                        var tile = new GridPos(option.Tile.X + dx, option.Tile.Y + dy);
                        if (run.Map.IsWalkable(tile)) aimMarks.Add(Effects.TileHighlight(effectRoot, TileCenter(tile), AreaColor));
                    }
            }
        }

        public void ClearAim()
        {
            foreach (var mark in aimReach)
                if (mark != null) Destroy(mark);
            aimReach.Clear();
            foreach (var mark in aimMarks)
                if (mark != null) Destroy(mark);
            aimMarks.Clear();
            shownAim = null;
            if (pixelCamera != null) pixelCamera.Frame(null);
        }

        /// <summary>The body tint for an actor's statuses: blue while guarded, red while marked.</summary>
        static Color StatusTint(Actor actor)
        {
            if (actor == null) return Color.white;
            if (actor.FindStatus(StatusKind.Guard) != null) return GuardTint;
            if (actor.FindStatus(StatusKind.Mark) != null) return MarkTint;
            return Color.white;
        }

        static Actor PartyMember(DungeonRun run, int actorId)
        {
            foreach (var member in run.Party)
                if (member.Id == actorId) return member;
            return null;
        }

        static int MaxHpOf(DungeonRun run, int actorId) => PartyMember(run, actorId)?.MaxHp ?? run.FindActor(actorId)?.MaxHp ?? 1;

        /// <summary>
        /// The skill's name pops up over its user (and goes in the log). Skills that aren't a swing or a shot show in
        /// the user's pose: a heal, a guard or an aura raises the shield (or casts, with a staff). An ultimate gets a
        /// bigger, golden moment (its cutscene comes later; the event already says whose ultimate it is).
        /// </summary>
        IEnumerator AnimateSkillUse(SkillUsedEvent used)
        {
            if (!actors.TryGetValue(used.ActorId, out var user)) yield break;
            var skill = used.Skill;
            bool pose = skill.Effect == SkillEffect.Heal || skill.Effect == SkillEffect.Guard || skill.Effect == SkillEffect.Aura;
            if (pose) user.Play("guard", 0f, "cast");
            if (skill.IsUltimate)
            {
                hud.AddMessage($"{user.DisplayName} unleashes {skill.Name}!", UltimateTextColor);
                hud.ShowFloatingText(user.TextAnchor + Vector3.up * 0.1f, skill.Name + "!", UltimateTextColor, 1.1f);
                Effects.Sparkle(effectRoot, user.transform.position, UltimateTextColor);
                Effects.Ring(effectRoot, user.transform.position, UltimateTextColor);
                pixelCamera.Shake(0.08f, 0.25f);
                yield return new WaitForSeconds(0.3f);
                yield break;
            }
            hud.AddMessage($"{user.DisplayName} used {skill.Name}!", SkillTextColor);
            hud.ShowFloatingText(user.TextAnchor, skill.Name, SkillTextColor, 0.7f);
            if (skill.Effect == SkillEffect.Dash || skill.RollTiles > 0 || skill.DashTiles > 0) yield break; // The dash itself is the show.
            if (skill.Effect != SkillEffect.Heal) Effects.Sparkle(effectRoot, user.transform.position, SpiritColor);
            yield return new WaitForSeconds(0.12f);
        }

        /// <summary>
        /// Volley: the bow drawn skyward, then arrows rain on every tile of the area and kick up dust where they land;
        /// the hits follow as damage events.
        /// </summary>
        IEnumerator AnimateVolley(DungeonRun run, AreaAttackEvent area)
        {
            if (actors.TryGetValue(area.ActorId, out var shooter))
            {
                shooter.Play("attack", 0.12f, "cast");
                yield return shooter.Lunge(Vector3.down * 0.5f, 0.12f);
            }
            for (int dy = -area.Radius; dy <= area.Radius; dy++)
                for (int dx = -area.Radius; dx <= area.Radius; dx++)
                {
                    var tile = new GridPos(area.Center.X + dx, area.Center.Y + dy);
                    if (run.Map.IsWalkable(tile)) StartCoroutine(Effects.RainArrows(effectRoot, TileCenter(tile), UltimateTextColor, 3, 0.36f));
                }
            yield return new WaitForSeconds(0.36f);
            for (int dy = -area.Radius; dy <= area.Radius; dy++)
                for (int dx = -area.Radius; dx <= area.Radius; dx++)
                {
                    var tile = new GridPos(area.Center.X + dx, area.Center.Y + dy);
                    if (run.Map.IsWalkable(tile)) Effects.Dust(effectRoot, TileCenter(tile), flipX: (dx + dy) % 2 != 0);
                }
            pixelCamera.Shake(0.1f, 0.2f);
            if (shooter != null) StartCoroutine(shooter.Recover(0.1f));
        }

        /// <summary>
        /// Blade Dance begins: its 3x3 lights up, and every strike that follows shows her at its target for a moment
        /// (<see cref="AnimateDanceStrike"/>), up to the last one, which is found here by looking ahead.
        /// </summary>
        void BeginDance(DungeonRun run, IReadOnlyList<GameEvent> events, int index, AreaAttackEvent area)
        {
            dancer = area.ActorId;
            danceStrikes = 0;
            danceEndsAt = index;
            for (int j = index + 1; j < events.Count; j++)
            {
                if (events[j] is AttackEvent strike)
                {
                    if (strike.AttackerId != dancer) break;
                    danceEndsAt = j;
                }
                else if (events[j] is SkillUsedEvent || events[j] is MovedEvent || events[j] is BossActionEvent || events[j] is CounterEvent) break;
            }
            if (danceEndsAt == index) dancer = -1; // Nobody left to strike.
            for (int dy = -area.Radius; dy <= area.Radius; dy++)
                for (int dx = -area.Radius; dx <= area.Radius; dx++)
                {
                    var tile = new GridPos(area.Center.X + dx, area.Center.Y + dy);
                    if (run.Map.IsWalkable(tile)) Destroy(Effects.TileHighlight(effectRoot, TileCenter(tile), AreaColor), 0.5f);
                }
        }

        /// <summary>
        /// One strike of a Blade Dance: she flickers to her target (a bright image of her beside it, facing it) and
        /// cuts, and the hit lands. She herself keeps her tile, as the rules have it, so the camera stays calm.
        /// </summary>
        IEnumerator AnimateDanceStrike(DungeonRun run, AttackEvent attack, DamageEvent hit)
        {
            if (!actors.TryGetValue(attack.AttackerId, out var dancing)) yield break;
            var target = TileCenter(attack.To);
            var way = target - dancing.transform.position;
            way = way.sqrMagnitude < 0.01f ? Vector3.right : way.normalized;
            dancing.SetFacing(attack.Direction);
            dancing.Play("attack", 0.05f, "attack2");
            if (attack.Distance > 1) dancing.LeaveGhostAt(effectRoot, target - way * 0.85f, DanceGhostColor, 0.2f);
            else dancing.LeaveAfterimage(effectRoot, DanceGhostColor);
            Effects.Slash(effectRoot, target, UltimateTextColor, flipX: danceStrikes++ % 2 == 1);
            yield return new WaitForSeconds(0.06f);
            if (hit != null)
            {
                ShowDamage(run, hit, way);
                if (actors.TryGetValue(hit.TargetId, out var victim)) hud.AddMessage(DescribeHit(dancing, victim, hit), DungeonHud.TextColor);
            }
            yield return new WaitForSeconds(hit != null && hit.Critical ? 0.12f : 0.07f);
        }

        /// <summary>A snare goes off: it snaps shut around whoever stepped on it, then is gone.</summary>
        IEnumerator AnimateTrap(TrapTriggeredEvent triggered)
        {
            if (traps.TryGetValue(triggered.TrapId, out var trap))
            {
                traps.Remove(triggered.TrapId);
                StartCoroutine(PopAndDestroy(trap));
            }
            if (actors.TryGetValue(triggered.ActorId, out var victim))
            {
                hud.AddMessage($"{Subject(victim)} steps on a snare!", SnareColor);
                Effects.Burst(effectRoot, victim.transform.position + Vector3.down * 0.3f, SnareColor, 8, 2f);
            }
            yield return new WaitForSeconds(0.12f);
        }

        /// <summary>A quick slide or tumble over several tiles, leaving fading afterimages and a puff of dust at each end.</summary>
        IEnumerator AnimateDash(DashedEvent dash)
        {
            if (!actors.TryGetValue(dash.ActorId, out var view)) yield break;
            view.SetFacing(dash.Direction);
            var from = TileCenter(dash.From);
            var to = TileCenter(dash.To);
            Effects.Dust(effectRoot, from);
            float duration = DashTimePerTile * Mathf.Max(1, GridPos.ChebyshevDistance(dash.From, dash.To));
            float nextGhost = 0f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                view.Place(Vector3.Lerp(from, to, 1f - (1f - k) * (1f - k))); // Fast start, soft stop.
                view.SetMoving();
                if (t >= nextGhost)
                {
                    view.LeaveAfterimage(effectRoot, DashGhostColor);
                    nextGhost += 0.025f;
                }
                yield return null;
            }
            view.Place(to);
            Effects.Dust(effectRoot, to, flipX: true);
        }

        /// <summary>
        /// A melee attack: the attacker's own attack animation, timed so its swing lands with the lunge, then the hit.
        /// A hero's skills show what they do: Divine Strike bursts into fire on the target, Shoulder Bash leads with the
        /// shield and shocks the ground, Piercing Punch drives a ring through to the foe behind, Flurry of Blows leaves
        /// afterimages, and gauntlets land with a burst instead of a blade's arc. The Piercer Blade stabs: a streak
        /// from its bearer to the target, three side by side in a blur for Triple Thrust, and after a Lunge's dash.
        /// A Riposte's counter is a flash (shown with its event), a stab and a cut, and then the stance again.
        /// </summary>
        IEnumerator AnimateAttack(DungeonRun run, AttackEvent attack, DamageEvent hit)
        {
            if (!actors.TryGetValue(attack.AttackerId, out var attacker)) yield break;
            attacker.SetFacing(attack.Direction);
            var offset = attack.Direction.ToOffset();
            var direction = new Vector3(offset.X, offset.Y, 0f).normalized;
            if (attack.Ranged)
            {
                yield return AnimateShot(run, attacker, attack, hit);
                yield break;
            }
            // A skill strike lunges further, with the heavier swing where the art has one.
            bool counter = attack.AttackerId == counterBy;
            if (counter) counterBy = -1;
            // The user's own strikes, as many as the skill has: the blows of others, and its later ones, are plain attacks.
            var skill = !counter && attacker.IsHero && attack.AttackerId == activeSkillUser && activeSkill != null &&
                        activeSkill.Effect == SkillEffect.Strike && strikesShown < activeSkill.Hits + (activeSkill.Pierce ? 1 : 0)
                ? activeSkill
                : null;
            if (skill != null) strikesShown++;
            bool flurry = skill != null && skill.Hits > 1;
            string swing = counter ? "attack2" : skill == null || flurry ? "attack" : skill.Shove ? "guard" : "attack2";
            attacker.Play(swing, ActorView.LungeTime, "attack", "cast");
            if (flurry) attacker.LeaveAfterimage(effectRoot, FlurryGhostColor);

            yield return attacker.Lunge(direction, skill != null || counter ? LungeDistance * 1.3f : LungeDistance);
            var targetTile = attacker.transform.position + new Vector3(offset.X, offset.Y, 0f);
            var weapon = run.FindActor(attack.AttackerId)?.Weapon?.Type;
            bool gauntlets = weapon == WeaponType.Gauntlets;
            if (gauntlets) Effects.Punch(effectRoot, targetTile, skill != null ? SpiritColor : PunchTint);
            if (weapon == WeaponType.PiercerBlade)
            {
                // A fencer stabs. A flurry's thrusts land side by side.
                var side = new Vector3(-direction.y, direction.x, 0f) * (flurry ? (strikesShown - 2) * 0.14f : 0f);
                var from = attacker.transform.position + direction * 0.2f + Vector3.up * 0.1f + side;
                Effects.Thrust(effectRoot, from, from + direction, counter ? UltimateTextColor : skill != null ? SpiritColor : BladeColor);
            }
            if (counter) Effects.Slash(effectRoot, targetTile, UltimateTextColor);
            if (skill != null)
            {
                if (skill.Element == Element.Fire)
                {
                    Effects.Explosion(effectRoot, targetTile);
                    Effects.Burst(effectRoot, targetTile, FireColor, 10, 3f);
                }
                else if (skill.Shove) Effects.Ring(effectRoot, targetTile, Color.white);
                else if (skill.Pierce)
                {
                    Effects.Ring(effectRoot, targetTile, SpiritColor);
                    Effects.Ring(effectRoot, targetTile + new Vector3(offset.X, offset.Y, 0f), SpiritColor);
                }
                else if (!flurry) Effects.Burst(effectRoot, targetTile, SpiritColor, 10, 3f);
            }

            if (hit != null)
            {
                ShowDamage(run, hit, direction);
                if (actors.TryGetValue(hit.TargetId, out var target))
                    hud.AddMessage(DescribeHit(attacker, target, hit), target.IsHero ? HeroHurtColor : DungeonHud.TextColor);
                yield return new WaitForSeconds(hit.Critical ? 0.14f : flurry ? 0.04f : 0.06f); // Hit-stop sells the impact.
            }
            yield return attacker.Recover(flurry ? 0.05f : 0.09f); // A flurry's blows come in a blur.
            if (stances.Contains(attack.AttackerId)) attacker.SetStance(true); // The counter is struck; the stance holds until her turn.
        }

        void ShowDamage(DungeonRun run, DamageEvent hit, Vector3 knockDirection)
        {
            if (!actors.TryGetValue(hit.TargetId, out var target)) return;
            target.Hurt(knockDirection, hit.HpAfter);
            var color = hit.Critical ? CritColor : target.IsHero ? HeroHurtColor : Color.white;
            // Numbers pop above the target, unless the attacker stands above it: then beside it, so they don't cover the attacker.
            var position = knockDirection.y < -0.1f ? target.transform.position + new Vector3(0.7f, 0.3f, 0f) : target.TextAnchor;
            hud.ShowFloatingText(position, hit.Amount.ToString(), color, hit.Critical ? 1.5f : 1f);
            if (target.IsHero) hud.SetMemberHp(hit.TargetId, hit.HpAfter, MaxHpOf(run, hit.TargetId));
            if (target.IsBoss) hud.SetBossHp(hit.HpAfter);
            Effects.Burst(effectRoot, target.transform.position + Vector3.up * 0.2f, hit.Critical ? CritColor : Color.white, hit.Critical ? 12 : 5, 2.5f);
            pixelCamera.Shake(hit.Critical ? 0.14f : 0.05f, hit.Critical ? 0.22f : 0.1f);
        }

        static string DescribeHit(ActorView attacker, ActorView target, DamageEvent hit)
        {
            string text = $"{Subject(attacker)} hit {Object(target)} for {hit.Amount}.";
            return hit.Critical ? "Critical! " + text : text;
        }

        static string Subject(ActorView view) => view.IsHero ? view.DisplayName : "The " + view.DisplayName;
        static string Object(ActorView view) => view.IsHero ? view.DisplayName : "the " + view.DisplayName;

        IEnumerator AnimateDeath(DiedEvent died)
        {
            if (!actors.TryGetValue(died.ActorId, out var view)) yield break;
            actors.Remove(died.ActorId);
            hud.AddMessage(view.IsHero ? $"{view.DisplayName} fainted..." : $"{Subject(view)} was defeated!",
                view.IsHero ? HeroHurtColor : view.IsBoss ? GoldColor : DungeonHud.TextColor);
            if (view.IsBoss)
            {
                ClearWarnings();
                hud.HideBoss();
                pixelCamera.Shake(0.2f, 0.6f);
                Effects.Burst(effectRoot, view.transform.position + Vector3.up * 0.6f, view.BurstColor, 40, 5f);
                Effects.Sparkle(effectRoot, view.transform.position, GoldColor);
            }
            else
            {
                Effects.Burst(effectRoot, view.transform.position + Vector3.up * 0.2f, view.BurstColor, 16, 3.5f);
            }
            yield return view.Die();
            Destroy(view.gameObject);
        }

        /// <summary>The boss winds up: it raises its club and holds, trembling, and the tiles it will hit pulse red.</summary>
        IEnumerator AnimateCharge(DungeonRun run, int bossId)
        {
            if (!actors.TryGetValue(bossId, out var boss)) yield break;
            boss.SetCharging(true);
            ClearWarnings(keepCharging: true);
            foreach (var tile in SlamTiles(run, bossId)) warnings.Add(Effects.WarningTile(effectRoot, TileCenter(tile)));
            hud.AddMessage($"{Subject(boss)} is gathering its strength! Get away!", WarningColor);
            yield return new WaitForSeconds(0.35f);
        }

        static IEnumerable<GridPos> SlamTiles(DungeonRun run, int bossId)
        {
            var center = run.FindActor(bossId)?.Pos;
            if (!center.HasValue) yield break;
            for (int dy = -EnemyBrain.SlamRadius; dy <= EnemyBrain.SlamRadius; dy++)
                for (int dx = -EnemyBrain.SlamRadius; dx <= EnemyBrain.SlamRadius; dx++)
                {
                    var tile = new GridPos(center.Value.X + dx, center.Value.Y + dy);
                    if ((dx != 0 || dy != 0) && run.Map.IsWalkable(tile)) yield return tile;
                }
        }

        /// <summary>
        /// The boss's slam: its attack animation, a shock ring and dust on every tile it hits when the club lands, then
        /// damage to everyone caught, while it goes on into its recovery.
        /// </summary>
        IEnumerator AnimateSlam(DungeonRun run, int bossId, List<DamageEvent> hits)
        {
            if (!actors.TryGetValue(bossId, out var boss)) yield break;
            boss.Play("attack", SlamImpactTime);
            boss.Then("recovery");
            yield return new WaitForSeconds(SlamImpactTime);
            ClearWarnings();
            pixelCamera.Shake(0.22f, 0.3f);
            Effects.Ring(effectRoot, boss.transform.position, DustColor);
            Effects.Shockwave(effectRoot, boss.transform.position, DustColor, 24, 3.5f);
            foreach (var tile in SlamTiles(run, bossId)) Effects.Dust(effectRoot, TileCenter(tile), flipX: (tile.X + tile.Y) % 2 != 0);
            if (hits.Count == 0) hud.AddMessage($"{Subject(boss)}'s slam hit nothing!", DungeonHud.TextColor);
            foreach (var hit in hits)
            {
                if (!actors.TryGetValue(hit.TargetId, out var target)) continue;
                var away = (target.transform.position - boss.transform.position).normalized;
                ShowDamage(run, hit, away);
                hud.AddMessage($"{Subject(boss)}'s slam hit {Object(target)} for {hit.Amount}!", target.IsHero ? HeroHurtColor : DungeonHud.TextColor);
            }
            yield return new WaitForSeconds(0.25f);
        }

        /// <summary>The boss roars for help: it raises its club, the ground shakes.</summary>
        IEnumerator AnimateSummon(int bossId)
        {
            if (!actors.TryGetValue(bossId, out var boss)) yield break;
            boss.Play("windup");
            hud.ShowFloatingText(boss.TextAnchor, "ROAR!", WarningColor, 1.2f);
            hud.AddMessage($"{Subject(boss)} called for help!", WarningColor);
            Effects.Ring(effectRoot, boss.transform.position, WarningColor);
            pixelCamera.Shake(0.1f, 0.3f);
            yield return new WaitForSeconds(0.3f);
        }

        void ClearWarnings(bool keepCharging = false)
        {
            foreach (var warning in warnings)
                if (warning != null) Destroy(warning);
            warnings.Clear();
            if (keepCharging) return;
            foreach (var view in actors.Values) view.SetCharging(false);
        }

        IEnumerator AnimateFloorChange(DungeonRun run)
        {
            yield return hud.Fade(1f, 0.35f);
            Rebuild(run);
            hud.Refresh(run);
            yield return new WaitForSeconds(0.15f);
            hud.ShowBanner(run.Config.Name, FloorTitle(run));
            if (run.IsBossFloor) hud.AddMessage("A powerful presence fills the air...", WarningColor);
            yield return hud.Fade(0f, 0.35f);
        }

        IEnumerator PopAndDestroy(SpriteRenderer renderer)
        {
            var start = renderer.transform.position;
            for (float t = 0f; t < 0.2f; t += Time.deltaTime)
            {
                float k = t / 0.2f;
                renderer.transform.position = start + Vector3.up * (0.3f * k);
                renderer.color = new Color(1f, 1f, 1f, 1f - k);
                yield return null;
            }
            Destroy(renderer.gameObject);
        }

        /// <summary>Snaps every view to the run's state after an action, so animation can never leave things out of place.</summary>
        void SyncToState(DungeonRun run)
        {
            foreach (var actor in run.Actors)
            {
                if (!actors.TryGetValue(actor.Id, out var view)) continue;
                view.Place(TileCenter(actor.Pos));
                view.SetHp(actor.Hp);
                ShowStatuses(view, actor);
                view.SetCharging(actor.Charging);
            }
            UpdateSeeThrough(run);
        }

        /// <summary>
        /// A sprite much taller than a tile (the Troll) covers the tiles behind it. While anyone stands there, it turns
        /// see-through, so no actor is ever hidden.
        /// </summary>
        void UpdateSeeThrough(DungeonRun run)
        {
            foreach (var actor in run.Actors)
            {
                if (!actors.TryGetValue(actor.Id, out var view) || view.Reach.y < 1.6f) continue;
                bool hides = false;
                foreach (var other in run.Actors)
                {
                    if (other == actor || !other.IsAlive) continue;
                    int dx = Mathf.Abs(other.Pos.X - actor.Pos.X), dy = other.Pos.Y - actor.Pos.Y;
                    if (dy >= 1 && dy <= Mathf.CeilToInt(view.Reach.y) - 1 && dx <= Mathf.FloorToInt(view.Reach.x + 0.4f)) hides = true;
                }
                view.SetSeeThrough(hides);
            }
        }

        // ---- Building ----

        ActorView AddActor(Actor actor)
        {
            bool isHero = actor.Team == Team.Hero;
            string id = actor.Definition.Id;
            var sprites = isHero ? HeroLooks.Sprites(id) : SpriteLibrary.Monster(id);
            var burstColor = isHero ? new Color(0.5f, 0.75f, 1f) : actor.Definition.IsBoss ? BossBurstColor : new Color(0.62f, 0.45f, 0.72f);
            var view = ActorView.Create(actorRoot, actor, sprites, burstColor);
            view.Place(TileCenter(actor.Pos));
            view.SetFacing(actor.Facing);
            ShowStatuses(view, actor);
            view.SetCharging(actor.Charging);
            if (isHero && ArtManifest.Current.Aura(HeroLooks.For(id).Aura) is AuraInfo ring && ColorUtility.TryParseHtmlString(ring.color, out var glow))
                view.SetRing(glow);
            actors[actor.Id] = view;
            return view;
        }

        /// <summary>
        /// A hero's ring set did something: a pulse of the set's colour over the hero. The ring bonuses come with the
        /// gear rules (milestone 1h), which call this when one fires; until then it shows when a hero wearing a ring
        /// (a look set with -fk-look) uses a skill.
        /// </summary>
        public void RingFlash(int actorId)
        {
            if (actors.TryGetValue(actorId, out var view)) view.RingFlash();
        }

        void AddTrap(int trapId, GridPos pos)
        {
            if (traps.ContainsKey(trapId)) return;
            var renderer = NewSprite("Snare", itemRoot, SpriteLibrary.Still("Effects/snare"), TrapOrder);
            renderer.transform.position = TileCenter(pos);
            traps[trapId] = renderer;
        }

        void AddItem(FloorItem item)
        {
            var renderer = NewSprite(item.Kind.ToString(), itemRoot, SpriteLibrary.Still("Effects/berry"), ItemOrder);
            renderer.transform.position = TileCenter(item.Pos);
            items[item.Id] = renderer;
        }

        /// <summary>
        /// Flat ground everywhere, and on every wall tile the raised ground: its grassy top where the tile to the south
        /// is a wall too, its grass lip over a cliff face where the tile to the south is floor. Which edges a tile shows
        /// depends on which of its neighbours (north, east, west) are walls.
        /// </summary>
        void DrawTerrain(DungeonMap map)
        {
            ground.ClearAllTiles();
            walls.ClearAllTiles();
            var cells = new List<Vector3Int>();
            var groundTiles = new List<TileBase>();
            var wallCells = new List<Vector3Int>();
            var wallTiles = new List<TileBase>();
            bool Wall(int x, int y) => !map.IsWalkable(new GridPos(x, y));

            for (int y = -WallPadding; y < map.Height + WallPadding; y++)
            {
                for (int x = -WallPadding; x < map.Width + WallPadding; x++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    cells.Add(cell);
                    groundTiles.Add(groundTile);
                    if (!Wall(x, y)) continue;
                    int neighbours = (Wall(x, y + 1) ? 4 : 0) | (Wall(x + 1, y) ? 2 : 0) | (Wall(x - 1, y) ? 1 : 0);
                    wallCells.Add(cell);
                    wallTiles.Add(Wall(x, y - 1) ? wallTops[neighbours] : wallFaces[neighbours]);
                }
            }
            ground.SetTiles(cells.ToArray(), groundTiles.ToArray());
            walls.SetTiles(wallCells.ToArray(), wallTiles.ToArray());
            Decorate(map);
        }

        /// <summary>
        /// Bushes, rocks, trees, bones and skull spikes on the raised ground, the same for a given floor every time.
        /// Nothing stands on or reaches over a walkable tile: a tree needs raised ground under its whole crown. A lone
        /// wall tile inside a room (the boss room's pillars) gets a skull spike, so it reads as blocking.
        /// </summary>
        void Decorate(DungeonMap map)
        {
            for (int i = decorationRoot.childCount - 1; i >= 0; i--) Destroy(decorationRoot.GetChild(i).gameObject);
            bool Wall(int x, int y) => !map.IsWalkable(new GridPos(x, y));

            for (int y = -WallPadding; y < map.Height + WallPadding; y++)
            {
                for (int x = -WallPadding; x < map.Width + WallPadding; x++)
                {
                    if (!Wall(x, y)) continue;
                    uint hash = Hash(x, y);
                    int variant = (int)(hash >> 8);
                    if (!Wall(x, y + 1) && !Wall(x, y - 1) && !Wall(x + 1, y) && !Wall(x - 1, y))
                    {
                        PlaceDecoration("Deco/skull_spike_" + (1 + variant % 2), x, y, 0.3f);
                        continue;
                    }
                    if (!Wall(x, y - 1)) continue; // A cliff face: nothing stands in front of it.
                    uint roll = hash % 100u;
                    if (roll < 3 && TreeFits(x, y, Wall)) PlaceDecoration("Deco/tree_" + (1 + variant % 4), x, y, 0.15f);
                    else if (roll < 9) PlaceDecoration("Deco/bush_" + (1 + variant % 4), x, y, 0.2f);
                    else if (roll < 12) PlaceDecoration("Deco/rock_" + (1 + variant % 4), x, y, 0.25f);
                    else if (roll < 14) PlaceDecoration(variant % 3 == 0 ? "Deco/stump_" + (1 + variant % 2) : "Deco/bones_" + (1 + variant % 3), x, y, 0.25f);
                    else if (roll < 16 && Wall(x, y + 1) && NearFloor(x, y, map)) PlaceDecoration("Deco/skull_spike_" + (1 + variant % 2), x, y, 0.2f);
                }
            }
        }

        static bool TreeFits(int x, int y, System.Func<int, int, bool> wall)
        {
            for (int dy = 0; dy <= 3; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (!wall(x + dx, y + dy)) return false;
            return true;
        }

        static bool NearFloor(int x, int y, DungeonMap map)
        {
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    if (map.IsWalkable(new GridPos(x + dx, y + dy))) return true;
            return false;
        }

        /// <summary>A decoration standing on a tile: its foot at <paramref name="foot"/> of the tile's height above its lower edge.</summary>
        void PlaceDecoration(string strip, int x, int y, float foot)
        {
            var renderer = NewSprite(strip, decorationRoot, SpriteLibrary.Still(strip), 5000 - Mathf.RoundToInt((y + foot) * 10f));
            renderer.transform.position = new Vector3(x + 0.5f, y + foot, 0f);
        }

        /// <summary>Stable pseudo-random number per tile.</summary>
        static uint Hash(int x, int y)
        {
            uint h = unchecked((uint)(x * 73856093) ^ (uint)(y * 19349663));
            h = unchecked((h ^ (h >> 13)) * 0x5bd1e995u);
            return h ^ (h >> 15);
        }

        Transform CreateChild(string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(transform, false);
            return child;
        }

        static Tilemap CreateTilemap(Transform grid, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(grid, false);
            go.GetComponent<TilemapRenderer>().sortingOrder = sortingOrder;
            return go.GetComponent<Tilemap>();
        }

        static Tile MakeTile(string path, Color fallback)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = SpriteLibrary.Get(path, fallback);
            tile.colliderType = Tile.ColliderType.None;
            return tile;
        }

        static SpriteRenderer NewSprite(string name, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
