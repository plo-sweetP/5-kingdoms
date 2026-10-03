using System;
using System.Collections.Generic;

namespace FiveKingdoms.Core
{
    public enum RunState { InProgress, Won, Lost }

    /// <summary>Whether a skill can be used right now, and if not, why (for button states and messages).</summary>
    public enum SkillCheck { Ready, NoSkill, OnCooldown, NotCharged, NoTarget, NotNeeded, Blocked }

    /// <summary>
    /// One dungeon expedition with a party of heroes: the turn-based rules for exploring floors and fighting.
    /// The player controls the leader (<see cref="Hero"/>); the other party members act on their own
    /// (<see cref="PartnerBrain"/>) with the tactic the player gave them. While exploring, the leader acts, then each
    /// partner, then every enemy once. Once an enemy notices the party, combat starts and turns follow the
    /// action-value <see cref="Timeline"/> (faster actors act more often, partners included) until no enemy is
    /// alerted. Time is kept in exact AV either way: regeneration and reinforcements run on it. Each leader action
    /// records what happened in <see cref="Events"/> for the presentation layer to animate. Every hero starts from a
    /// <see cref="HeroProgress"/> and EXP earned is recorded back into it as it happens. The run is lost when the whole
    /// party has fallen (or, if the dungeon says so, when the leader falls). There is no mana: skills sit out a turn
    /// after use, and ultimates need a full charge meter (PROGRESSION.md, "Skill resources"). Ranged hits are weaker
    /// than melee ones, and weaker still at point-blank range ("Ranged vs melee"). No Unity dependency, so it can be unit
    /// tested and simulated headlessly.
    /// </summary>
    public sealed class DungeonRun
    {
        readonly List<Actor> actors = new List<Actor>();
        readonly List<Actor> party = new List<Actor>();
        readonly Dictionary<int, HeroProgress> progressById = new Dictionary<int, HeroProgress>();
        readonly List<FloorItem> items = new List<FloorItem>();
        readonly List<FloorTrap> traps = new List<FloorTrap>();
        readonly List<GameEvent> events = new List<GameEvent>();
        readonly Timeline timeline = new Timeline();
        const int MaxTurnsBetweenLeaderTurns = 10000;

        /// <summary>Own turns two party members must wait before swapping places with each other again.</summary>
        public const int SwapBlockTurns = 3;
        int nextId = 1;

        /// <summary>The hero whose ultimate is resolving: its own hits don't refill its charge meter.</summary>
        Actor ultimateUser;
        AvTime runTime;
        AvTime floorTime;

        /// <summary>A run with one hero (or, without one, the dungeon's <see cref="DungeonRunConfig.Party"/> or <see cref="DungeonRunConfig.Hero"/>).</summary>
        public DungeonRun(int seed, DungeonRunConfig config = null, HeroProgress hero = null)
            : this(seed, config, hero != null ? new[] { hero } : null)
        {
        }

        /// <summary>A run with a party, leader first.</summary>
        public DungeonRun(int seed, DungeonRunConfig config, IReadOnlyList<HeroProgress> heroes)
        {
            Seed = seed;
            Config = config ?? new DungeonRunConfig();
            Random = new Rng(Rng.DeriveSeed(seed, int.MaxValue));
            if (heroes == null || heroes.Count == 0)
            {
                var definitions = Config.Party ?? new[] { Config.Hero };
                var fresh = new List<HeroProgress>();
                foreach (var definition in definitions) fresh.Add(new HeroProgress(definition));
                heroes = fresh;
            }
            foreach (var progress in heroes)
            {
                var member = new Actor(nextId++, progress.Definition, Team.Hero, default, progress.Level) { Exp = progress.Exp };
                party.Add(member);
                progressById[member.Id] = progress;
            }
            Hero = party[0];
            EnterFloor(1);
        }

        public int Seed { get; }
        public DungeonRunConfig Config { get; }
        public int Floor { get; private set; }
        public DungeonMap Map { get; private set; }

        /// <summary>The party member the player controls (the leader).</summary>
        public Actor Hero { get; private set; }

        /// <summary>Every party member in party order, fallen ones included.</summary>
        public IReadOnlyList<Actor> Party => party;

        /// <summary>The leader's saved progress.</summary>
        public HeroProgress Progress => progressById[Hero.Id];

        public HeroProgress ProgressOf(Actor member) => progressById[member.Id];

        public IReadOnlyList<Actor> Actors => actors;
        public IReadOnlyList<FloorItem> Items => items;

        /// <summary>Traps set on this floor (Uzuki's snares), waiting for an enemy to step on them.</summary>
        public IReadOnlyList<FloorTrap> Traps => traps;
        public int Berries { get; set; }
        public RunState State { get; private set; } = RunState.InProgress;

        /// <summary>Leader turns taken this run.</summary>
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

        public FloorTrap TrapAt(GridPos pos)
        {
            foreach (var trap in traps)
                if (trap.Pos == pos) return trap;
            return null;
        }

        /// <summary>
        /// The next <paramref name="count"/> turns in combat, starting with the leader's current one, assuming each action
        /// costs a normal turn. Empty while exploring.
        /// </summary>
        public IReadOnlyList<TimelineTurn> Forecast(int count) =>
            InCombat ? timeline.Forecast(Hero, count) : (IReadOnlyList<TimelineTurn>)Array.Empty<TimelineTurn>();

        /// <summary>Changes an actor's speed (buffs later). Mid-fight it keeps the actor's progress toward its next turn.</summary>
        public void SetSpeed(Actor actor, int speed) => timeline.ChangeSpeed(actor, speed);

        /// <summary>How a partner plays when the player isn't controlling it. Takes no time.</summary>
        public void SetTactic(Actor member, PartyTactic tactic) => member.Tactic = tactic;

        // ---- Leader actions. Each returns true when it used up the leader's turn. ----

        public bool Execute(HeroCommand command)
        {
            switch (command.Kind)
            {
                case HeroCommandKind.Move: return Move(command.Direction);
                case HeroCommandKind.Attack: return command.Aimed ? Attack(command.Direction) : Attack();
                case HeroCommandKind.Wait: return Wait();
                case HeroCommandKind.UseBerry: return UseBerry();
                case HeroCommandKind.Descend: return Descend();
                case HeroCommandKind.Skill: return command.Aimed ? UseSkill(command.Slot, command.Direction) : UseSkill(command.Slot);
                case HeroCommandKind.SwitchLeader: return SwitchLeader(command.Slot);
                case HeroCommandKind.Ultimate: return command.Aimed ? UseUltimate(command.Direction) : UseUltimate();
                default: throw new ArgumentOutOfRangeException(nameof(command), command.Kind, null);
            }
        }

        /// <summary>
        /// Walk one tile, attack whatever enemy stands there, or swap places with a partner. Bumping a wall only turns
        /// the leader and costs no turn.
        /// </summary>
        public bool Move(Direction8 dir)
        {
            if (!BeginAction() || !TryMove(Hero, dir, allowSwap: true, out int cost)) return false;
            FinishLeaderTurn(cost);
            return true;
        }

        /// <summary>Attack along the way the leader faces, even at nothing (a missed swing or shot still uses the turn).</summary>
        public bool Attack() => Attack(Hero.Facing);

        /// <summary>Turn toward <paramref name="direction"/> and attack along it.</summary>
        public bool Attack(Direction8 direction)
        {
            if (!BeginAction()) return false;
            ResolveAttack(Hero, direction);
            FinishLeaderTurn(Config.Costs.PercentFor(ActionKind.Attack));
            return true;
        }

        public bool Wait()
        {
            if (!BeginAction()) return false;
            FinishLeaderTurn(Config.Costs.PercentFor(ActionKind.Wait));
            return true;
        }

