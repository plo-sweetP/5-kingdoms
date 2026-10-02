using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    public enum RunState { InProgress, Won, Lost }

    /// <summary>
    /// One dungeon expedition: the turn-based rules for exploring floors and fighting.
    /// Each hero action resolves the hero's turn and then every enemy's, recording what happened in
    /// <see cref="Events"/> for the presentation layer to animate. No Unity dependency, so it can be
    /// unit tested and simulated headlessly.
    /// </summary>
    public sealed class DungeonRun
    {
        readonly List<Actor> actors = new List<Actor>();
        readonly List<FloorItem> items = new List<FloorItem>();
        readonly List<GameEvent> events = new List<GameEvent>();
        int nextId = 1;
        int turnsOnFloor;

        public DungeonRun(int seed, DungeonRunConfig config = null)
        {
            Seed = seed;
            Config = config ?? new DungeonRunConfig();
            Random = new Rng(Rng.DeriveSeed(seed, int.MaxValue));
            Hero = new Actor(nextId++, Config.Hero, Team.Hero, default);
            EnterFloor(1);
        }

        public int Seed { get; }
        public DungeonRunConfig Config { get; }
        public int Floor { get; private set; }
        public DungeonMap Map { get; private set; }
        public Actor Hero { get; }
        public IReadOnlyList<Actor> Actors => actors;
        public IReadOnlyList<FloorItem> Items => items;
        public int Berries { get; set; }
        public RunState State { get; private set; } = RunState.InProgress;

        /// <summary>Hero turns taken this run.</summary>
        public int Turn { get; private set; }

        /// <summary>Everything that happened during the most recent action, in order.</summary>
        public IReadOnlyList<GameEvent> Events => events;

        public bool HeroOnStairs => Hero.Pos == Map.Stairs;
        public bool IsLastFloor => Floor >= Config.FloorCount;

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
                EndHeroTurn();
                return true;
            }
            if (occupant != null || !Map.CanStep(Hero.Pos, dir))
            {
                events.Add(new FacingChangedEvent(Hero.Id, dir));
                return false;
            }

            Step(Hero, dir);
            PickUpItemUnderHero();
            EndHeroTurn();
            return true;
        }

        /// <summary>Attack the tile the hero is facing, even if it's empty (a missed swing still uses the turn).</summary>
        public bool Attack()
        {
            if (!BeginAction()) return false;
            ResolveAttack(Hero, Hero.Facing);
            EndHeroTurn();
            return true;
        }

        public bool Wait()
        {
            if (!BeginAction()) return false;
            EndHeroTurn();
            return true;
        }

        /// <summary>Eat a berry to heal. Refused (no turn used) when out of berries or already at full HP.</summary>
        public bool UseBerry()
        {
            if (!BeginAction() || Berries <= 0 || Hero.Hp >= Hero.MaxHp) return false;
            Berries--;
            Heal(Hero, Config.BerryHeal);
            EndHeroTurn();
            return true;
        }

        /// <summary>Take the stairs. On the last floor this clears the dungeon.</summary>
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

        public Actor SpawnEnemy(GridPos pos, ActorDefinition definition = null)
        {
            var enemy = new Actor(nextId++, definition ?? Config.Enemy, Team.Enemy, pos);
            int floorBonus = Floor - 1;
            enemy.MaxHp = enemy.Hp = enemy.MaxHp + floorBonus * 3;
            enemy.Attack += floorBonus;
            enemy.Defense += floorBonus / 2;
            enemy.ExpReward += floorBonus * 2;
            actors.Add(enemy);
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

        void EndHeroTurn()
        {
            Turn++;
            turnsOnFloor++;

            // Snapshot: actors can die (or the hero can) partway through the enemy phase.
            foreach (var enemy in actors.ToArray())
            {
                if (State != RunState.InProgress) return;
                if (enemy.Team == Team.Hero || !enemy.IsAlive) continue;
                TakeEnemyTurn(enemy);
            }
            if (State != RunState.InProgress) return;

            Regenerate();
            Reinforce();
        }

        void TakeEnemyTurn(Actor enemy)
        {
            var intent = EnemyBrain.Decide(this, enemy);
            switch (intent.Kind)
            {
                case IntentKind.Attack:
                    ResolveAttack(enemy, intent.Direction);
                    break;
                case IntentKind.Move:
                    if (Map.CanStep(enemy.Pos, intent.Direction) && ActorAt(enemy.Pos + intent.Direction.ToOffset()) == null)
                        Step(enemy, intent.Direction);
                    break;
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
            target.Hp = Math.Max(0, target.Hp - roll.Amount);
            events.Add(new DamageEvent(target.Id, roll.Amount, roll.Critical, target.Hp));
            if (target.Hp == 0) Kill(target, attacker);
        }

        void Kill(Actor victim, Actor killer)
        {
            actors.Remove(victim);
            events.Add(new DiedEvent(victim.Id));
            if (victim == Hero)
            {
                EndRun(won: false);
                return;
            }
            if (killer == Hero) GainExp(victim.ExpReward);
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
            if (Config.RegenInterval > 0 && Turn % Config.RegenInterval == 0 && Hero.Hp < Hero.MaxHp)
                Hero.Hp++;
        }

        /// <summary>Every so often a new enemy wanders in out of the hero's sight, so camping on a floor isn't free.</summary>
        void Reinforce()
        {
            if (!Config.Populate || Config.ReinforcementInterval <= 0 || turnsOnFloor % Config.ReinforcementInterval != 0) return;
            if (actors.Count - 1 >= Config.MaxEnemies) return;
            if (TryFindSpawnTile(Random, Map.RoomIndexAt(Hero.Pos), Config.SightRange + 3, out var pos))
                events.Add(new ActorSpawnedEvent(SpawnEnemy(pos).Id));
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
            turnsOnFloor = 0;
            int floorSeed = Rng.DeriveSeed(Seed, floor);
            Map = Config.MapFactory != null
                ? Config.MapFactory(floor, floorSeed)
                : DungeonGenerator.Generate(floorSeed, Config.Generation);

            actors.Clear();
            items.Clear();
            Hero.Pos = Map.Start;
            Hero.Facing = Direction8.S;
            actors.Add(Hero);

            if (Config.Populate) Populate(new Rng(Rng.DeriveSeed(floorSeed, 1)));
            events.Add(new FloorStartedEvent(floor));
        }

        void Populate(Rng floorRng)
        {
            int startRoom = Map.RoomIndexAt(Map.Start);
            int enemyCount = Math.Min(Config.MaxEnemies, Config.EnemiesOnFirstFloor + (Floor - 1) * Config.ExtraEnemiesPerFloor);
            for (int i = 0; i < enemyCount; i++)
                if (TryFindSpawnTile(floorRng, startRoom, 4, out var pos)) SpawnEnemy(pos);

            for (int i = 0; i < Config.ItemsPerFloor; i++)
                if (TryFindSpawnTile(floorRng, -1, 0, out var pos) && ItemAt(pos) == null) PlaceItem(pos, ItemKind.Berry);
        }

        /// <summary>A random free room tile, not in <paramref name="avoidRoom"/> and at least <paramref name="minDistance"/> from the hero.</summary>
        bool TryFindSpawnTile(Rng rng, int avoidRoom, int minDistance, out GridPos pos)
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                int roomIndex = rng.Range(0, Map.Rooms.Count);
                if (roomIndex == avoidRoom && Map.Rooms.Count > 1) continue;
                var room = Map.Rooms[roomIndex];
                pos = new GridPos(rng.Range(room.X, room.XMax + 1), rng.Range(room.Y, room.YMax + 1));
                if (!Map.IsWalkable(pos) || pos == Map.Stairs || ActorAt(pos) != null) continue;
                if (GridPos.ChebyshevDistance(pos, Hero.Pos) < minDistance) continue;
                return true;
            }
            pos = default;
            return false;
        }
    }
}
