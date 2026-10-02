using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    public enum RunState { InProgress, Won, Lost }

    /// <summary>Whether a skill can be used right now, and if not, why (for button states and messages).</summary>
    public enum SkillCheck { Ready, NoSkill, OnCooldown, NotEnoughMana, NoTarget, NotNeeded, Blocked }

    /// <summary>
    /// One dungeon expedition: the turn-based rules for exploring floors and fighting.
    /// While exploring, every other actor takes one turn per hero action. Once an enemy notices the hero, combat
    /// starts and turns follow the action-value <see cref="Timeline"/> (faster actors act more often) until no enemy
    /// is alerted. Time is kept in exact AV either way: regeneration and reinforcements run on it, so a faster hero
    /// gets more done before the floor reacts. Each hero action records what happened in <see cref="Events"/> for the
    /// presentation layer to animate. The hero starts from a <see cref="HeroProgress"/> and EXP earned is recorded
    /// back into it as it happens. No Unity dependency, so it can be unit tested and simulated headlessly.
    /// </summary>
    public sealed class DungeonRun
    {
        readonly List<Actor> actors = new List<Actor>();
        readonly List<FloorItem> items = new List<FloorItem>();
        readonly List<GameEvent> events = new List<GameEvent>();
        readonly Timeline timeline = new Timeline();
        int nextId = 1;
        AvTime runTime;
        AvTime floorTime;

        public DungeonRun(int seed, DungeonRunConfig config = null, HeroProgress hero = null)
        {
            Seed = seed;
            Config = config ?? new DungeonRunConfig();
            Random = new Rng(Rng.DeriveSeed(seed, int.MaxValue));
            Progress = hero ?? new HeroProgress(Config.Hero);
            Hero = new Actor(nextId++, Progress.Definition, Team.Hero, default, Progress.Level) { Exp = Progress.Exp };
            EnterFloor(1);
        }

        public int Seed { get; }
        public DungeonRunConfig Config { get; }
        public HeroProgress Progress { get; }
        public int Floor { get; private set; }
        public DungeonMap Map { get; private set; }
        public Actor Hero { get; }
        public IReadOnlyList<Actor> Actors => actors;
        public IReadOnlyList<FloorItem> Items => items;
        public int Berries { get; set; }
        public RunState State { get; private set; } = RunState.InProgress;

        /// <summary>Hero turns taken this run.</summary>
        public int Turn { get; private set; }

        /// <summary>AV elapsed this run, exploring and fighting alike.</summary>
        public AvTime RunTime => runTime;

        /// <summary>True while an enemy is alerted and turns follow the timeline.</summary>
        public bool InCombat { get; private set; }

        /// <summary>AV since the current fight began (0 when not fighting).</summary>
        public AvTime CombatTime => timeline.Now;

        /// <summary>The fight's current cycle: 0 for the first 150 AV, then one per 100 AV.</summary>
        public int CombatCycle => Timeline.CycleOf(timeline.Now);

        /// <summary>Everything that happened during the most recent action, in order.</summary>
        public IReadOnlyList<GameEvent> Events => events;

        public bool HeroOnStairs => Hero.Pos == Map.Stairs;
        public bool IsLastFloor => Floor >= Config.FloorCount;

        /// <summary>The last floor holds the boss's arena instead of stairs, when the dungeon has a boss.</summary>
        public bool IsBossFloor => Config.Boss != null && IsLastFloor;

        /// <summary>The boss on this floor, or null.</summary>
        public Actor Boss
        {
            get
            {
                foreach (var actor in actors)
                    if (actor.Definition.IsBoss) return actor;
                return null;
            }
        }

        /// <summary>Randomness for combat and AI. Floors use their own seeds, so layouts don't depend on how fights went.</summary>
        internal Rng Random { get; }

        public Actor ActorAt(GridPos pos)
        {
            foreach (var actor in actors)
                if (actor.Pos == pos) return actor;
            return null;
        }

        public Actor FindActor(int id)
        {
            foreach (var actor in actors)
                if (actor.Id == id) return actor;
            return null;
        }

        public FloorItem ItemAt(GridPos pos)
        {
            foreach (var item in items)
                if (item.Pos == pos) return item;
            return null;
        }

        /// <summary>
        /// The next <paramref name="count"/> turns in combat, starting with the hero's current one, assuming each action
        /// costs a normal turn. Empty while exploring.
        /// </summary>
        public IReadOnlyList<TimelineTurn> Forecast(int count) =>
            InCombat ? timeline.Forecast(Hero, count) : (IReadOnlyList<TimelineTurn>)Array.Empty<TimelineTurn>();

        /// <summary>Changes an actor's speed (buffs later). Mid-fight it keeps the actor's progress toward its next turn.</summary>
        public void SetSpeed(Actor actor, int speed) => timeline.ChangeSpeed(actor, speed);

        // ---- Hero actions. Each returns true when it used up the hero's turn. ----

        public bool Execute(HeroCommand command)
        {
            switch (command.Kind)
            {
                case HeroCommandKind.Move: return Move(command.Direction);
                case HeroCommandKind.Attack: return Attack();
                case HeroCommandKind.Wait: return Wait();
                case HeroCommandKind.UseBerry: return UseBerry();
                case HeroCommandKind.Descend: return Descend();
                case HeroCommandKind.Skill: return command.Aimed ? UseSkill(command.Slot, command.Direction) : UseSkill(command.Slot);
                default: throw new ArgumentOutOfRangeException(nameof(command), command.Kind, null);
            }
        }

        /// <summary>Walk one tile, or attack whatever enemy stands there. Bumping a wall only turns the hero and costs no turn.</summary>
        public bool Move(Direction8 dir)
        {
            if (!BeginAction()) return false;

            Hero.Facing = dir;
            var occupant = ActorAt(Hero.Pos + dir.ToOffset());
            if (occupant != null && occupant.Team != Hero.Team && Map.IsCornerClear(Hero.Pos, dir))
            {
                ResolveAttack(Hero, dir);
                FinishHeroTurn(Config.Costs.PercentFor(ActionKind.Attack));
                return true;
            }
            if (occupant != null || !Map.CanStep(Hero.Pos, dir))
            {
                events.Add(new FacingChangedEvent(Hero.Id, dir));
                return false;
            }

            Step(Hero, dir);
            PickUpItemUnderHero();
            FinishHeroTurn(Config.Costs.PercentFor(ActionKind.Move));
            return true;
        }

        /// <summary>Attack the tile the hero is facing, even if it's empty (a missed swing still uses the turn).</summary>
        public bool Attack()
        {
            if (!BeginAction()) return false;
            ResolveAttack(Hero, Hero.Facing);
            FinishHeroTurn(Config.Costs.PercentFor(ActionKind.Attack));
            return true;
        }

        public bool Wait()
        {
            if (!BeginAction()) return false;
            FinishHeroTurn(Config.Costs.PercentFor(ActionKind.Wait));
            return true;
        }

        /// <summary>
        /// Eat a berry: restores mana (and HP, if the dungeon's berries heal). Refused (no turn used) when out of
        /// berries or when it would do nothing.
        /// </summary>
        public bool UseBerry()
        {
            if (!BeginAction() || Berries <= 0) return false;
            bool heals = Config.BerryHealHp > 0 && Hero.Hp < Hero.MaxHp;
            bool restores = Config.BerryRestoreMp > 0 && Hero.Mp < Hero.MaxMp;
            if (!heals && !restores) return false;
            Berries--;
            events.Add(new ItemUsedEvent(Hero.Id, ItemKind.Berry));
            if (heals) Heal(Hero, Config.BerryHealHp);
            if (restores) ChangeMana(Hero, Config.BerryRestoreMp);
            FinishHeroTurn(Config.Costs.PercentFor(ActionKind.Item));
            return true;
        }

        /// <summary>Whether the hero's skill in <paramref name="slot"/> can be used right now, and if not, why.</summary>
        public SkillCheck CheckSkill(int slot) => CheckSkill(slot, Hero.Facing);

        /// <summary>The same, for the skill aimed at <paramref name="aim"/> (strikes and dashes go that way).</summary>
        public SkillCheck CheckSkill(int slot, Direction8 aim)
        {
            var skills = Hero.Definition.Skills;
            if (slot < 0 || slot >= skills.Count) return SkillCheck.NoSkill;
            var skill = skills[slot];
            if (Hero.SkillCooldowns[slot] > 0) return SkillCheck.OnCooldown;
            if (Hero.Mp < skill.ManaCost) return SkillCheck.NotEnoughMana;
            switch (skill.Effect)
            {
                case SkillEffect.Strike: return FindStrikeTarget(aim, out _) != null ? SkillCheck.Ready : SkillCheck.NoTarget;
                case SkillEffect.Heal: return Hero.Hp < Hero.MaxHp ? SkillCheck.Ready : SkillCheck.NotNeeded;
                case SkillEffect.Dash: return DashDestination(skill.Power, aim) != Hero.Pos ? SkillCheck.Ready : SkillCheck.Blocked;
                default: return SkillCheck.Ready;
            }
        }

        /// <summary>
        /// Uses one of the hero's skills the way the hero faces: pays its mana, applies its effect, gains any mana it
        /// builds, then ends the turn with the skill's own AV cost and starts its cooldown. Refused (no turn used)
        /// unless <see cref="CheckSkill(int)"/> is Ready.
        /// </summary>
        public bool UseSkill(int slot) => UseSkill(slot, Hero.Facing);

        /// <summary>The same, aimed: strikes and dashes go toward <paramref name="aim"/>, turning the hero for free.</summary>
        public bool UseSkill(int slot, Direction8 aim)
        {
            if (!BeginAction() || CheckSkill(slot, aim) != SkillCheck.Ready) return false;
            var skill = Hero.Definition.Skills[slot];
            events.Add(new SkillUsedEvent(Hero.Id, skill));
            if (skill.ManaCost > 0) ChangeMana(Hero, -skill.ManaCost);

            switch (skill.Effect)
            {
                case SkillEffect.Strike:
                {
                    var target = FindStrikeTarget(aim, out var dir);
                    Hero.Facing = dir;
                    events.Add(new AttackEvent(Hero.Id, target.Id, dir));
                    ApplyDamage(Hero, target, CombatRules.RollHeavyAttack(Hero, target, Random, skill.Power));
                    break;
                }
                case SkillEffect.Heal:
                    Heal(Hero, Math.Max(1, Hero.MaxHp * skill.Power / 100));
                    break;
                case SkillEffect.Dash:
                {
                    var from = Hero.Pos;
                    Hero.Facing = aim;
                    Hero.Pos = DashDestination(skill.Power, aim);
                    events.Add(new DashedEvent(Hero.Id, from, Hero.Pos, aim));
                    PickUpItemUnderHero();
                    break;
                }
            }
            if (skill.ManaGain > 0 && State == RunState.InProgress) ChangeMana(Hero, skill.ManaGain);

            FinishHeroTurn(skill.CostPercent);
            Hero.SkillCooldowns[slot] = skill.Cooldown;
            return true;
        }

        /// <summary>The enemy in the <paramref name="preferred"/> direction (if the corner allows), otherwise the first adjacent one; null if none.</summary>
        Actor FindStrikeTarget(Direction8 preferred, out Direction8 direction)
        {
            direction = preferred;
            var faced = Map.IsCornerClear(Hero.Pos, direction) ? ActorAt(Hero.Pos + direction.ToOffset()) : null;
            if (faced != null && faced.Team != Hero.Team) return faced;
            foreach (var dir in Directions.All)
            {
                var other = ActorAt(Hero.Pos + dir.ToOffset());
                if (other == null || other.Team == Hero.Team || !Map.IsCornerClear(Hero.Pos, dir)) continue;
                direction = dir;
                return other;
            }
            return null;
        }

        /// <summary>
        /// Where a dash of up to <paramref name="tiles"/> toward <paramref name="direction"/> would end: before the first
        /// wall, blocked corner or actor. The hero's own tile if it can't move at all.
        /// </summary>
        public GridPos DashDestination(int tiles, Direction8 direction)
        {
            var pos = Hero.Pos;
            for (int i = 0; i < tiles; i++)
            {
                if (!Map.CanStep(pos, direction) || ActorAt(pos + direction.ToOffset()) != null) break;
                pos += direction.ToOffset();
            }
            return pos;
        }

        /// <summary>Take the stairs. Without a boss, the last floor's stairs clear the dungeon.</summary>
        public bool Descend()
        {
            if (!BeginAction() || !HeroOnStairs) return false;
            if (IsLastFloor)
            {
                EndRun(won: true);
                return true;
            }
            EnterFloor(Floor + 1);
            return true;
        }

        // ---- Setup helpers, also used by tests. ----

        /// <summary>
        /// Adds a monster. Regular monsters get tougher on deeper floors; bosses keep their own stats. Mid-fight a new
        /// monster's first turn is one full turn away, unless <paramref name="readyNow"/> (reinforcements arriving
        /// between rounds act in the coming round).
        /// </summary>
        public Actor SpawnEnemy(GridPos pos, ActorDefinition definition = null, bool readyNow = false)
        {
            var enemy = new Actor(nextId++, definition ?? Config.Enemy, Team.Enemy, pos);
            if (enemy.Definition.IsBoss)
            {
                enemy.SpecialCooldown = 2; // A moment's grace before the first slam.
            }
            else
            {
                int floorBonus = Floor - 1;
                enemy.MaxHp = enemy.Hp = enemy.MaxHp + floorBonus * 3;
                enemy.Attack += floorBonus;
                enemy.Defense += floorBonus / 2;
                enemy.ExpReward += floorBonus * 2;
            }
            actors.Add(enemy);
            if (InCombat) timeline.Add(enemy, readyNow);
            return enemy;
        }

        public FloorItem PlaceItem(GridPos pos, ItemKind kind)
        {
            var item = new FloorItem(nextId++, kind, pos);
            items.Add(item);
            return item;
        }

        // ---- Turn flow ----

        /// <summary>Clears the previous action's events. False if the run is already over.</summary>
        bool BeginAction()
        {
            events.Clear();
            return State == RunState.InProgress;
        }

        /// <summary>
        /// Ends the hero's action and plays out everything until the hero is up again. In combat that means running
        /// the timeline. Exploring, everyone else takes one turn (the original alternating rhythm); if that leaves an
        /// enemy alerted, a fight starts and the timeline takes over from a fresh start.
        /// </summary>
        void FinishHeroTurn(int cost)
        {
            Turn++;
            for (int i = 0; i < Hero.SkillCooldowns.Length; i++)
                if (Hero.SkillCooldowns[i] > 0) Hero.SkillCooldowns[i]--;

            if (InCombat && AnyEnemyAlerted())
            {
                timeline.EndTurn(Hero, cost);
                RunTimelineUntilHero();
                return;
            }
            if (InCombat) EndCombat(); // The hero's own action ended the fight.

            // Snapshot: actors can die (or the hero can) partway through.
            foreach (var enemy in actors.ToArray())
            {
                if (State != RunState.InProgress) return;
                if (enemy.Team == Team.Hero || !enemy.IsAlive) continue;
                TakeEnemyTurn(enemy);
            }
            if (State != RunState.InProgress) return;

            if (AnyEnemyAlerted())
            {
                StartCombat();
                RunTimelineUntilHero();
            }
            else
            {
                AdvanceClock(Timeline.TurnLength(Hero.Speed, cost));
            }
        }

        /// <summary>Plays turns in timeline order until it is the hero's turn again (or the run ends).</summary>
        void RunTimelineUntilHero()
        {
            while (State == RunState.InProgress)
            {
                var next = timeline.PeekNext(Hero);
                var elapsed = timeline.NextTurnOf(next.Id) - timeline.Now;
                timeline.AdvanceTo(next);
                AdvanceClock(elapsed);
                if (State != RunState.InProgress) return;
                if (next == Hero) break;

                var kind = TakeEnemyTurn(next);
                if (timeline.Contains(next.Id)) timeline.EndTurn(next, Config.Costs.PercentFor(kind));
            }
            if (State == RunState.InProgress && !AnyEnemyAlerted()) EndCombat();
        }

        void StartCombat()
        {
            InCombat = true;
            timeline.Start(actors);
            events.Add(new CombatStartedEvent());
        }

        void EndCombat()
        {
            InCombat = false;
            timeline.Clear();
            events.Add(new CombatEndedEvent());
        }

        bool AnyEnemyAlerted()
        {
            foreach (var actor in actors)
                if (actor.Team != Team.Hero && actor.Alerted) return true;
            return false;
        }

        /// <summary>Moves the run's AV clocks forward; regeneration and reinforcements happen as their intervals pass.</summary>
        void AdvanceClock(AvTime elapsed)
        {
            var runBefore = runTime;
            var floorBefore = floorTime;
            runTime += elapsed;
            floorTime += elapsed;
            for (int i = IntervalsPassed(runBefore, runTime, Config.RegenIntervalAv); i > 0; i--) Regenerate();
            for (int i = IntervalsPassed(floorBefore, floorTime, Config.ReinforcementIntervalAv); i > 0 && State == RunState.InProgress; i--) Reinforce();
        }

        static int IntervalsPassed(AvTime from, AvTime to, int interval) =>
            interval <= 0 ? 0 : (int)(to.Scale(1, interval).Floor() - from.Scale(1, interval).Floor());

        ActionKind TakeEnemyTurn(Actor enemy)
        {
            if (enemy.SpecialCooldown > 0) enemy.SpecialCooldown--;
            var intent = EnemyBrain.Decide(this, enemy);
            switch (intent.Kind)
            {
                case IntentKind.Attack:
                    ResolveAttack(enemy, intent.Direction);
                    return ActionKind.Attack;
                case IntentKind.Move:
                    if (Map.CanStep(enemy.Pos, intent.Direction) && ActorAt(enemy.Pos + intent.Direction.ToOffset()) == null)
                        Step(enemy, intent.Direction);
                    return ActionKind.Move;
                case IntentKind.Charge:
                    enemy.Charging = true;
                    events.Add(new BossActionEvent(enemy.Id, BossAction.Charge));
                    return ActionKind.Special;
                case IntentKind.Slam:
                    ResolveSlam(enemy);
                    return ActionKind.Special;
                case IntentKind.Summon:
                    SummonHelp(enemy);
                    return ActionKind.Special;
                default:
                    return ActionKind.Wait;
            }
        }

        void Step(Actor actor, Direction8 dir)
        {
            var from = actor.Pos;
            actor.Pos = from + dir.ToOffset();
            actor.Facing = dir;
            events.Add(new MovedEvent(actor.Id, from, actor.Pos, dir));
        }

        void ResolveAttack(Actor attacker, Direction8 dir)
        {
            attacker.Facing = dir;
            var target = Map.IsCornerClear(attacker.Pos, dir) ? ActorAt(attacker.Pos + dir.ToOffset()) : null;
            if (target != null && target.Team == attacker.Team) target = null;

            events.Add(new AttackEvent(attacker.Id, target?.Id ?? -1, dir));
            if (target == null) return;

            var roll = CombatRules.RollBasicAttack(attacker, target, Random);
            ApplyDamage(attacker, target, roll);
            if (attacker == Hero && State == RunState.InProgress) ChangeMana(Hero, CombatRules.BasicAttackManaGain);
        }

        /// <summary>Adds (or with a negative amount, spends) mana within 0..MaxMp; records the change if there was one.</summary>
        void ChangeMana(Actor actor, int amount)
        {
            int after = Math.Max(0, Math.Min(actor.MaxMp, actor.Mp + amount));
            int change = after - actor.Mp;
            if (change == 0) return;
            actor.Mp = after;
            events.Add(new ManaChangedEvent(actor.Id, change, after));
        }

        /// <summary>The boss's wound-up slam: heavy damage to every foe next to it. Stepping away during the wind-up dodges it.</summary>
        void ResolveSlam(Actor boss)
        {
            boss.Charging = false;
            boss.SpecialCooldown = EnemyBrain.SlamCooldown;
            events.Add(new BossActionEvent(boss.Id, BossAction.Slam));
            foreach (var target in actors.ToArray())
            {
                if (target.Team == boss.Team || !target.IsAlive) continue;
                if (GridPos.ChebyshevDistance(boss.Pos, target.Pos) > EnemyBrain.SlamRadius) continue;
                ApplyDamage(boss, target, CombatRules.RollHeavyAttack(boss, target, Random, EnemyBrain.SlamDamagePercent));
                if (State != RunState.InProgress) return;
            }
        }

        void SummonHelp(Actor boss)
        {
            boss.CalledForHelp = true;
            events.Add(new BossActionEvent(boss.Id, BossAction.Summon));
            int summoned = 0;
            int first = Random.Range(0, 8);
            for (int i = 0; i < 8 && summoned < EnemyBrain.HelpersSummoned; i++)
            {
                var pos = boss.Pos + ((Direction8)((first + i) % 8)).ToOffset();
                if (!Map.IsWalkable(pos) || ActorAt(pos) != null) continue;
                var helper = SpawnEnemy(pos, Config.Enemy);
                helper.Alerted = true;
                events.Add(new ActorSpawnedEvent(helper.Id));
                summoned++;
            }
        }

        void ApplyDamage(Actor attacker, Actor target, DamageRoll roll)
        {
            target.Hp = Math.Max(0, target.Hp - roll.Amount);
            events.Add(new DamageEvent(target.Id, roll.Amount, roll.Critical, target.Hp));
            if (target.Hp == 0) Kill(target, attacker);
        }

        void Kill(Actor victim, Actor killer)
        {
            actors.Remove(victim);
            timeline.Remove(victim.Id);
            events.Add(new DiedEvent(victim.Id));
            if (victim == Hero)
            {
                EndRun(won: false);
                return;
            }
            if (killer.Team == Team.Hero) GainExp(victim.ExpReward);
            if (victim.Definition.IsBoss && IsBossFloor) EndRun(won: true);
        }

        void GainExp(int amount)
        {
            if (amount <= 0) return;
            Hero.Exp += amount;
            events.Add(new ExpGainedEvent(Hero.Id, amount));
            while (Hero.Exp >= CombatRules.ExpToNextLevel(Hero.Level))
            {
                Hero.Exp -= CombatRules.ExpToNextLevel(Hero.Level);
                CombatRules.ApplyLevelUp(Hero);
                events.Add(new LevelUpEvent(Hero.Id, Hero.Level));
            }
            Progress.Record(Hero.Level, Hero.Exp);
        }

        void Heal(Actor actor, int amount)
        {
            int healed = Math.Min(amount, actor.MaxHp - actor.Hp);
            actor.Hp += healed;
            events.Add(new HealedEvent(actor.Id, healed, actor.Hp));
        }

        void PickUpItemUnderHero()
        {
            var item = ItemAt(Hero.Pos);
            if (item == null || Berries >= Config.MaxBerries) return;
            items.Remove(item);
            Berries++;
            events.Add(new ItemPickedUpEvent(item.Id, item.Kind));
        }

        void Regenerate()
        {
            if (Hero.IsAlive && Hero.Hp < Hero.MaxHp) Hero.Hp++;
        }

        /// <summary>Every so often a new enemy wanders in out of the hero's sight, so camping on a floor isn't free.</summary>
        void Reinforce()
        {
            if (!Config.Populate || IsBossFloor) return;
            if (actors.Count - 1 >= Config.MaxEnemies) return;
            if (TryFindSpawnTile(Random, room => room != Map.RoomIndexAt(Hero.Pos), Config.SightRange + 3, out var pos))
                events.Add(new ActorSpawnedEvent(SpawnEnemy(pos, readyNow: true).Id));
        }

        void EndRun(bool won)
        {
            State = won ? RunState.Won : RunState.Lost;
            events.Add(new RunEndedEvent(won));
        }

        // ---- Floors ----

        void EnterFloor(int floor)
        {
            Floor = floor;
            floorTime = AvTime.Zero;
            InCombat = false;
            timeline.Clear();
            int floorSeed = Rng.DeriveSeed(Seed, floor);
            Map = Config.MapFactory != null ? Config.MapFactory(floor, floorSeed)
                : IsBossFloor ? DungeonGenerator.GenerateBossFloor(floorSeed, Config.Generation)
                : DungeonGenerator.Generate(floorSeed, Config.Generation);

            actors.Clear();
            items.Clear();
            Hero.Pos = Map.Start;
            Hero.Facing = Direction8.S;
            actors.Add(Hero);

            if (Config.Populate)
            {
                var floorRng = new Rng(Rng.DeriveSeed(floorSeed, 1));
                if (IsBossFloor) PopulateBossFloor(floorRng);
                else Populate(floorRng);
            }
            events.Add(new FloorStartedEvent(floor));
        }

        void Populate(Rng floorRng)
        {
            int startRoom = Map.RoomIndexAt(Map.Start);
            int enemyCount = Math.Min(Config.MaxEnemies, Config.EnemiesOnFirstFloor + (Floor - 1) * Config.ExtraEnemiesPerFloor);
            for (int i = 0; i < enemyCount; i++)
                if (TryFindSpawnTile(floorRng, room => room != startRoom, 4, out var pos)) SpawnEnemy(pos);

            for (int i = 0; i < Config.ItemsPerFloor; i++)
                if (TryFindSpawnTile(floorRng, room => true, 0, out var pos)) PlaceItem(pos, ItemKind.Berry);
        }

        /// <summary>The boss waits in the middle of the arena (the last room); berries wait in the antechamber.</summary>
        void PopulateBossFloor(Rng floorRng)
        {
            int arena = Map.Rooms.Count - 1;
            if (TryFindFreeTileNear(Map.Rooms[arena].Center, out var bossPos)) SpawnEnemy(bossPos, Config.Boss);

            for (int i = 0; i < Config.BossFloorBerries; i++)
                if (TryFindSpawnTile(floorRng, room => room == 0, 0, out var pos)) PlaceItem(pos, ItemKind.Berry);
        }

        /// <summary>
        /// A random free room tile (no actor, item or stairs), in a room accepted by <paramref name="roomFilter"/>,
        /// at least <paramref name="minDistance"/> from the hero.
        /// </summary>
        bool TryFindSpawnTile(Rng rng, Func<int, bool> roomFilter, int minDistance, out GridPos pos)
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                int roomIndex = rng.Range(0, Map.Rooms.Count);
                if (!roomFilter(roomIndex) && Map.Rooms.Count > 1) continue;
                var room = Map.Rooms[roomIndex];
                pos = new GridPos(rng.Range(room.X, room.XMax + 1), rng.Range(room.Y, room.YMax + 1));
                if (!Map.IsWalkable(pos) || pos == Map.Stairs || ActorAt(pos) != null || ItemAt(pos) != null) continue;
                if (GridPos.ChebyshevDistance(pos, Hero.Pos) < minDistance) continue;
                return true;
            }
            pos = default;
            return false;
        }

        /// <summary>The free walkable tile closest to <paramref name="center"/>, searching outward ring by ring.</summary>
        bool TryFindFreeTileNear(GridPos center, out GridPos pos)
        {
            for (int radius = 0; radius <= 4; radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                        pos = new GridPos(center.X + dx, center.Y + dy);
                        if (Map.IsWalkable(pos) && ActorAt(pos) == null) return true;
                    }
                }
            }
            pos = default;
            return false;
        }
    }
}