        /// <summary>The leader eats a berry and heals. Refused (no turn used) when out of berries or already at full HP.</summary>
        public bool UseBerry()
        {
            if (!BeginAction() || Berries <= 0 || Config.BerryHealHp <= 0 || Hero.Hp >= Hero.MaxHp) return false;
            Berries--;
            events.Add(new ItemUsedEvent(Hero.Id, ItemKind.Berry));
            Heal(Hero, Config.BerryHealHp);
            FinishLeaderTurn(Config.Costs.PercentFor(ActionKind.Item));
            return true;
        }

        /// <summary>Whether the leader's skill in <paramref name="slot"/> can be used right now, and if not, why.</summary>
        public SkillCheck CheckSkill(int slot) => CheckSkill(Hero, slot, Hero.Facing);

        /// <summary>The same, for the skill aimed at <paramref name="aim"/> (strikes, shots and dashes go that way).</summary>
        public SkillCheck CheckSkill(int slot, Direction8 aim) => CheckSkill(Hero, slot, aim);

        /// <summary>Whether <paramref name="user"/> could use its skill in <paramref name="slot"/> aimed at <paramref name="aim"/>.</summary>
        public SkillCheck CheckSkill(Actor user, int slot, Direction8 aim)
        {
            var skills = user.Definition.Skills;
            if (slot < 0 || slot >= skills.Count) return SkillCheck.NoSkill;
            if (user.SkillCooldowns[slot] > 0) return SkillCheck.OnCooldown;
            return CheckTarget(user, skills[slot], aim);
        }

        /// <summary>Whether the leader's ultimate can be used right now (it needs a full charge meter and a target).</summary>
        public SkillCheck CheckUltimate() => CheckUltimate(Hero, Hero.Facing);

        public SkillCheck CheckUltimate(Direction8 aim) => CheckUltimate(Hero, aim);

        public SkillCheck CheckUltimate(Actor user, Direction8 aim)
        {
            var ultimate = user.Definition.Ultimate;
            if (ultimate == null) return SkillCheck.NoSkill;
            if (user.Charge < CombatRules.MaxCharge) return SkillCheck.NotCharged;
            return CheckTarget(user, ultimate, aim);
        }

        /// <summary>Whether a skill aimed at <paramref name="aim"/> would have something to do.</summary>
        SkillCheck CheckTarget(Actor user, SkillDefinition skill, Direction8 aim)
        {
            switch (skill.Effect)
            {
                case SkillEffect.Strike: return FindStrikeTarget(user, aim, out _) != null ? SkillCheck.Ready : SkillCheck.NoTarget;
                case SkillEffect.Shot when skill.RollTiles > 0:
                    return DashDestination(user, skill.RollTiles, aim) != user.Pos ? SkillCheck.Ready : SkillCheck.Blocked;
                case SkillEffect.Shot:
                case SkillEffect.Mark:
                case SkillEffect.Area:
                    return FindShotTarget(user, aim, skill.Range, out _, out _) != null ? SkillCheck.Ready : SkillCheck.NoTarget;
                case SkillEffect.Heal: return HealTargets(user, skill).Count > 0 ? SkillCheck.Ready : SkillCheck.NotNeeded;
                case SkillEffect.Dash: return DashDestination(user, skill.Power, aim) != user.Pos ? SkillCheck.Ready : SkillCheck.Blocked;
                default: return SkillCheck.Ready;
            }
        }

        /// <summary>
        /// Uses one of the leader's skills the way it faces: applies its effect, then ends the turn with the skill's own
        /// AV cost and starts its cooldown. Refused (no turn used) unless <see cref="CheckSkill(int)"/> is Ready.
        /// </summary>
        public bool UseSkill(int slot) => UseSkill(slot, Hero.Facing);

        /// <summary>The same, aimed: strikes, shots and dashes go toward <paramref name="aim"/>, turning the leader for free.</summary>
        public bool UseSkill(int slot, Direction8 aim)
        {
            if (!BeginAction() || !TryUseSkill(Hero, slot, aim, out int cost)) return false;
            FinishLeaderTurn(cost);
            return true;
        }

        /// <summary>
        /// The leader's ultimate, the way it faces: empties the charge meter and applies its effect. Refused (no turn used)
        /// unless <see cref="CheckUltimate()"/> is Ready.
        /// </summary>
        public bool UseUltimate() => UseUltimate(Hero.Facing);

        public bool UseUltimate(Direction8 aim)
        {
            if (!BeginAction() || !TryUseUltimate(Hero, aim, out int cost)) return false;
            FinishLeaderTurn(cost);
            return true;
        }

        /// <summary>
        /// Takes control of another living party member. Exploring, no time passes and the new leader moves next. Mid-fight
        /// it was the old leader's turn, so the old leader plays it on its own and the fight runs on to the new leader's turn.
        /// </summary>
        public bool SwitchLeader(int partyIndex)
        {
            if (!BeginAction() || partyIndex < 0 || partyIndex >= party.Count) return false;
            var member = party[partyIndex];
            if (member == Hero || !member.IsAlive) return false;
            var previous = Hero;
            Hero = member;
            events.Add(new LeaderChangedEvent(member.Id));
            if (!InCombat) return false;

            int cost = TakePartnerTurn(previous, started: true);
            if (State != RunState.InProgress) return true;
            if (timeline.Contains(previous.Id)) timeline.EndTurn(previous, cost);
            RunTimelineUntilLeader();
            return true;
        }

        /// <summary>Take the stairs; the whole party goes down. Without a boss, the last floor's stairs clear the dungeon.</summary>
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

        // ---- Questions the AI asks ----

        /// <summary>Where a dash of up to <paramref name="tiles"/> toward <paramref name="direction"/> would take the leader.</summary>
        public GridPos DashDestination(int tiles, Direction8 direction) => DashDestination(Hero, tiles, direction);

        /// <summary>
        /// Where a dash of up to <paramref name="tiles"/> toward <paramref name="direction"/> would end: before the first
        /// wall, blocked corner or actor. The actor's own tile if it can't move at all.
        /// </summary>
        public GridPos DashDestination(Actor actor, int tiles, Direction8 direction)
        {
            var pos = actor.Pos;
            for (int i = 0; i < tiles; i++)
            {
                if (!Map.CanStep(pos, direction) || ActorAt(pos + direction.ToOffset()) != null) break;
                pos += direction.ToOffset();
            }
            return pos;
        }

        /// <summary>
        /// The first foe straight along <paramref name="direction"/> within <paramref name="range"/> tiles. Walls and
        /// blocked corners stop the line; allies don't (arrows fly past friends). <paramref name="distance"/> is how far
        /// it got: to the foe, or to where the line stopped.
        /// </summary>
        public Actor FirstFoeInLine(Actor from, Direction8 direction, int range, out int distance) =>
            FirstFoeInLine(from.Pos, from.Team, direction, range, out distance);

        /// <summary>The same, from any tile, for a shooter of <paramref name="team"/> (the AI asks "could I shoot from there?").</summary>
        public Actor FirstFoeInLine(GridPos from, Team team, Direction8 direction, int range, out int distance)
        {
            var pos = from;
            distance = 0;
            for (int step = 1; step <= range; step++)
            {
                if (!Map.IsCornerClear(pos, direction)) break;
                var next = pos + direction.ToOffset();
                if (!Map.IsWalkable(next)) break;
                pos = next;
                distance = step;
                var occupant = ActorAt(pos);
                if (occupant != null && occupant.Team != team) return occupant;
            }
            if (distance == 0) distance = 1;
            return null;
        }

