using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.CoreTests
{
    /// <summary>
    /// Minimal NUnit runner so the Core tests can run in seconds without opening Unity.
    /// Supports [Test] and [SetUp]; Unity's Test Runner remains the source of truth.
    /// </summary>
    static class Program
    {
        /// <summary>The party the reports play, leader first: the starting party, or with "-lead ID" that hero in front.</summary>
        static ActorDefinition[] Party = ActorCatalog.StartingParty;

        /// <summary>
        /// The builds the reports play, by hero id: "build=uzuki:hunter" puts that hero on another path than its
        /// default (<see cref="HeroBuilds"/>), "build=uzuki:marksman,hunter" names one per milestone. "build=none"
        /// leaves every point unspent: tier 1 of the hero's own class, as the reports played before there were builds.
        /// </summary>
        static readonly Dictionary<string, int[]> Builds = new Dictionary<string, int[]>();
        static bool SpendsPoints = true;

        /// <summary>
        /// "equip=uzuki:hunters_mark,crippling_shot,rolling_shot": the loadout that hero takes instead of its build's
        /// own, slot by slot, as far as it knows those skills.
        /// </summary>
        static readonly Dictionary<string, string[]> Loadouts = new Dictionary<string, string[]>();

        /// <summary>"level=N": the level the "fresh" runs of the balance report start at, to measure a build that needs points.</summary>
        static int StartLevel = 1;

        /// <summary>"-brief": the balance report in a few lines (wins, what was used, the campaign), to compare builds side by side.</summary>
        static bool Brief;

        /// <summary>"-fresh": the balance report without its campaign, which takes most of its time.</summary>
        static bool FreshOnly;

        /// <summary>
        /// Changes one number of a catalog skill for this run of the tool only (the reports' way to try a number, like
        /// the key=value overrides of <see cref="TuningFrom"/>): "skill.crippling_shot.DelayPercent=25". The game never
        /// does this: there a skill only changes on a hero's own copy.
        /// </summary>
        static bool TrySkillOverride(string arg)
        {
            var parts = arg.Split('=');
            var path = parts[0].Split('.');
            if (parts.Length != 2 || path.Length != 3 || !int.TryParse(parts[1], out int value)) return false;
            var skill = typeof(SkillCatalog).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(field => field.GetValue(null)).OfType<SkillDefinition>().FirstOrDefault(candidate => candidate.Id == path[1]);
            var property = typeof(SkillDefinition).GetProperty(path[2]);
            if (skill == null || property == null || property.PropertyType != typeof(int) || property.GetSetMethod(nonPublic: true) == null) return false;
            property.SetValue(skill, value);
            Console.WriteLine($"Skill override: {skill.Name}'s {property.Name} is {value}");
            return true;
        }

        /// <summary>A hero for a report: at <paramref name="level"/>, with its points spent on its build.</summary>
        static HeroProgress NewHero(ActorDefinition definition, int level)
        {
            var hero = new HeroProgress(definition, level);
            Spend(hero);
            return hero;
        }

        /// <summary>The party for a report, leader first, each hero with its build.</summary>
        static HeroProgress[] NewParty(int level = 1) => Party.Select(definition => NewHero(definition, level)).ToArray();

        /// <summary>Spends a hero's free points on its build: between the runs of a campaign, as a player would.</summary>
        static void Spend(HeroProgress hero)
        {
            if (SpendsPoints) HeroBuilds.Spend(hero, Builds.TryGetValue(hero.Definition.Id, out var paths) ? paths : null);
            if (Loadouts.TryGetValue(hero.Definition.Id, out var skills)) HeroBuilds.Equip(hero, skills);
        }

        /// <summary>A hero's build and loadout in a line, for the reports' headers.</summary>
        static string Describe(HeroProgress hero) => $"{HeroBuilds.Describe(hero)} [{string.Join(", ", hero.Kit.Skills.Select(skill => skill.ShortName))}]";

        static int Main(string[] args)
        {
            int leadIndex = Array.IndexOf(args, "-lead");
            if (leadIndex >= 0)
            {
                var leader = ActorCatalog.Find(args[leadIndex + 1]);
                Party = new[] { leader }.Concat(Party.Where(definition => definition != leader)).ToArray();
                args = args.Where((arg, index) => index != leadIndex && index != leadIndex + 1).ToArray();
            }
            // "seeds=N" runs the reports over more (or fewer) seeds than the usual 200, for a steadier number.
            int seeds = 200;
            foreach (string arg in args)
                if (arg.StartsWith("seeds=")) seeds = int.Parse(arg.Substring(6));
            args = args.Where(arg => !arg.StartsWith("seeds=")).ToArray();
            // "map=N" draws the floor around the leader for the first N actions of a party trace.
            int mapActions = 0;
            foreach (string arg in args)
                if (arg.StartsWith("map=")) mapActions = int.Parse(arg.Substring(4));
            args = args.Where(arg => !arg.StartsWith("map=")).ToArray();
            foreach (string arg in args)
            {
                if (arg.StartsWith("level=")) StartLevel = int.Parse(arg.Substring(6));
                if (arg.StartsWith("equip="))
                {
                    var parts = arg.Substring(6).Split(':');
                    if (parts.Length != 2 || ActorCatalog.Find(parts[0]) == null)
                    {
                        Console.WriteLine($"{arg}: write equip=uzuki:hunters_mark,power_shot,rolling_shot");
                        return 2;
                    }
                    Loadouts[parts[0]] = parts[1].Split(',');
                }
                if (arg == "build=none") SpendsPoints = false;
                if (!arg.StartsWith("build=") || arg == "build=none") continue;
                if (!HeroBuilds.TryParse(arg.Substring(6), out var hero, out var paths, out string error))
                {
                    Console.WriteLine($"{arg}: {error}");
                    return 2;
                }
                Builds[hero.Id] = paths;
            }
            args = args.Where(arg => !arg.StartsWith("build=") && !arg.StartsWith("level=") && !arg.StartsWith("equip=")).ToArray();
            // "skill.bouncing_shot.Power=200": a catalog skill with one number changed, to try it before it goes in the catalog.
            foreach (string arg in args.Where(arg => arg.StartsWith("skill.")))
            {
                if (TrySkillOverride(arg)) continue;
                Console.WriteLine($"{arg}: write skill.<skill id>.<Property>=<number>, e.g. skill.bouncing_shot.Power=200");
                return 2;
            }
            args = args.Where(arg => !arg.StartsWith("skill.")).ToArray();
            Brief = args.Contains("-brief");
            FreshOnly = args.Contains("-fresh");
            args = args.Where(arg => arg != "-brief" && arg != "-fresh").ToArray();
            if (args.Contains("-balance")) return BalanceReport(seeds, TuningFrom(args));
            if (args.Contains("-spread")) return SpreadReport(seeds, TuningFrom(args));
            int bossIndex = Array.IndexOf(args, "-boss");
            if (bossIndex >= 0) return BossReport(int.Parse(args[bossIndex + 1]), seeds, TuningFrom(args));
            int mapIndex = Array.IndexOf(args, "-map");
            if (mapIndex >= 0) return PrintFloor(int.Parse(args[mapIndex + 1]));
            int partyIndex = Array.IndexOf(args, "-party");
            if (partyIndex >= 0)
            {
                bool from = partyIndex + 2 < args.Length && !args[partyIndex + 2].Contains('=');
                return TraceParty(int.Parse(args[partyIndex + 1]), from ? int.Parse(args[partyIndex + 2]) : int.MaxValue, mapActions, TuningFrom(args));
            }
            int traceIndex = Array.IndexOf(args, "-trace");
            if (traceIndex >= 0) return TraceAutopilot(int.Parse(args[traceIndex + 1]), int.Parse(args[traceIndex + 2]));
            if (!AssertsThrow()) return 2;

            string filter = args.FirstOrDefault(a => !a.StartsWith("-"));
            int passed = 0, failed = 0;
            var clock = Stopwatch.StartNew();

            foreach (var type in typeof(Program).Assembly.GetTypes().OrderBy(t => t.FullName))
            {
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                var tests = methods.Where(m => m.GetCustomAttribute<TestAttribute>() != null).ToList();
                if (tests.Count == 0) continue;
                var setUps = methods.Where(m => m.GetCustomAttribute<SetUpAttribute>() != null).ToList();

                foreach (var test in tests)
                {
                    string name = $"{type.Name}.{test.Name}";
                    if (filter != null && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    try
                    {
                        object instance = Activator.CreateInstance(type);
                        foreach (var setUp in setUps) setUp.Invoke(instance, null);
                        test.Invoke(instance, null);
                        passed++;
                        Console.WriteLine($"  pass  {name}");
                    }
                    catch (TargetInvocationException e)
                    {
                        failed++;
                        Console.WriteLine($"  FAIL  {name}\n        {e.InnerException?.Message?.Trim().Replace("\n", "\n        ")}");
                    }
                }
            }

            Console.WriteLine($"\n{passed} passed, {failed} failed in {clock.Elapsed.TotalSeconds:0.0}s");
            return failed == 0 ? 0 : 1;
        }

        /// <summary>Guards against a silently broken runner: a failing assert must throw outside NUnit's engine too.</summary>
        static bool AssertsThrow()
        {
            try
            {
                Assert.AreEqual(1, 2);
            }
            catch (AssertionException)
            {
                return true;
            }
            Console.WriteLine("NUnit asserts did not throw; test results would be meaningless.");
            return false;
        }

        static int PrintFloor(int seed)
        {
            var run = new DungeonRun(seed);
            var text = run.Map.ToAscii().ToCharArray();
            int width = run.Map.Width + 1;
            foreach (var actor in run.Actors)
                if (actor != run.Hero) text[(run.Map.Height - 1 - actor.Pos.Y) * width + actor.Pos.X] = 's';
            foreach (var item in run.Items) text[(run.Map.Height - 1 - item.Pos.Y) * width + item.Pos.X] = 'b';
            Console.WriteLine($"Seed {seed}, floor 1: {run.Map.Rooms.Count} rooms, {run.Actors.Count - 1} enemies, {run.Items.Count} berries");
            Console.Write(new string(text));
            return 0;
        }

        /// <summary>Replays an autopilot run and prints each command from <paramref name="fromAction"/> on, for debugging.</summary>
        static int TraceAutopilot(int seed, int fromAction)
        {
            var run = new DungeonRun(seed);
            int actions = 0;
            for (; actions < fromAction + 20 && run.State == RunState.InProgress; actions++)
            {
                var command = AutoPilot.Decide(run);
                var before = run.Hero.Pos;
                bool used = run.Execute(command);
                if (actions >= fromAction)
                    Console.WriteLine($"{actions,5} B{run.Floor}F {command,-10} {before} -> {run.Hero.Pos} used={used} " +
                                      $"berries={run.Berries} items=[{string.Join(" ", run.Items.Select(it => it.Pos))}]");
            }
            Console.WriteLine($"After {actions} actions: {run.State} on B{run.Floor}F, Lv {run.Hero.Level}, HP {run.Hero.Hp}/{run.Hero.MaxHp}, turn {run.Turn}");
            return 0;
        }

        /// <summary>
        /// The dungeon config for the balance report: the defaults, or with key=value overrides from the command line to
        /// try numbers before putting them in the catalog (spiderHp, spiderAtk, spiderDef, spiderHpGrowth, spiderAtkGrowth,
        /// spiderDefGrowth, spiderExp, spiderExpGrowth, bossHp, bossAtk, bossDef, bossExp, enemies, extraEnemies, maxEnemies,
        /// berries, berryHeal, packMin, packMax, fastPercent).
        /// </summary>
        static Func<DungeonRunConfig> TuningFrom(string[] args)
        {
            var values = args.Where(a => a.Contains('=')).Select(a => a.Split('=')).ToDictionary(kv => kv[0], kv => int.Parse(kv[1]));
            if (values.Count == 0) return () => new DungeonRunConfig();
            int Get(string key, int fallback) => values.TryGetValue(key, out int value) ? value : fallback;
            var spider = ActorCatalog.Spider;
            var boss = ActorCatalog.Troll;
            var enemy = new ActorDefinition(spider.Id, spider.Name, Get("spiderHp", spider.MaxHp), Get("spiderAtk", spider.Attack),
                Get("spiderDef", spider.Defense), Get("spiderExp", spider.ExpReward), hpGrowth: Get("spiderHpGrowth", spider.HpGrowth),
                atkGrowth: Get("spiderAtkGrowth", spider.AtkGrowth), defGrowth: Get("spiderDefGrowth", spider.DefGrowth),
                expGrowth: Get("spiderExpGrowth", spider.ExpGrowth));
            var king = new ActorDefinition(boss.Id, boss.Name, Get("bossHp", boss.MaxHp), Get("bossAtk", boss.Attack), Get("bossDef", boss.Defense),
                Get("bossExp", boss.ExpReward), brain: boss.Brain, speed: boss.Speed);
            Console.WriteLine("Overrides: " + string.Join(", ", values.Select(kv => $"{kv.Key}={kv.Value}")));
            return () =>
            {
                var config = new DungeonRunConfig { Enemy = enemy, Boss = king };
                config.EnemiesOnFirstFloor = Get("enemies", config.EnemiesOnFirstFloor);
                config.ExtraEnemiesPerFloor = Get("extraEnemies", config.ExtraEnemiesPerFloor);
                config.MaxEnemies = Get("maxEnemies", config.MaxEnemies);
                config.ItemsPerFloor = Get("berries", config.ItemsPerFloor);
                config.BerryHealHp = Get("berryHeal", config.BerryHealHp);
                config.PackSizeMin = Get("packMin", config.PackSizeMin);
                config.PackSizeMax = Get("packMax", config.PackSizeMax);
                config.FastEnemyPercent = Get("fastPercent", config.FastEnemyPercent);
                return config;
            };
        }

        /// <summary>
        /// The floor around the leader: '#' walls, ',' corridors and doorways (<see cref="DungeonMap.IsNarrow"/>), '.' open
        /// floor, the heroes by their initial, foes as 's' (after the party), 'z' (hasn't noticed it), 'b' (a bat) or 'T'.
        /// </summary>
        static void PrintAround(DungeonRun run, int halfWidth, int halfHeight)
        {
            var center = run.Hero.Pos;
            for (int y = center.Y + halfHeight; y >= center.Y - halfHeight; y--)
            {
                var line = new System.Text.StringBuilder("        ");
                for (int x = center.X - halfWidth; x <= center.X + halfWidth; x++)
                {
                    var pos = new GridPos(x, y);
                    var actor = run.ActorAt(pos);
                    char c = !run.Map.IsWalkable(pos) ? '#' : pos == run.Map.Stairs ? '>' : run.Map.IsNarrow(pos) ? ',' : '.';
                    if (actor != null)
                        c = actor.Team == Team.Hero ? actor.Name[0]
                            : actor.Definition.IsBoss ? 'T'
                            : actor.Definition == ActorCatalog.Bat ? 'b'
                            : actor.Alerted ? 's' : 'z';
                    line.Append(c);
                }
                Console.WriteLine(line);
            }
        }

        /// <summary>
        /// Plays the starting party with the autopilot on a seed, as the game does, printing one line per floor and, from
        /// action <paramref name="fromAction"/> on, every action with where everyone stands, where the foes that are after
        /// the party are, and who swapped places (by actor id); for the first <paramref name="mapActions"/> of those, the
        /// floor around the leader too.
        /// </summary>
        static int TraceParty(int seed, int fromAction, int mapActions, Func<DungeonRunConfig> tuning)
        {
            var run = new DungeonRun(seed, tuning(), NewParty(StartLevel));
            int floor = 0;
            for (int action = 0; action < 3000 && run.State == RunState.InProgress; action++)
            {
                if (run.Floor != floor)
                {
                    floor = run.Floor;
                    Console.WriteLine($"{action,5} B{floor}F, stairs {run.Map.Stairs}");
                }
                var command = AutoPilot.Decide(run);
                bool used = run.Execute(command);
                if (action >= fromAction)
                    Console.WriteLine($"{action,5} {command,-18} used={used} combat={run.InCombat} " +
                                      string.Join(" ", run.Party.Select(m => $"{m.Name[0]}{m.Pos}{(m == run.Hero ? "*" : "")} {(m.IsAlive ? m.Hp * 100 / m.MaxHp + "%" : "down")}")) +
                                      $" foes={run.Actors.Count(a => a.Team == Team.Enemy)} after us: " +
                                      string.Join(" ", run.Actors.Where(a => a.Team == Team.Enemy && a.Alerted).Select(a => a.Pos)) +
                                      string.Concat(run.Events.OfType<SwappedEvent>().Select(swap => $" swap {swap.ActorId}<>{swap.OtherId}")) +
                                      (run.IsBossFloor ? BossFightTrail(run) : ""));
                if (action >= fromAction && action - fromAction < mapActions) PrintAround(run, 9, 6);
            }
            Console.WriteLine($"{run.State} on B{run.Floor}F after turn {run.Turn}");
            return 0;
        }

        /// <summary>
        /// For a party trace on the boss floor: where the boss stands, and what happened in this action in order: who
        /// moved where, who used what, the boss's wind-ups and slams, and how each hero a slam caught was standing.
        /// </summary>
        static string BossFightTrail(DungeonRun run)
        {
            string Name(int actorId)
            {
                foreach (var member in run.Party)
                    if (member.Id == actorId) return member.Name.Substring(0, 1);
                return run.Boss != null && run.Boss.Id == actorId ? "T" : "s";
            }

            var trail = new List<string>();
            foreach (var e in run.Events)
                switch (e)
                {
                    case MovedEvent moved: trail.Add($"{Name(moved.ActorId)}>{moved.To}"); break;
                    case DashedEvent dashed: trail.Add($"{Name(dashed.ActorId)}>>{dashed.To}"); break;
                    case SkillUsedEvent used: trail.Add($"{Name(used.ActorId)}:{used.Skill.Name}"); break;
                    case AttackEvent attack when attack.TargetId >= 0: trail.Add($"{Name(attack.AttackerId)}x{Name(attack.TargetId)}"); break;
                    case BossActionEvent boss: trail.Add($"T:{boss.Action}"); break;
                    case SlamCaughtEvent caught: trail.Add($"caught {Name(caught.TargetId)} ({caught.Footing}{(caught.Braced ? ", braced" : "")})"); break;
                    case DiedEvent died: trail.Add($"{Name(died.ActorId)} falls"); break;
                }
            var troll = run.Boss;
            return troll == null ? "" : $" | T{troll.Pos} {troll.Hp * 100 / troll.MaxHp}%{(troll.Charging ? " winding up" : "")}: {string.Join(", ", trail)}";
        }

        /// <summary>
        /// How well the party stays together: after every action of the leader, how many steps each partner would have
        /// to walk to reach it (walls count, other actors don't), in fights and while exploring. Lists the moments a
        /// partner was farthest off, to look at with "-party".
        /// </summary>
        static int SpreadReport(int seeds, Func<DungeonRunConfig> tuning)
        {
            const int Far = 4, Unreached = 999;
            var fights = new Spread();
            var exploring = new Spread();
            var worst = new List<(int Steps, string Where)>();
            for (int seed = 1; seed <= seeds; seed++)
            {
                var run = new DungeonRun(seed, tuning(), NewParty(StartLevel));
                (int Steps, string Where) farthest = (0, null);
                for (int action = 0; action < 5000 && run.State == RunState.InProgress; action++)
                {
                    run.Execute(AutoPilot.Decide(run));
                    if (run.State != RunState.InProgress) break;
                    var fromLeader = Pathfinder.StepsFrom(run.Map, run.Hero.Pos, Unreached);
                    foreach (var member in run.Party)
                    {
                        if (member == run.Hero || !member.IsAlive) continue;
                        int steps = fromLeader[member.Pos.Y * run.Map.Width + member.Pos.X];
                        if (steps < 0) steps = Unreached;
                        (run.InCombat ? fights : exploring).Add(steps, Far);
                        if (run.InCombat && steps > farthest.Steps)
                            farthest = (steps, $"seed {seed} action {action} B{run.Floor}F: {member.Name} {steps} steps from the leader");
                    }
                }
                if (farthest.Where != null) worst.Add(farthest);
            }
            string names = string.Join(", ", Party.Select(definition => definition.Name));
            Console.WriteLine($"Partners' walking distance to the leader ({names}; the first leads), autopilot over {seeds} seeds:");
            Console.WriteLine($"  in fights: {fights.Describe(Far)}");
            Console.WriteLine($"  exploring: {exploring.Describe(Far)}");
            Console.WriteLine("Farthest in a fight:");
            foreach (var entry in worst.OrderByDescending(entry => entry.Steps).Take(8)) Console.WriteLine("  " + entry.Where);
            return 0;
        }

        /// <summary>Running totals of the partners' distances to the leader, for <see cref="SpreadReport"/>.</summary>
        sealed class Spread
        {
            long samples, steps, far;
            int most;

            public void Add(int distance, int farFrom)
            {
                samples++;
                steps += distance;
                if (distance > farFrom) far++;
                most = Math.Max(most, distance);
            }

            public string Describe(int farFrom) =>
                $"{steps / (float)Math.Max(1, samples):0.00} steps on average, more than {farFrom} steps away " +
                $"{far * 100f / Math.Max(1, samples):0.0}% of the time, at most {most}";
        }

        /// <summary>How far the autopilot gets with the default tuning and the starting party; a sanity check after balance changes.</summary>
        static int BalanceReport(int seeds, Func<DungeonRunConfig> tuning)
        {
            int won = 0, lost = 0, stalled = 0, floorSum = 0, levelSum = 0, turnSum = 0, fallenSum = 0;
            int fights = 0, bossFights = 0, ultimates = 0, bossUltimates = 0, swaps = 0, safetySwaps = 0;
            long charge = 0, bossCharge = 0;
            var floorsReached = new int[new DungeonRunConfig().FloorCount + 1];
            var standingAtBoss = new int[Party.Length];
            int reachedBoss = 0;
            int holds = 0, frontSwaps = 0, fallenEarly = 0, fallenBesideHelp = 0;
            int rests = 0, restHeals = 0;
            var skillUses = new Dictionary<string, int>();
            int counters = 0;
            var hpAtFightStart = new long[Party.Length];
            var hpAtBossStart = new long[Party.Length];
            var packFights = new FightStats(Party.Length);
            var bossFight = new FightStats(Party.Length);
            var footing = new SlamStats(Party.Length);
            // The second yardstick (how fast the party fights): the boss fight's rounds in the runs that were won, and
            // what the boss loses per round of its fight, won or lost.
            long wonBossRounds = 0, bossDamage = 0;
            int bossMaxHp = 0;
            for (int seed = 1; seed <= seeds; seed++)
            {
                var run = new DungeonRun(seed, tuning(), NewParty(StartLevel));
                footing.BeginRun();
                long bossRoundsBefore = bossFight.Rounds;
                Actor boss = null;
                for (int i = 0; i < 5000 && run.State == RunState.InProgress; i++)
                {
                    bool wasBossFloor = run.IsBossFloor;
                    boss ??= run.Boss;
                    var round = (wasBossFloor ? bossFight : packFights).Begin(run);
                    bool fighting = run.InCombat;
                    var command = AutoPilot.Decide(run);
                    if (command.Holding) holds++;
                    if (command.Resting) rests++;
                    round.End(run, run.Execute(command));
                    footing.After(run);
                    // Heals used between fights: by the leader's own action, or by partners before a fight (re)started.
                    bool quiet = !fighting;
                    foreach (var e in run.Events)
                    {
                        if (e is CombatStartedEvent) quiet = false;
                        else if (e is CombatEndedEvent) quiet = true;
                        else if (quiet && e is SkillUsedEvent heal && heal.Skill.Effect == SkillEffect.Heal) restHeals++;
                    }
                    // Heroes that fall before the boss, and how many of them fell while a melee ally with most of its HP
                    // stood within two tiles with nothing to hit: the playtest's "the other two can't do anything useful".
                    if (!wasBossFloor)
                        foreach (var died in run.Events.OfType<DiedEvent>())
                        {
                            var victim = run.Party.FirstOrDefault(member => member.Id == died.ActorId);
                            if (victim == null) continue;
                            fallenEarly++;
                            if (run.Party.Any(member => member != victim && member.IsAlive && !member.Definition.IsRanged &&
                                                        GridPos.ChebyshevDistance(member.Pos, victim.Pos) <= 2 &&
                                                        member.Hp * 100 >= member.MaxHp * 60 && !run.InMelee(member)))
                                fallenBesideHelp++;
                        }
                    if (run.IsBossFloor && !wasBossFloor)
                    {
                        reachedBoss++;
                        for (int member = 0; member < run.Party.Count; member++)
                            if (run.Party[member].IsAlive) standingAtBoss[member]++;
                    }
                    // What the heroes use: every skill and ultimate by name, and how often a counter stance is answered.
                    foreach (var e in run.Events)
                    {
                        if (e is CounterEvent) counters++;
                        if (!(e is SkillUsedEvent any)) continue;
                        skillUses.TryGetValue(any.Skill.Name, out int uses);
                        skillUses[any.Skill.Name] = uses + 1;
                    }
                    foreach (var e in run.Events)
                    {
                        if (e is CombatStartedEvent)
                        {
                            if (run.IsBossFloor) bossFights++;
                            else fights++;
                            // How much of its HP each hero brings into the fight (fallen heroes bring none).
                            for (int member = 0; member < run.Party.Count; member++)
                                (run.IsBossFloor ? hpAtBossStart : hpAtFightStart)[member] += run.Party[member].Hp * 100 / run.Party[member].MaxHp;
                        }
                        else if (e is SkillUsedEvent used && used.Skill.IsUltimate)
                        {
                            if (run.IsBossFloor) bossUltimates++;
                            else ultimates++;
                        }
                        else if (e is SwappedEvent swapped)
                        {
                            swaps++;
                            if (swapped.Reason == SwapReason.Safety) safetySwaps++;
                            else if (swapped.Reason == SwapReason.Rotate) frontSwaps++;
                        }
                        else if (e is ChargeChangedEvent changed && changed.Amount > 0)
                        {
                            if (run.IsBossFloor) bossCharge += changed.Amount;
                            else charge += changed.Amount;
                        }
                    }
                }
                if (boss != null)
                {
                    bossDamage += boss.MaxHp - boss.Hp;
                    bossMaxHp = boss.MaxHp;
                }
                if (run.State == RunState.Won) wonBossRounds += bossFight.Rounds - bossRoundsBefore;
                if (run.State == RunState.Won) won++;
                else if (run.State == RunState.Lost) lost++;
                else
                {
                    stalled++;
                    Console.WriteLine($"Stalled: seed {seed} on B{run.Floor}F, leader at {run.Hero.Pos}, stairs {run.Map.Stairs}, " +
                                      $"next command {AutoPilot.Decide(run)}, actors: {string.Join("; ", run.Actors)}");
                }
                floorSum += run.Floor;
                levelSum += run.Party.Max(member => member.Level); // A hero who fell misses EXP, so the party's best.
                turnSum += run.Turn;
                fallenSum += run.Party.Count(member => !member.IsAlive);
                floorsReached[run.Floor]++;
            }
            string names = string.Join(", ", Party.Select(definition => definition.Name));
            Console.WriteLine($"Builds at Lv {StartLevel}: {string.Join("; ", NewParty(StartLevel).Select(Describe))}");
            Console.WriteLine($"Fresh level-{StartLevel} party ({names}; the first leads), autopilot over {seeds} seeds: won {won}, lost {lost}, stalled {stalled}");
            Console.WriteLine($"Average: floor {floorSum / (float)seeds:0.0}, level {levelSum / (float)seeds:0.0}, turns {turnSum / (float)seeds:0}, " +
                              $"heroes fallen {fallenSum / (float)seeds:0.0} of 3");
            string tempo = $"How fast the party fights: pack fights of {packFights.Rounds / (float)Math.Max(1, fights):0.00} rounds of blows each; " +
                           $"the boss fight in the {won} runs won: {wonBossRounds / (float)Math.Max(1, won):0.0} rounds; in all its fights the boss lost " +
                           $"{bossDamage / (float)Math.Max(1, bossFight.Rounds):0} HP a round ({bossDamage * 100f / Math.Max(1, bossFight.Rounds) / Math.Max(1, bossMaxHp):0.00}% of its {bossMaxHp})";
            string skillsUsed = "Skills used a run: " +
                                string.Join(", ", skillUses.OrderByDescending(pair => pair.Value).Select(pair => $"{pair.Key} {pair.Value / (float)seeds:0.0}")) +
                                $"; a counter stance was answered {counters / (float)seeds:0.0} times a run";
            if (Brief)
            {
                Console.WriteLine($"Reached the boss floor: {reachedBoss}; heroes fallen before it: {fallenEarly}; ultimates per hero in the boss fight " +
                                  $"{bossUltimates / 3f / Math.Max(1, bossFights):0.00}");
                Console.WriteLine($"Before the boss: {fights / (float)seeds:0.0} fights a run of {packFights.Rounds / (float)Math.Max(1, fights):0.00} rounds of blows each, " +
                                  $"{packFights.HitsTaken / (float)seeds:0.0} enemy attackers-rounds on heroes a run; HP into the boss fight: " +
                                  string.Join(", ", Party.Select((definition, member) => $"{definition.Name} {hpAtBossStart[member] / Math.Max(1, bossFights)}%")) +
                                  $"; the boss fight: {bossFight.Rounds / (float)Math.Max(1, bossFights):0.0} rounds");
                Console.WriteLine(tempo);
                Console.WriteLine(skillsUsed);
                CampaignReport(players: seeds / 2, maxAttempts: 10, tuning);
                return 0;
            }
            Console.WriteLine(tempo);
            for (int f = 1; f < floorsReached.Length; f++) Console.WriteLine($"  ended on B{f}F: {floorsReached[f]}");
            Console.WriteLine($"Reached the boss floor: {reachedBoss}; still standing on arrival: " +
                              string.Join(", ", Party.Select((definition, member) => $"{definition.Name} {standingAtBoss[member] * 100 / Math.Max(1, reachedBoss)}%")));
            Console.WriteLine($"Fights: {fights / (float)seeds:0.0} a run before the boss; per hero per fight, ultimates {ultimates / 3f / Math.Max(1, fights):0.00} " +
                              $"(charge gained {charge / 3f / Math.Max(1, fights):0}), in the boss fight {bossUltimates / 3f / Math.Max(1, bossFights):0.00} " +
                              $"(charge {bossCharge / 3f / Math.Max(1, bossFights):0}); swaps {swaps / (float)seeds:0.0} a run " +
                              $"({safetySwaps / (float)seeds:0.00} of them a badly hurt hero running to safety)");
            Console.WriteLine($"Doorways and corridors: the leader held a doorway for {holds / (float)seeds:0.0} turns a run, and two melee heroes " +
                              $"rotated the front {frontSwaps / (float)seeds:0.0} times a run (the fresh one for the hurt one). Heroes fallen before the boss floor: " +
                              $"{fallenEarly} in {seeds} runs, {fallenBesideHelp} of them with a fresh melee ally idle within two tiles");
            Console.WriteLine($"Between fights: the leader waited {rests / (float)seeds:0.0} turns a run for the party to heal up, and {restHeals / (float)seeds:0.0} heals a run " +
                              "were used outside a fight. HP brought into a fight: " +
                              string.Join(", ", Party.Select((definition, member) => $"{definition.Name} {hpAtFightStart[member] / Math.Max(1, fights)}%")) +
                              "; into the boss fight: " +
                              string.Join(", ", Party.Select((definition, member) => $"{definition.Name} {hpAtBossStart[member] / Math.Max(1, bossFights)}%")));
            Console.WriteLine(skillsUsed);
            Console.WriteLine("In the fights before the boss (a round is one action of the leader's in which blows were exchanged):");
            packFights.Print(Party);
            Console.WriteLine("In the boss fight:");
            bossFight.Print(Party);
            footing.Print(Party);
            CampaignReport(players: seeds / 2, maxAttempts: 10, tuning);
            return 0;
        }

        /// <summary>
        /// What the heroes do in fights, for the balance report: per hero, how its turns split into attacking or using a
        /// skill, moving and waiting, and in how many rounds (one action of the leader's, so about one turn for
        /// everyone) three or more different enemies hit it. Read from the events of each action, so partners count too.
        /// </summary>
        sealed class FightStats
        {
            const int Crowd = 3;
            readonly long[] turns, acted, moved, rounds, crowded, hits;
            long fightRounds, crowdedRounds;

            public FightStats(int partySize)
            {
                turns = new long[partySize];
                acted = new long[partySize];
                moved = new long[partySize];
                rounds = new long[partySize];
                crowded = new long[partySize];
                hits = new long[partySize];
            }

            /// <summary>Rounds in which blows were exchanged, over all fights.</summary>
            public long Rounds => fightRounds;

            /// <summary>How many times a hero was attacked by a different enemy in a round, summed over the heroes and the rounds.</summary>
            public long HitsTaken => hits.Sum();

            /// <summary>Call before an action; <see cref="Round.End"/> after it. Rounds outside a fight count nothing.</summary>
            public Round Begin(DungeonRun run) => new Round(this, run);

            public readonly struct Round
            {
                readonly FightStats stats;
                readonly bool fighting;
                readonly int[] turnsBefore;
                readonly bool[] standing;
                readonly Actor leader;

                public Round(FightStats stats, DungeonRun run)
                {
                    this.stats = stats;
                    fighting = run.InCombat;
                    turnsBefore = run.Party.Select(member => member.TurnsTaken).ToArray();
                    standing = run.Party.Select(member => member.IsAlive).ToArray();
                    leader = run.Hero;
                }

                public void End(DungeonRun run, bool used)
                {
                    if (!fighting) return;
                    int size = run.Party.Count;
                    var acted = new int[size];
                    var moved = new int[size];
                    var attackers = new HashSet<int>[size];
                    for (int member = 0; member < size; member++) attackers[member] = new HashSet<int>();
                    int IndexOf(int actorId)
                    {
                        for (int member = 0; member < size; member++)
                            if (run.Party[member].Id == actorId) return member;
                        return -1;
                    }

                    // A skill's own swings and shots follow its SkillUsedEvent; an attack by anyone else ends them.
                    int skillUser = -1, swappedAway = -1;
                    GameEvent previous = null;
                    foreach (var e in run.Events)
                    {
                        switch (e)
                        {
                            case SkillUsedEvent skill:
                                skillUser = skill.ActorId;
                                if (IndexOf(skill.ActorId) >= 0) acted[IndexOf(skill.ActorId)]++;
                                break;
                            case ItemUsedEvent item:
                                if (IndexOf(item.ActorId) >= 0) acted[IndexOf(item.ActorId)]++;
                                break;
                            case AttackEvent attack:
                                int attacker = IndexOf(attack.AttackerId), target = IndexOf(attack.TargetId);
                                if (attacker < 0 && target >= 0) attackers[target].Add(attack.AttackerId);
                                if (attack.AttackerId == skillUser) break;
                                skillUser = -1;
                                if (attacker >= 0) acted[attacker]++;
                                break;
                            case SwappedEvent swap:
                                swappedAway = swap.OtherId; // Its move follows the mover's: not a step of its own.
                                break;
                            case MovedEvent move:
                                bool pushed = previous is PushedEvent push && push.ActorId == move.ActorId;
                                if (move.ActorId == swappedAway) swappedAway = -1;
                                else if (!pushed)
                                {
                                    if (move.ActorId != skillUser) skillUser = -1;
                                    if (IndexOf(move.ActorId) >= 0) moved[IndexOf(move.ActorId)]++;
                                }
                                break;
                            case BossActionEvent _:
                                skillUser = -1;
                                break;
                        }
                        previous = e;
                    }

                    // Rounds where the party only walks (the foes are still on their way, or it is chasing one) aren't the fight.
                    bool blows = false;
                    foreach (var e in run.Events)
                        if (e is AttackEvent blow && (IndexOf(blow.AttackerId) >= 0 || IndexOf(blow.TargetId) >= 0)) blows = true;
                    if (!blows) return;

                    bool anyCrowded = false;
                    for (int member = 0; member < size; member++)
                    {
                        if (!standing[member]) continue;
                        var hero = run.Party[member];
                        // The leader's turn began before this action. A partner's turns begin and end inside it, except
                        // the one that begins as it takes the lead (the leader fell), which is still to be played.
                        int taken = hero == leader ? (used ? 1 : 0) : hero.TurnsTaken - turnsBefore[member] - (hero == run.Hero ? 1 : 0);
                        taken = Math.Max(taken, acted[member] + moved[member]);
                        stats.turns[member] += taken;
                        stats.acted[member] += acted[member];
                        stats.moved[member] += moved[member];
                        stats.rounds[member]++;
                        stats.hits[member] += attackers[member].Count;
                        if (attackers[member].Count < Crowd) continue;
                        stats.crowded[member]++;
                        anyCrowded = true;
                    }
                    stats.fightRounds++;
                    if (anyCrowded) stats.crowdedRounds++;
                }
            }

            public void Print(ActorDefinition[] party)
            {
                if (fightRounds == 0)
                {
                    Console.WriteLine("  (no fights)");
                    return;
                }
                Console.WriteLine($"  rounds in which some hero was hit by {Crowd} or more enemies: {crowdedRounds * 100f / fightRounds:0.0}%");
                for (int member = 0; member < party.Length; member++)
                {
                    long all = Math.Max(1, turns[member]);
                    long waited = Math.Max(0, turns[member] - acted[member] - moved[member]);
                    Console.WriteLine($"  {party[member].Name,-9} hit by {Crowd}+ in {crowded[member] * 100f / Math.Max(1, rounds[member]):0.0}% of its rounds " +
                                      $"({hits[member] / (float)Math.Max(1, rounds[member]):0.00} enemies a round); turns: " +
                                      $"{acted[member] * 100f / all:0}% attacking or using a skill, {moved[member] * 100f / all:0}% moving, {waited * 100f / all:0}% waiting");
                }
            }
        }

        /// <summary>
        /// The starting party at one level, straight into the boss arena (as the PlayMode boss test and "-fk-floors 1
        /// -fk-level N" do): how often the autopilot wins.
        /// </summary>
        static int BossReport(int level, int seeds, Func<DungeonRunConfig> tuning)
        {
            int won = 0, ultimates = 0, turns = 0;
            var footing = new SlamStats(Party.Length);
            for (int seed = 1; seed <= seeds; seed++)
            {
                var party = NewParty(level);
                var config = tuning();
                config.FloorCount = 1;
                var run = new DungeonRun(seed, config, party);
                footing.BeginRun();
                for (int i = 0; i < 2000 && run.State == RunState.InProgress; i++)
                {
                    run.Execute(AutoPilot.Decide(run));
                    footing.After(run);
                    ultimates += run.Events.Count(e => e is SkillUsedEvent used && used.Skill.IsUltimate);
                }
                if (run.State == RunState.Won) won++;
                turns += run.Turn;
            }
            Console.WriteLine($"Builds: {string.Join("; ", NewParty(level).Select(Describe))}");
            Console.WriteLine($"Starting party at Lv {level} against the Troll: won {won} of {seeds} " +
                              $"(leader turns {turns / (float)seeds:0}, ultimates per hero {ultimates / 3f / seeds:0.00})");
            footing.Print(Party);
            return 0;
        }

        /// <summary>
        /// Footing in the boss fight (PROGRESSION.md, "Footing in a boss fight"), for the balance report: how many of
        /// the boss's slams hit a hero, and how many of those heroes had no way out when their last turn began (the
        /// rules say so with each <see cref="SlamCaughtEvent"/>). Next to it, how often a hero next to the boss stands on
        /// a tile with no way out at all, sampled after each action of the leader's; and what the heroes' turns went to
        /// while a slam was winding up: an attack or a skill, a step, or a plain wait out of its reach.
        /// </summary>
        sealed class SlamStats
        {
            readonly long[] caught, alone, cornered, corneredBraced, noTurn, cameBack, hpShare, felled, beside, besideCornered;
            readonly long[] windUpActs, windUpSteps, windUpWaits;
            readonly HashSet<int> pending = new HashSet<int>();
            int fights, windUps, slams, slamsThatHit, slamsOnCornered;
            bool fighting, slamHits, slamCorners, windingUp;

            public SlamStats(int partySize)
            {
                caught = new long[partySize];
                alone = new long[partySize];
                cornered = new long[partySize];
                corneredBraced = new long[partySize];
                noTurn = new long[partySize];
                cameBack = new long[partySize];
                hpShare = new long[partySize];
                felled = new long[partySize];
                beside = new long[partySize];
                besideCornered = new long[partySize];
                windUpActs = new long[partySize];
                windUpSteps = new long[partySize];
                windUpWaits = new long[partySize];
            }

            /// <summary>A boss fight is a run in which the boss winds up at least once.</summary>
            public void BeginRun()
            {
                fighting = slamHits = slamCorners = windingUp = false;
                pending.Clear();
            }

            /// <summary>Call after each action.</summary>
            public void After(DungeonRun run)
            {
                int IndexOf(int actorId)
                {
                    for (int member = 0; member < run.Party.Count; member++)
                        if (run.Party[member].Id == actorId) return member;
                    return -1;
                }

                // What the heroes do between a wind-up and its slam. A skill's own blows follow its SkillUsedEvent.
                int skillUser = -1, swappedAway = -1;
                foreach (var e in run.Events)
                {
                    switch (e)
                    {
                        case BossActionEvent boss:
                            windingUp = boss.Action == BossAction.Charge || windingUp && boss.Action != BossAction.Slam;
                            skillUser = -1;
                            break;
                        case SkillUsedEvent used when windingUp && IndexOf(used.ActorId) >= 0:
                            skillUser = used.ActorId;
                            windUpActs[IndexOf(used.ActorId)]++;
                            break;
                        case AttackEvent blow when windingUp && IndexOf(blow.AttackerId) >= 0:
                            if (blow.AttackerId != skillUser) windUpActs[IndexOf(blow.AttackerId)]++;
                            break;
                        case SwappedEvent swap:
                            swappedAway = swap.OtherId;
                            break;
                        case MovedEvent moved when windingUp && IndexOf(moved.ActorId) >= 0:
                            if (moved.ActorId == swappedAway) swappedAway = -1;
                            else if (moved.ActorId != skillUser) windUpSteps[IndexOf(moved.ActorId)]++;
                            break;
                        case HeroWaitedEvent waited when waited.Reason == WaitReason.KeepsClear && IndexOf(waited.ActorId) >= 0:
                            windUpWaits[IndexOf(waited.ActorId)]++;
                            break;
                    }
                }

                foreach (var e in run.Events)
                {
                    switch (e)
                    {
                        case SlamCaughtEvent hit when IndexOf(hit.TargetId) >= 0:
                            int target = IndexOf(hit.TargetId);
                            caught[target]++;
                            if (!run.Party.Any(member => member.IsAlive && member.Id != hit.TargetId)) alone[target]++;
                            slamHits = true;
                            if (hit.Footing == SlamFooting.NoTurn) noTurn[target]++;
                            if (hit.Footing == SlamFooting.OutOfReach) cameBack[target]++;
                            if (hit.Footing == SlamFooting.Cornered)
                            {
                                cornered[target]++;
                                slamCorners = true;
                                if (hit.Braced) corneredBraced[target]++;
                            }
                            pending.Add(hit.TargetId);
                            break;
                        case BossActionEvent boss when boss.Action == BossAction.Charge:
                            windUps++;
                            if (!fighting) fights++;
                            fighting = true;
                            break;
                        case BossActionEvent boss when boss.Action == BossAction.Slam:
                            slams++;
                            if (slamHits) slamsThatHit++;
                            if (slamCorners) slamsOnCornered++;
                            slamHits = slamCorners = false;
                            break;
                        // The slam's own hits follow it at once; a counter's blow in between lands on the boss.
                        case DamageEvent damage when pending.Remove(damage.TargetId):
                            int struck = IndexOf(damage.TargetId);
                            hpShare[struck] += damage.Amount * 100 / run.Party[struck].MaxHp;
                            if (damage.HpAfter == 0) felled[struck]++;
                            break;
                        case AttackEvent attack when IndexOf(attack.AttackerId) < 0:
                            pending.Clear();
                            break;
                    }
                }

                var troll = run.Boss;
                if (troll == null || !run.InCombat) return;
                for (int member = 0; member < run.Party.Count; member++)
                {
                    var hero = run.Party[member];
                    if (!hero.IsAlive || GridPos.ChebyshevDistance(hero.Pos, troll.Pos) > EnemyBrain.SlamRadius) continue;
                    beside[member]++;
                    if (!HeroTactics.HasWayOut(run, hero, troll)) besideCornered[member]++;
                }
            }

            public void Print(ActorDefinition[] party)
            {
                float per = Math.Max(1, fights);
                Console.WriteLine($"Footing in the boss fight ({fights} fights in which the boss wound up): {windUps} wind-ups, {slams} slams; " +
                                  $"{slamsThatHit} slams hit a hero ({slamsThatHit / per:0.00} a fight), {slamsOnCornered} of them a hero that had no way out " +
                                  $"({slamsOnCornered / per:0.00} a fight)");
                for (int member = 0; member < party.Length; member++)
                {
                    long hits = Math.Max(1, caught[member]);
                    long stayed = caught[member] - cornered[member] - noTurn[member] - cameBack[member];
                    Console.WriteLine($"  {party[member].Name,-9} hit by {caught[member]} slams ({caught[member] / per:0.00} a fight, {alone[member]} as the last one standing): {cornered[member]} with no way out " +
                                      $"({cornered[member] / per:0.00} a fight; {corneredBraced[member]} of them behind a guard, a stance or an aura), " +
                                      $"{noTurn[member]} without a turn since the wind-up, {cameBack[member]} that began its last turn out of reach " +
                                      $"and came in, {stayed} that had a way out and stayed; a hit took {hpShare[member] / hits}% of its HP " +
                                      $"and felled it {felled[member]} times; next to the boss it had no way out in " +
                                      $"{besideCornered[member] * 100f / Math.Max(1, beside[member]):0.0}% of {beside[member]} rounds");
                }
                Console.WriteLine("  While a slam winds up, a fight: " + string.Join("; ", party.Select((definition, member) =>
                    $"{definition.Name} {windUpActs[member] / per:0.0} attacks or skills, {windUpSteps[member] / per:0.0} steps, " +
                    $"{windUpWaits[member] / per:0.0} waits out of its reach")));
            }
        }

        /// <summary>Levels carried between runs, as in the real game: how many attempts until the party's first clear.</summary>
        static void CampaignReport(int players, int maxAttempts, Func<DungeonRunConfig> tuning)
        {
            if (FreshOnly) return;
            int cleared = 0, attemptsSum = 0, levelSum = 0;
            var clearedOnAttempt = new int[maxAttempts + 1];
            var tierSum = new long[Party.Length];
            for (int player = 1; player <= players; player++)
            {
                var party = NewParty();
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    // Between runs each hero spends the points its new levels brought, on its build.
                    foreach (var hero in party) Spend(hero);
                    var run = new DungeonRun(player * 1000 + attempt, tuning(), party);
                    for (int i = 0; i < 5000 && run.State == RunState.InProgress; i++) run.Execute(AutoPilot.Decide(run));
                    if (run.State != RunState.Won) continue;
                    cleared++;
                    attemptsSum += attempt;
                    levelSum += party.Max(hero => hero.Level);
                    clearedOnAttempt[attempt]++;
                    for (int member = 0; member < party.Length; member++)
                        tierSum[member] += party[member].TierOf(party[member].Definition.StartingClass);
                    break;
                }
            }
            Console.WriteLine();
            Console.WriteLine($"Levels kept between runs, {players} autopilot parties, up to {maxAttempts} attempts each:");
            Console.WriteLine($"  {cleared} beat the Troll; on average on attempt {attemptsSum / (float)Math.Max(1, cleared):0.0}, at Lv {levelSum / (float)Math.Max(1, cleared):0.0}");
            Console.WriteLine("  points spent between runs, each hero on its own class; its tier in the winning run: " +
                              string.Join(", ", Party.Select((definition, member) =>
                                  $"{definition.Name} {definition.StartingClass.Name} {tierSum[member] / (float)Math.Max(1, cleared):0.0}")));
            for (int a = 1; a <= maxAttempts; a++)
                if (clearedOnAttempt[a] > 0) Console.WriteLine($"  first clear on attempt {a}: {clearedOnAttempt[a]}");
        }
    }
}