        /// <summary>Whether a shooter of <paramref name="team"/> standing on <paramref name="from"/> would have any foe in line within <paramref name="range"/>.</summary>
        public bool AnyFoeInLine(GridPos from, Team team, int range)
        {
            foreach (var dir in Directions.All)
                if (FirstFoeInLine(from, team, dir, range, out _) != null) return true;
            return false;
        }

        /// <summary>Whether a foe of <paramref name="team"/> stands next to <paramref name="pos"/>.</summary>
        public bool FoeAdjacent(GridPos pos, Team team)
        {
            foreach (var actor in actors)
                if (actor.Team != team && GridPos.ChebyshevDistance(actor.Pos, pos) == 1) return true;
            return false;
        }

        public bool FoeAdjacent(Actor actor) => FoeAdjacent(actor.Pos, actor.Team);

        /// <summary>Steps (Chebyshev) from <paramref name="pos"/> to the nearest foe of <paramref name="team"/>; int.MaxValue if none.</summary>
        public int DistanceToNearestFoe(GridPos pos, Team team)
        {
            int best = int.MaxValue;
            foreach (var actor in actors)
                if (actor.Team != team) best = Math.Min(best, GridPos.ChebyshevDistance(actor.Pos, pos));
            return best;
        }

        /// <summary>
        /// Whether party AI may move <paramref name="mover"/> into <paramref name="other"/>'s tile, swapping the two
        /// (PROGRESSION.md, "No swap loops"): only a melee hero swaps, never with another melee hero, never back with
        /// the one it just swapped with, and only when the swap puts it next to a foe or strictly closer to one. The
        /// player's own moves always swap.
        /// </summary>
        public bool CanSwap(Actor mover, Actor other)
        {
            if (other == null || other == mover || other.Team != mover.Team || !other.IsAlive) return false;
            if (mover.Definition.IsRanged || !other.Definition.IsRanged) return false;
            if (mover.SwappedWithId == other.Id && mover.SwapBlockTurns > 0) return false;
            if (other.SwappedWithId == mover.Id && other.SwapBlockTurns > 0) return false;
            if (GridPos.ChebyshevDistance(mover.Pos, other.Pos) != 1 || !Map.IsCornerClear(mover.Pos, Directions.Toward(mover.Pos, other.Pos))) return false;
            int after = DistanceToNearestFoe(other.Pos, mover.Team);
            return after == 1 || after < DistanceToNearestFoe(mover.Pos, mover.Team);
        }

        /// <summary>
        /// The foe a shot aimed at <paramref name="preferred"/> would hit; if nothing is that way, the nearest foe along any
        /// other straight line in range. Null if none.
        /// </summary>
        public Actor FindShotTarget(Actor user, Direction8 preferred, int range, out Direction8 direction, out int distance)
        {
            direction = preferred;
            var target = FirstFoeInLine(user, preferred, range, out distance);
            if (target != null) return target;
            int best = int.MaxValue;
            foreach (var dir in Directions.All)
            {
                var candidate = FirstFoeInLine(user, dir, range, out int candidateDistance);
                if (candidate == null || candidateDistance >= best) continue;
                best = candidateDistance;
                target = candidate;
                direction = dir;
                distance = candidateDistance;
            }
            return target;
        }

        /// <summary>
        /// What <paramref name="user"/>'s <paramref name="skill"/> (null: its weapon attack) can reach and be aimed at right
        /// now, for the targeting highlight. Strikes reach the neighbors; shots, marks, areas and ranged weapon attacks
        /// reach along the 8 lines (past allies, up to walls) and target the first foe on each; rolls and dashes pick
        /// where to move. Cooldowns and charge aren't checked here.
        /// </summary>
        public AimInfo AimFor(Actor user, SkillDefinition skill)
        {
            var info = new AimInfo { NeedsAim = skill == null || skill.NeedsAim };
            if (!info.NeedsAim) return info;

            bool moves = skill != null && (skill.Effect == SkillEffect.Dash || skill.RollTiles > 0);
            bool line = skill == null ? user.Definition.AttackRange > 1 : skill.Effect != SkillEffect.Strike && !moves;
            int range = skill == null ? user.Definition.AttackRange : moves ? (skill.RollTiles > 0 ? skill.RollTiles : skill.Power) : skill.Range;
            info.AreaRadius = skill != null && skill.Effect == SkillEffect.Area ? skill.Radius : 0;
            info.PicksTile = moves;

            foreach (var dir in Directions.All)
            {
                if (moves)
                {
                    var end = DashDestination(user, range, dir);
                    if (end == user.Pos) continue;
                    for (var pos = user.Pos; pos != end;)
                    {
                        pos += dir.ToOffset();
                        info.AddReach(pos);
                    }
                    info.AddOption(new AimOption(end, dir, null));
                }
                else if (line)
                {
                    var pos = user.Pos;
                    for (int step = 1; step <= range; step++)
                    {
                        if (!Map.IsCornerClear(pos, dir) || !Map.IsWalkable(pos + dir.ToOffset())) break;
                        pos += dir.ToOffset();
                        info.AddReach(pos);
                        var occupant = ActorAt(pos);
                        if (occupant == null || occupant.Team == user.Team) continue;
                        info.AddOption(new AimOption(pos, dir, occupant));
                        break;
                    }
                }
                else
                {
                    var pos = user.Pos + dir.ToOffset();
                    if (!Map.IsWalkable(pos) || !Map.IsCornerClear(user.Pos, dir)) continue;
                    info.AddReach(pos);
                    var occupant = ActorAt(pos);
                    if (occupant != null && occupant.Team != user.Team) info.AddOption(new AimOption(pos, dir, occupant));
                }
            }
            return info;
        }

        // ---- Setup helpers, also used by tests. ----

        /// <summary>
        /// Adds a monster at the floor's level, so its level growth makes deeper floors tougher (bosses have none and keep
        /// their own stats). Mid-fight a new monster's first turn is one full turn away, unless <paramref name="readyNow"/>
        /// (reinforcements arriving between rounds act in the coming round).
        /// </summary>
        public Actor SpawnEnemy(GridPos pos, ActorDefinition definition = null, bool readyNow = false)
        {
            var enemy = new Actor(nextId++, definition ?? Config.Enemy, Team.Enemy, pos, level: Floor);
            if (enemy.Definition.IsBoss) enemy.SpecialCooldown = 2; // A moment's grace before the first slam.
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
        /// Ends the leader's action and plays out everything until the leader is up again. In combat that means running
        /// the timeline. Exploring, each partner and then every enemy takes one turn (the original alternating rhythm);
        /// if that leaves an enemy alerted, a fight starts and the timeline takes over from a fresh start.
        /// </summary>
        void FinishLeaderTurn(int cost)
        {
            Turn++;
            EndOwnTurn(Hero);

            if (InCombat && AnyEnemyAlerted())
            {
                timeline.EndTurn(Hero, cost);
                RunTimelineUntilLeader();
                return;
            }
            if (InCombat) EndCombat(); // The leader's own action ended the fight.

            // Snapshots: actors can fall (or the whole party can) partway through.
            foreach (var partner in party.ToArray())
            {
                if (State != RunState.InProgress) return;
                if (partner == Hero || !partner.IsAlive) continue;
                TakePartnerTurn(partner);
            }
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
                RunTimelineUntilLeader();
            }
            else
            {
                AdvanceClock(Timeline.TurnLength(Hero.Speed, cost));
                if (State == RunState.InProgress) StartLeaderTurn();
            }
        }

        /// <summary>Plays turns in timeline order (partners and enemies alike) until it is the leader's turn again.</summary>
        void RunTimelineUntilLeader()
        {
            for (int turns = 0; State == RunState.InProgress; turns++)
            {
                // A fight where the leader never gets a turn would freeze the game; fail loudly instead.
                if (turns > MaxTurnsBetweenLeaderTurns || !timeline.Contains(Hero.Id))
                    throw new InvalidOperationException($"The timeline never returns to the leader {Hero} (turn {Turn}, B{Floor}F).");
                var next = timeline.PeekNext(Hero);
                var elapsed = timeline.NextTurnOf(next.Id) - timeline.Now;
                timeline.AdvanceTo(next);
                AdvanceClock(elapsed);
                if (State != RunState.InProgress) return;
                if (next == Hero) break;

                int cost = next.Team == Team.Hero ? TakePartnerTurn(next) : TakeEnemyTurn(next);
                if (timeline.Contains(next.Id)) timeline.EndTurn(next, cost);
            }
            if (State != RunState.InProgress) return;
            if (!AnyEnemyAlerted()) EndCombat();
            StartLeaderTurn();
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

        /// <summary>The leader is up.</summary>
        void StartLeaderTurn() => StartTurn(Hero);

        /// <summary>
        /// Any actor's turn is starting: whatever it gave "until its next turn" ends, and an aura it holds heals the allies
        /// next to it and counts down a turn.
        /// </summary>
        void StartTurn(Actor actor)
        {
            ExpireStatusesFrom(actor);
            var aura = actor.FindStatus(StatusKind.Aura);
            if (aura == null) return;
            foreach (var ally in AlliesWithin(actor, 1))
                if (ally != actor && ally.Hp < ally.MaxHp) Heal(ally, Math.Max(1, actor.MaxHp * aura.HealPercent / 100));
            if (--aura.TurnsLeft > 0) return;
            actor.Statuses.Remove(aura);
            events.Add(new StatusEndedEvent(actor.Id, StatusKind.Aura));
        }

        /// <summary>
        /// After any actor's own turn: its skills recharge a step and its timed statuses tick down (an aura counts its
        /// holder's turns as they start instead). A party member's swap block ticks down too.
        /// </summary>
        void EndOwnTurn(Actor actor)
        {
            for (int i = 0; i < actor.SkillCooldowns.Length; i++)
                if (actor.SkillCooldowns[i] > 0) actor.SkillCooldowns[i]--;
            if (actor.SwapBlockTurns > 0) actor.SwapBlockTurns--;
            for (int i = actor.Statuses.Count - 1; i >= 0; i--)
            {
                var status = actor.Statuses[i];
                if (status.EndsOnSourceTurn || status.Kind == StatusKind.Aura || --status.TurnsLeft > 0) continue;
                actor.Statuses.RemoveAt(i);
                events.Add(new StatusEndedEvent(actor.Id, status.Kind));
            }
        }

        /// <summary>
        /// A partner's turn: its brain picks an action for its tactic. Returns the action's AV cost. <paramref name="started"/>:
        /// the turn already began as the leader's (the player handed control to someone else mid-turn).
        /// </summary>
        int TakePartnerTurn(Actor partner, bool started = false)
        {
            if (!started) StartTurn(partner);
            var command = PartnerBrain.Decide(this, partner);
            int cost = Config.Costs.PercentFor(ActionKind.Wait);
            var aim = command.Aimed ? command.Direction : partner.Facing;
            switch (command.Kind)
            {
                case HeroCommandKind.Move:
                    var occupant = ActorAt(partner.Pos + command.Direction.ToOffset());
                    if (TryMove(partner, command.Direction, allowSwap: CanSwap(partner, occupant), out int moveCost)) cost = moveCost;
                    break;
                case HeroCommandKind.Attack:
                    ResolveAttack(partner, aim);
                    cost = Config.Costs.PercentFor(ActionKind.Attack);
                    break;
                case HeroCommandKind.Skill:
                    if (TryUseSkill(partner, command.Slot, aim, out int skillCost)) cost = skillCost;
                    break;
                case HeroCommandKind.Ultimate:
                    if (TryUseUltimate(partner, aim, out int ultimateCost)) cost = ultimateCost;
                    break;
            }
            EndOwnTurn(partner);
            return cost;
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

        /// <summary>A monster's turn. Returns the action's AV cost.</summary>
        int TakeEnemyTurn(Actor enemy)
        {
            StartTurn(enemy);
            var stun = enemy.FindStatus(StatusKind.Stunned);
            if (stun != null)
            {
                enemy.Statuses.Remove(stun);
                events.Add(new TurnSkippedEvent(enemy.Id));
                events.Add(new StatusEndedEvent(enemy.Id, StatusKind.Stunned));
                EndOwnTurn(enemy);
                return Config.Costs.PercentFor(ActionKind.Wait);
            }
            if (enemy.SpecialCooldown > 0) enemy.SpecialCooldown--;
            var intent = EnemyBrain.Decide(this, enemy);
            var kind = ActionKind.Wait;
            switch (intent.Kind)
            {
                case IntentKind.Attack:
                    ResolveAttack(enemy, intent.Direction);
                    kind = ActionKind.Attack;
                    break;
                case IntentKind.Move:
                    if (enemy.FindStatus(StatusKind.Rooted) == null && Map.CanStep(enemy.Pos, intent.Direction) &&
                        ActorAt(enemy.Pos + intent.Direction.ToOffset()) == null)
                    {
                        Step(enemy, intent.Direction);
                        TriggerTrapUnder(enemy);
                    }
                    kind = ActionKind.Move;
                    break;
                case IntentKind.Charge:
                    enemy.Charging = true;
                    events.Add(new BossActionEvent(enemy.Id, BossAction.Charge));
                    kind = ActionKind.Special;
                    break;
                case IntentKind.Slam:
                    ResolveSlam(enemy);
                    kind = ActionKind.Special;
                    break;
                case IntentKind.Summon:
                    SummonHelp(enemy);
                    kind = ActionKind.Special;
                    break;
            }
            EndOwnTurn(enemy);
            return Config.Costs.PercentFor(kind);
        }

        /// <summary>
        /// Moves <paramref name="actor"/> one tile: an enemy there is attacked instead, and with <paramref name="allowSwap"/>
        /// a party member there swaps places (the pair then can't swap back for a few turns). False (no turn used) when a
        /// wall or someone else is in the way. A hero stepping out of melee counts a retreat step.
        /// </summary>
        bool TryMove(Actor actor, Direction8 dir, bool allowSwap, out int cost)
        {
            cost = 0;
            actor.Facing = dir;
            var occupant = ActorAt(actor.Pos + dir.ToOffset());
            if (occupant != null && occupant.Team != actor.Team && Map.IsCornerClear(actor.Pos, dir))
            {
                ResolveAttack(actor, dir);
                cost = Config.Costs.PercentFor(ActionKind.Attack);
                return true;
            }
            if (occupant != null && allowSwap && occupant.Team == actor.Team && Map.CanStep(actor.Pos, dir))
            {
                var from = actor.Pos;
                events.Add(new SwappedEvent(actor.Id, occupant.Id));
                Step(actor, dir);
                occupant.PreviousPos = occupant.Pos;
                occupant.Pos = from;
                occupant.Facing = Directions.Toward(occupant.PreviousPos, from);
                events.Add(new MovedEvent(occupant.Id, occupant.PreviousPos, from, occupant.Facing));
                actor.SwappedWithId = occupant.Id;
                occupant.SwappedWithId = actor.Id;
                actor.SwapBlockTurns = occupant.SwapBlockTurns = SwapBlockTurns;
                actor.RetreatSteps = 0;
                PickUpItemUnder(actor);
                PickUpItemUnder(occupant);
                cost = Config.Costs.PercentFor(ActionKind.Move);
                return true;
            }
            if (occupant != null || !Map.CanStep(actor.Pos, dir))
            {
                events.Add(new FacingChangedEvent(actor.Id, dir));
                return false;
            }

            bool wasInMelee = actor.Team == Team.Hero && FoeAdjacent(actor);
            Step(actor, dir);
            if (actor.Team == Team.Hero)
            {
                actor.RetreatSteps = wasInMelee && !FoeAdjacent(actor) ? actor.RetreatSteps + 1 : 0;
                PickUpItemUnder(actor);
            }
            cost = Config.Costs.PercentFor(ActionKind.Move);
            return true;
        }

        void Step(Actor actor, Direction8 dir)
        {
            var from = actor.Pos;
            actor.PreviousPos = from;
            actor.Pos = from + dir.ToOffset();
            actor.Facing = dir;
            events.Add(new MovedEvent(actor.Id, from, actor.Pos, dir));
        }

        /// <summary>
        /// A weapon attack (always ready): a melee blow, or a shot along the line for heroes with reach (Uzuki's bow),
        /// which deals less, and less again at point-blank range. A hero's attack charges its ultimate.
        /// </summary>
        void ResolveAttack(Actor attacker, Direction8 dir)
        {
            attacker.Facing = dir;
            int range = attacker.Definition.AttackRange;
            bool ranged = range > 1;
            var target = FirstFoeInLine(attacker, dir, range, out int distance);
            events.Add(new AttackEvent(attacker.Id, target?.Id ?? -1, dir, ranged: ranged, distance: distance));
            if (target != null) ApplyDamage(attacker, target, CombatRules.RollBasicAttack(attacker, target, Random, ReachPercent(attacker, ranged)));
            if (attacker.Team == Team.Hero) Acted(attacker);
        }

        /// <summary>What's left of a hit from <paramref name="attacker"/> after the ranged cuts (100 for a melee hit).</summary>
        int ReachPercent(Actor attacker, bool ranged) => CombatRules.ReachPercent(ranged, ranged && FoeAdjacent(attacker));

        /// <summary>A hero attacked or used a skill: its ultimate charges a little, and it isn't retreating any more.</summary>
        void Acted(Actor hero)
        {
            hero.RetreatSteps = 0;
            if (State == RunState.InProgress) GainCharge(hero, CombatRules.ChargePerAction);
        }

        /// <summary>Fills a hero's charge meter up to full; records the change if there was one. Only heroes with an ultimate charge.</summary>
        void GainCharge(Actor hero, int amount)
        {
            if (hero.Definition.Ultimate == null || hero == ultimateUser) return;
            int after = Math.Max(0, Math.Min(CombatRules.MaxCharge, hero.Charge + amount));
            int change = after - hero.Charge;
            if (change == 0) return;
            hero.Charge = after;
            events.Add(new ChargeChangedEvent(hero.Id, change, after));
        }

        // ---- Skills ----

        bool TryUseSkill(Actor user, int slot, Direction8 aim, out int cost)
        {
            cost = 0;
            if (CheckSkill(user, slot, aim) != SkillCheck.Ready) return false;
            var skill = user.Definition.Skills[slot];
            Perform(user, skill, aim);
            Acted(user);
            user.SkillCooldowns[slot] = skill.Cooldown + 1; // The end of this very turn takes the first step off.
            cost = skill.CostPercent;
            return true;
        }

        /// <summary>A hero's ultimate: empties its charge meter, then the effect (whose own hits don't charge it again).</summary>
        bool TryUseUltimate(Actor user, Direction8 aim, out int cost)
        {
            cost = 0;
            if (CheckUltimate(user, aim) != SkillCheck.Ready) return false;
            var ultimate = user.Definition.Ultimate;
            events.Add(new ChargeChangedEvent(user.Id, -user.Charge, 0));
            user.Charge = 0;
            user.RetreatSteps = 0;
            ultimateUser = user;
            try
            {
                Perform(user, ultimate, aim);
            }
            finally
            {
                ultimateUser = null;
            }
            cost = ultimate.CostPercent;
            return true;
        }

        /// <summary>A skill's or ultimate's effect, aimed at <paramref name="aim"/>.</summary>
        void Perform(Actor user, SkillDefinition skill, Direction8 aim)
        {
            events.Add(new SkillUsedEvent(user.Id, skill));
            switch (skill.Effect)
            {
                case SkillEffect.Strike:
                    ResolveStrike(user, skill, aim);
                    break;
                case SkillEffect.Shot:
                    if (skill.RollTiles > 0) aim = Roll(user, skill, aim);
                    ResolveShot(user, skill, aim);
                    break;
                case SkillEffect.Area:
                    ResolveArea(user, skill, aim);
                    break;
                case SkillEffect.Aura:
                    AddStatus(user, StatusKind.Aura, user, skill.StatusPower, skill.StatusTurns, endsOnSourceTurn: false, healPercent: skill.Power);
                    break;
                case SkillEffect.Mark:
                {
                    var target = FindShotTarget(user, aim, skill.Range, out var dir, out _);
                    user.Facing = dir;
                    ClearMarksFrom(user);
                    AddStatus(target, StatusKind.Mark, user, skill.Power, skill.StatusTurns, endsOnSourceTurn: false);
                    break;
                }
                case SkillEffect.Heal:
                    foreach (var member in HealTargets(user, skill))
                        Heal(member, Math.Max(1, (skill.HealsFromUser ? user.MaxHp : member.MaxHp) * skill.Power / 100));
                    break;
                case SkillEffect.Dash:
                {
                    var from = user.Pos;
                    user.Facing = aim;
                    user.PreviousPos = from;
                    user.Pos = DashDestination(user, skill.Power, aim);
                    events.Add(new DashedEvent(user.Id, from, user.Pos, aim));
                    PickUpItemUnder(user);
                    break;
                }
                case SkillEffect.Guard:
                    foreach (var member in AlliesWithin(user, skill.Radius))
                        AddStatus(member, StatusKind.Guard, user, skill.Power, turns: 0, endsOnSourceTurn: true);
                    break;
            }
        }

        /// <summary>
        /// A strike skill: its hits on the target (more with a wall behind a shove; a flurry moves on to another foe next to
        /// the user when the target falls), then the shove itself, the enemy behind for a piercing blow, and any slow,
        /// stun or status.
        /// </summary>
        void ResolveStrike(Actor user, SkillDefinition skill, Direction8 aim)
        {
            var target = FindStrikeTarget(user, aim, out var dir);
            user.Facing = dir;
            var behind = target.Pos + dir.ToOffset();
            bool pinned = skill.Shove && !CanPush(target, dir);
            int percent = pinned ? skill.Power * (100 + skill.WallBonusPercent) / 100 : skill.Power;
            var victim = target;
            var victimDir = dir;
            for (int hit = 0; hit < skill.Hits && State == RunState.InProgress; hit++)
            {
                if (!victim.IsAlive)
                {
                    victim = FindStrikeTarget(user, victimDir, out victimDir);
                    if (victim == null) break;
                    user.Facing = victimDir;
                }
                events.Add(new AttackEvent(user.Id, victim.Id, victimDir));
                ApplyDamage(user, victim, CombatRules.RollDamage(user, victim, Random, percent, element: skill.Element));
            }
            if (skill.Shove && target.IsAlive && State == RunState.InProgress) Push(target, dir, 1);
            if (target.IsAlive) ApplyOnHit(user, target, skill, slow: true);

            if (!skill.Pierce || State != RunState.InProgress || !Map.IsCornerClear(target.Pos == behind ? user.Pos : target.Pos, dir)) return;
            var second = ActorAt(behind);
            if (second == null || second.Team == user.Team || second == target) return;
            events.Add(new AttackEvent(user.Id, second.Id, dir, distance: 2));
            ApplyDamage(user, second, CombatRules.RollDamage(user, second, Random, skill.Power, element: skill.Element));
        }

        /// <summary>
        /// Rolls the user up to the skill's RollTiles toward <paramref name="aim"/>, leaving its trap on the tile it left.
        /// Returns the way to shoot afterwards: back where it came from (the shot itself finds the nearest foe in line).
        /// </summary>
        Direction8 Roll(Actor user, SkillDefinition skill, Direction8 aim)
        {
            var from = user.Pos;
            user.Facing = aim;
            user.PreviousPos = from;
            user.Pos = DashDestination(user, skill.RollTiles, aim);
            events.Add(new DashedEvent(user.Id, from, user.Pos, aim));
            PickUpItemUnder(user);
            if (skill.LeavesTrap != TrapKind.None) PlaceTrap(skill.LeavesTrap, from, user);
            return (Direction8)(((int)aim + 4) % 8);
        }

        /// <summary>Sets a trap, keeping at most the dungeon's limit per floor (the oldest goes).</summary>
        void PlaceTrap(TrapKind kind, GridPos pos, Actor owner)
        {
            if (TrapAt(pos) != null) return;
            if (traps.Count >= Config.MaxTrapsPerFloor) traps.RemoveAt(0);
            var trap = new FloorTrap(nextId++, kind, pos, owner.Id);
            traps.Add(trap);
            events.Add(new TrapPlacedEvent(trap.Id, kind, pos));
        }

        /// <summary>An enemy just arrived on <paramref name="actor"/>'s tile: a trap there goes off (a snare roots it for its next 2 turns).</summary>
        void TriggerTrapUnder(Actor actor)
        {
            if (actor.Team == Team.Hero || !actor.IsAlive) return;
            var trap = TrapAt(actor.Pos);
            if (trap == null) return;
            traps.Remove(trap);
            events.Add(new TrapTriggeredEvent(trap.Id, actor.Id));
            var owner = FindActor(trap.OwnerId);
            if (trap.Kind != TrapKind.Snare) return;
            // Bosses can't be rooted, as they can't be stunned: the snare costs them time instead.
            if (actor.Definition.IsBoss) Delay(actor, BossSnareDelayPercent);
            else AddStatus(actor, StatusKind.Rooted, owner ?? actor, 0, SnareTurns, endsOnSourceTurn: false);
        }

        const int BossSnareDelayPercent = 30;

        /// <summary>A snare holds for the turn its victim steps on it plus its next two.</summary>
        const int SnareTurns = 3;

        bool CanPush(Actor actor, Direction8 dir) =>
            !actor.Definition.IsBoss && Map.CanStep(actor.Pos, dir) && ActorAt(actor.Pos + dir.ToOffset()) == null;

        /// <summary>Shoves or knocks <paramref name="actor"/> up to <paramref name="tiles"/> tiles; bosses don't budge. Traps go off.</summary>
        bool Push(Actor actor, Direction8 dir, int tiles)
        {
            bool moved = false;
            for (int i = 0; i < tiles && actor.IsAlive && CanPush(actor, dir); i++)
            {
                var from = actor.Pos;
                actor.PreviousPos = from;
                actor.Pos = from + dir.ToOffset();
                events.Add(new PushedEvent(actor.Id, blocked: false));
                events.Add(new MovedEvent(actor.Id, from, actor.Pos, (Direction8)(((int)dir + 4) % 8)));
                moved = true;
                TriggerTrapUnder(actor);
            }
            if (!moved) events.Add(new PushedEvent(actor.Id, blocked: true));
            return moved;
        }

        /// <summary>Removes the mark this user has on anyone (one mark per hunter).</summary>
        void ClearMarksFrom(Actor user)
        {
            foreach (var actor in actors)
            {
                var mark = actor.FindStatus(StatusKind.Mark);
                if (mark == null || mark.SourceId != user.Id) continue;
                actor.Statuses.Remove(mark);
                events.Add(new StatusEndedEvent(actor.Id, StatusKind.Mark));
            }
        }

        /// <summary>
        /// A shot skill. With a Multishot weapon (the Hunter Bow), a ranged physical skill fires 2 arrows at reduced damage:
        /// the first at the aimed target, the second at another foe in range if there is one, else the same target. Each
        /// arrow rolls damage and crit on its own; a slow or knockback lands only once per target. Shots take the ranged
        /// cuts (point-blank if a foe is next to the shooter as it fires). Nothing in line after a roll: no shot.
        /// </summary>
        void ResolveShot(Actor user, SkillDefinition skill, Direction8 aim)
        {
            var first = FindShotTarget(user, aim, skill.Range, out var firstDirection, out int firstDistance);
            if (first == null) return;
            user.Facing = firstDirection;
            var weapon = user.Weapon;
            bool multishot = weapon != null && weapon.Passive == WeaponPassive.Multishot &&
                             skill.Kind == DamageKind.Physical && skill.Reach == AttackReach.Ranged;
            int arrows = multishot ? 2 : 1;
            int percent = multishot ? skill.Power * weapon.PassivePower / 100 : skill.Power;
            int reach = ReachPercent(user, skill.IsRanged);

            var hit = new List<Actor>();
            for (int arrow = 0; arrow < arrows && State == RunState.InProgress; arrow++)
            {
                var target = first;
                var direction = firstDirection;
                int distance = firstDistance;
                if (arrow > 0 && TryFindOtherFoeInLine(user, skill.Range, hit, out var other, out var otherDirection, out int otherDistance))
                {
                    target = other;
                    direction = otherDirection;
                    distance = otherDistance;
                }
                if (!target.IsAlive) break;
                distance = GridPos.ChebyshevDistance(user.Pos, target.Pos); // It may have been knocked back by the first arrow.

                events.Add(new AttackEvent(user.Id, target.Id, direction, ranged: true, distance: distance));
                ApplyDamage(user, target, CombatRules.RollDamage(user, target, Random, percent, element: skill.Element, reachPercent: reach));
                bool firstHit = !hit.Contains(target);
                if (firstHit) hit.Add(target);
                if (!target.IsAlive) continue;
                ApplyOnHit(user, target, skill, slow: firstHit);
                if (firstHit && skill.Knockback > 0 && State == RunState.InProgress) Push(target, direction, skill.Knockback);
            }
        }

        /// <summary>
        /// An area skill (Volley): centered on the foe a shot aimed that way would hit, every foe within Radius of it takes
        /// the skill's hits, with the ranged cuts.
        /// </summary>
        void ResolveArea(Actor user, SkillDefinition skill, Direction8 aim)
        {
            var center = FindShotTarget(user, aim, skill.Range, out var direction, out _);
            user.Facing = direction;
            int reach = ReachPercent(user, skill.IsRanged);
            var area = center.Pos;
            events.Add(new AreaAttackEvent(user.Id, area, skill.Radius));
            var victims = new List<Actor>();
            foreach (var actor in actors)
                if (actor.Team != user.Team && GridPos.ChebyshevDistance(actor.Pos, area) <= skill.Radius) victims.Add(actor);
            for (int hit = 0; hit < skill.Hits; hit++)
            {
                foreach (var victim in victims)
                {
                    if (State != RunState.InProgress) return;
                    if (!victim.IsAlive) continue;
                    ApplyDamage(user, victim, CombatRules.RollDamage(user, victim, Random, skill.Power, element: skill.Element, reachPercent: reach));
                }
            }
        }

        /// <summary>The nearest foe along any straight line in range that hasn't been hit yet.</summary>
        bool TryFindOtherFoeInLine(Actor user, int range, List<Actor> exclude, out Actor target, out Direction8 direction, out int distance)
        {
            target = null;
            direction = Direction8.S;
            distance = 0;
            int best = int.MaxValue;
            foreach (var dir in Directions.All)
            {
                var candidate = FirstFoeInLine(user, dir, range, out int candidateDistance);
                if (candidate == null || exclude.Contains(candidate) || candidateDistance >= best) continue;
                best = candidateDistance;
                target = candidate;
                direction = dir;
                distance = candidateDistance;
            }
            return target != null;
        }

        /// <summary>What a skill does to a foe it hit, besides damage: a slow (once per use), a stun, and a status.</summary>
        void ApplyOnHit(Actor user, Actor target, SkillDefinition skill, bool slow)
        {
            if (slow && skill.DelayPercent > 0) Delay(target, skill.DelayPercent);
            if (slow && skill.StunChance > 0)
            {
                if (target.Definition.IsBoss) Delay(target, skill.BossDelayPercent); // Bosses can't be stunned: they lose time instead.
                else if (Random.Range(0, 1000) < StatusChance(user, target, skill.StunChance))
                    AddStatus(target, StatusKind.Stunned, user, 0, 1, endsOnSourceTurn: false);
            }
            if (skill.Status.HasValue)
                AddStatus(target, skill.Status.Value, user, skill.StatusPower, skill.StatusTurns, endsOnSourceTurn: false);
        }

        /// <summary>GEAR.md: chance = base x (1 + Affinity) x (1 - Resist), in tenths of a percent.</summary>
        static int StatusChance(Actor user, Actor target, int basePercent) =>
            (int)((long)basePercent * 10 * (1000 + user.Affinity) / 1000 * Math.Max(0, 1000 - target.Resist) / 1000);

        /// <summary>Pushes a foe's next turn back on the timeline (only in a fight: exploring, everyone acts once anyway).</summary>
        void Delay(Actor target, int percent)
        {
            if (percent <= 0 || !InCombat || !timeline.Contains(target.Id)) return;
            timeline.Delay(target, percent);
            events.Add(new TurnDelayedEvent(target.Id, percent));
        }

        /// <summary>The enemy in the <paramref name="preferred"/> direction (if the corner allows), otherwise the first adjacent one; null if none.</summary>
        Actor FindStrikeTarget(Actor user, Direction8 preferred, out Direction8 direction)
        {
            direction = preferred;
            var faced = Map.IsCornerClear(user.Pos, direction) ? ActorAt(user.Pos + direction.ToOffset()) : null;
            if (faced != null && faced.Team != user.Team) return faced;
            foreach (var dir in Directions.All)
            {
                var other = ActorAt(user.Pos + dir.ToOffset());
                if (other == null || other.Team == user.Team || !Map.IsCornerClear(user.Pos, dir)) continue;
                direction = dir;
                return other;
            }
            return null;
        }

        /// <summary>The user and living allies within <paramref name="radius"/> tiles of it.</summary>
        List<Actor> AlliesWithin(Actor user, int radius)
        {
            var allies = new List<Actor> { user };
            foreach (var member in party)
                if (member != user && member.IsAlive && actors.Contains(member) && GridPos.ChebyshevDistance(member.Pos, user.Pos) <= radius)
                    allies.Add(member);
            return allies;
        }

        /// <summary>Who a heal would reach right now (only those missing HP).</summary>
        public List<Actor> HealTargets(Actor user, SkillDefinition skill)
        {
            int radius = skill.HealTarget == HealTarget.Self ? 0 : skill.HealTarget == HealTarget.SelfOrAdjacentAlly ? 1 : skill.Radius;
            var targets = AlliesWithin(user, radius);
            targets.RemoveAll(member => member.Hp >= member.MaxHp);
            if (skill.HealTarget != HealTarget.SelfOrAdjacentAlly || targets.Count <= 1) return targets;
            Actor neediest = targets[0];
            foreach (var member in targets)
                if ((long)member.Hp * neediest.MaxHp < (long)neediest.Hp * member.MaxHp) neediest = member;
            return new List<Actor> { neediest };
        }

        // ---- Statuses ----

        /// <summary>Puts a status on <paramref name="target"/>, replacing one of the same kind.</summary>
        void AddStatus(Actor target, StatusKind kind, Actor source, int power, int turns, bool endsOnSourceTurn, int healPercent = 0)
        {
            target.Statuses.RemoveAll(status => status.Kind == kind);
            target.Statuses.Add(new StatusEffect(kind, source.Id, power, turns, endsOnSourceTurn, healPercent));
            events.Add(new StatusAppliedEvent(target.Id, kind, source.Id));
        }

        /// <summary><paramref name="source"/>'s turn is starting: whatever it gave "until its next turn" ends.</summary>
        void ExpireStatusesFrom(Actor source)
        {
            foreach (var actor in actors)
            {
                for (int i = actor.Statuses.Count - 1; i >= 0; i--)
                {
                    var status = actor.Statuses[i];
                    if (!status.EndsOnSourceTurn || status.SourceId != source.Id) continue;
                    actor.Statuses.RemoveAt(i);
                    events.Add(new StatusEndedEvent(actor.Id, status.Kind));
                }
            }
        }

        /// <summary><paramref name="source"/> fell: everything it put on others ends.</summary>
        void ClearStatusesFrom(Actor source)
        {
            foreach (var actor in actors)
            {
                for (int i = actor.Statuses.Count - 1; i >= 0; i--)
                {
                    var status = actor.Statuses[i];
                    if (status.SourceId != source.Id) continue;
                    actor.Statuses.RemoveAt(i);
                    events.Add(new StatusEndedEvent(actor.Id, status.Kind));
                }
            }
        }

        /// <summary>
        /// How much of a hit from <paramref name="attacker"/> this actor takes, in percent: a guard cuts it, and so does an
        /// ally's aura next to it; a mark raises it (only for the hunter who placed it; with no attacker given, any mark
        /// counts).
        /// </summary>
        public int DamageTakenPercent(Actor target, Actor attacker = null)
        {
            int percent = 100;
            var mark = target.FindStatus(StatusKind.Mark);
            if (mark != null && (attacker == null || mark.SourceId == attacker.Id)) percent += mark.Power;
            var guard = target.FindStatus(StatusKind.Guard);
            if (guard != null) percent -= guard.Power;
            var aura = AuraProtecting(target);
            if (aura != null) percent -= aura.Power;
            return percent;
        }

        /// <summary>The strongest aura an ally next to <paramref name="target"/> holds, or null.</summary>
        StatusEffect AuraProtecting(Actor target)
        {
            StatusEffect best = null;
            foreach (var actor in actors)
            {
                if (actor == target || actor.Team != target.Team || GridPos.ChebyshevDistance(actor.Pos, target.Pos) != 1) continue;
                var aura = actor.FindStatus(StatusKind.Aura);
                if (aura != null && (best == null || aura.Power > best.Power)) best = aura;
            }
            return best;
        }

        int AdjustForStatuses(Actor target, Actor attacker, int amount)
        {
            int percent = DamageTakenPercent(target, attacker);
            return percent == 100 ? amount : Math.Max(1, amount * percent / 100);
        }

        // ---- Damage, healing, defeat ----

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
                ApplyDamage(boss, target, CombatRules.RollDamage(boss, target, Random, EnemyBrain.SlamDamagePercent));
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

        /// <summary>Lands a hit. A hero charges its ultimate for each hit it lands and each hit it takes and survives.</summary>
        void ApplyDamage(Actor attacker, Actor target, DamageRoll roll)
        {
            int amount = AdjustForStatuses(target, attacker, roll.Amount);
            target.Hp = Math.Max(0, target.Hp - amount);
            events.Add(new DamageEvent(target.Id, amount, roll.Critical, target.Hp));
            if (target.Hp == 0) Kill(target, attacker);
            if (State != RunState.InProgress) return;
            if (attacker.Team == Team.Hero && attacker.IsAlive) GainCharge(attacker, CombatRules.ChargePerHitDealt);
            if (target.Team == Team.Hero && target.IsAlive) GainCharge(target, CombatRules.ChargePerHitTaken);
        }

        void Kill(Actor victim, Actor killer)
        {
            actors.Remove(victim);
            timeline.Remove(victim.Id);
            events.Add(new DiedEvent(victim.Id));
            ClearStatusesFrom(victim);
            JumpMark(victim);
            if (victim.Team == Team.Hero)
            {
                Actor nextLeader = null;
                foreach (var member in party)
                {
                    if (!member.IsAlive) continue;
                    nextLeader = member;
                    break;
                }
                if (nextLeader == null || victim == Hero && Config.DefeatWhenLeaderFalls)
                {
                    EndRun(won: false);
                    return;
                }
                if (victim == Hero)
                {
                    Hero = nextLeader;
                    events.Add(new LeaderChangedEvent(Hero.Id));
                }
                return;
            }
            if (killer.Team == Team.Hero) GainExp(victim.ExpReward);
            if (victim.Definition.IsBoss && IsBossFloor) EndRun(won: true);
        }

        /// <summary>A marked foe fell: its hunter's mark jumps to the nearest other foe in range, for the turns it had left.</summary>
        void JumpMark(Actor victim)
        {
            var mark = victim.FindStatus(StatusKind.Mark);
            var hunter = mark != null ? FindActor(mark.SourceId) : null;
            if (hunter == null) return;
            Actor next = null;
            int best = int.MaxValue;
            foreach (var actor in actors)
            {
                if (actor.Team == hunter.Team) continue;
                int distance = GridPos.ChebyshevDistance(actor.Pos, hunter.Pos);
                if (distance <= MarkJumpRange && distance < best)
                {
                    best = distance;
                    next = actor;
                }
            }
            if (next != null) AddStatus(next, StatusKind.Mark, hunter, mark.Power, Math.Max(1, mark.TurnsLeft), endsOnSourceTurn: false);
        }

        const int MarkJumpRange = SkillCatalog.RangedReach;

        /// <summary>Every party member still standing gets the EXP in full, as in Mystery Dungeon.</summary>
        void GainExp(int amount)
        {
            if (amount <= 0) return;
            foreach (var member in party)
            {
                if (!member.IsAlive) continue;
                member.Exp += amount;
                events.Add(new ExpGainedEvent(member.Id, amount));
                while (member.Exp >= CombatRules.ExpToNextLevel(member.Level))
                {
                    member.Exp -= CombatRules.ExpToNextLevel(member.Level);
                    CombatRules.ApplyLevelUp(member);
                    events.Add(new LevelUpEvent(member.Id, member.Level));
                }
                progressById[member.Id].Record(member.Level, member.Exp);
            }
        }

        void Heal(Actor actor, int amount)
        {
            int healed = Math.Min(amount, actor.MaxHp - actor.Hp);
            actor.Hp += healed;
            events.Add(new HealedEvent(actor.Id, healed, actor.Hp));
        }

        /// <summary>Party members pick up whatever they step on, into the shared bag.</summary>
        void PickUpItemUnder(Actor actor)
        {
            var item = ItemAt(actor.Pos);
            if (item == null || Berries >= Config.MaxBerries) return;
            items.Remove(item);
            Berries++;
            events.Add(new ItemPickedUpEvent(item.Id, item.Kind));
        }

        void Regenerate()
        {
            foreach (var member in party)
                if (member.IsAlive) member.Hp = Math.Min(member.MaxHp, member.Hp + Config.RegenHp);
        }

        /// <summary>Every so often a new enemy wanders in out of the party's sight, so camping on a floor isn't free.</summary>
        void Reinforce()
        {
            if (!Config.Populate || IsBossFloor) return;
            if (actors.Count - LivingPartyCount >= Config.MaxEnemies) return;
            if (TryFindSpawnTile(Random, room => room != Map.RoomIndexAt(Hero.Pos), Config.SightRange + 3, out var pos))
                events.Add(new ActorSpawnedEvent(SpawnEnemy(pos, readyNow: true).Id));
        }

        int LivingPartyCount
        {
            get
            {
                int count = 0;
                foreach (var actor in actors)
                    if (actor.Team == Team.Hero) count++;
                return count;
            }
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
            traps.Clear();
            Hero.Pos = Hero.PreviousPos = Map.Start;
            Hero.Facing = Direction8.S;
            Hero.Statuses.Clear();
            actors.Add(Hero);
            foreach (var member in party)
            {
                if (member == Hero || !member.IsAlive) continue;
                member.Statuses.Clear();
                if (!TryFindFreeTileNear(Map.Start, out var pos)) continue; // No room on a tiny hand-built floor.
                member.Pos = member.PreviousPos = pos;
                member.Facing = Direction8.S;
                actors.Add(member);
            }

            if (Config.Populate)
            {
                var floorRng = new Rng(Rng.DeriveSeed(floorSeed, 1));
                if (IsBossFloor) PopulateBossFloor(floorRng);
                else Populate(floorRng);
            }
            events.Add(new FloorStartedEvent(floor));
        }

        /// <summary>
        /// Monsters away from the start room, in packs (so no single hero can clear a room alone; PROGRESSION.md, "Bigger
        /// fights"), some packs bringing a fast monster; then berries anywhere.
        /// </summary>
        void Populate(Rng floorRng)
        {
            int startRoom = Map.RoomIndexAt(Map.Start);
            int enemyCount = Math.Min(Config.MaxEnemies, Config.EnemiesOnFirstFloor + (Floor - 1) * Config.ExtraEnemiesPerFloor);
            if (Config.PackSizeMax <= 1)
            {
                for (int i = 0; i < enemyCount; i++)
                    if (TryFindSpawnTile(floorRng, room => room != startRoom, 4, out var pos)) SpawnEnemy(pos);
            }
            else
            {
                for (int placed = 0; placed < enemyCount;)
                {
                    int size = Math.Min(enemyCount - placed, floorRng.Range(Config.PackSizeMin, Config.PackSizeMax + 1));
                    placed += size;
                    if (!TryFindSpawnTile(floorRng, room => room != startRoom, 4, out var center)) continue;
                    bool fast = Config.FastEnemy != null && floorRng.Chance(Config.FastEnemyPercent);
                    for (int i = 0; i < size; i++)
                    {
                        if (!TryFindFreeTileNear(center, out var pos, avoidStairs: true)) break;
                        SpawnEnemy(pos, fast && i == 0 ? Config.FastEnemy : null);
                    }
                }
            }

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
        /// at least <paramref name="minDistance"/> from every party member on the floor.
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
                if (NearestPartyDistance(pos) < minDistance) continue;
                return true;
            }
            pos = default;
            return false;
        }

        int NearestPartyDistance(GridPos pos)
        {
            int nearest = int.MaxValue;
            foreach (var actor in actors)
                if (actor.Team == Team.Hero) nearest = Math.Min(nearest, GridPos.ChebyshevDistance(pos, actor.Pos));
            return nearest;
        }

        /// <summary>The free walkable tile closest to <paramref name="center"/>, searching outward ring by ring.</summary>
        bool TryFindFreeTileNear(GridPos center, out GridPos pos, bool avoidStairs = false)
        {
            for (int radius = 0; radius <= 4; radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                        pos = new GridPos(center.X + dx, center.Y + dy);
                        if (Map.IsWalkable(pos) && ActorAt(pos) == null && !(avoidStairs && pos == Map.Stairs)) return true;
                    }
                }
            }
            pos = default;
            return false;
        }
    }
}
