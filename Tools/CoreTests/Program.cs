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
            if (args.Contains("-balance")) return BalanceReport(seeds, TuningFrom(args));
            if (args.Contains("-spread")) return SpreadReport(seeds, TuningFrom(args));
            int bossIndex = Array.IndexOf(args, "-boss");
            if (bossIndex >= 0) return BossReport(int.Parse(args[bossIndex + 1]), seeds, TuningFrom(args));
            int mapIndex = Array.IndexOf(args, "-map");
            if (mapIndex >= 0) return PrintFloor(int.Parse(args[mapIndex + 1]));
            int partyIndex = Array.IndexOf(args, "-party");
            if (partyIndex >= 0) return TraceParty(int.Parse(args[partyIndex + 1]), partyIndex + 2 < args.Length ? int.Parse(args[partyIndex + 2]) : int.MaxValue);
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
        /// try numbers before putting them in the catalog (slimeHp, slimeAtk, slimeDef, slimeHpGrowth, slimeAtkGrowth,
        /// slimeDefGrowth, slimeExp, slimeExpGrowth, bossHp, bossAtk, bossDef, bossExp, enemies, extraEnemies, maxEnemies,
        /// berries, berryHeal, packMin, packMax, fastPercent).
        /// </summary>
        static Func<DungeonRunConfig> TuningFrom(string[] args)
        {
            var values = args.Where(a => a.Contains('=')).Select(a => a.Split('=')).ToDictionary(kv => kv[0], kv => int.Parse(kv[1]));
            if (values.Count == 0) return () => new DungeonRunConfig();
            int Get(string key, int fallback) => values.TryGetValue(key, out int value) ? value : fallback;
            var slime = ActorCatalog.Slime;
            var boss = ActorCatalog.KingSlime;
            var enemy = new ActorDefinition(slime.Id, slime.Name, Get("slimeHp", slime.MaxHp), Get("slimeAtk", slime.Attack),
                Get("slimeDef", slime.Defense), Get("slimeExp", slime.ExpReward), hpGrowth: Get("slimeHpGrowth", slime.HpGrowth),
                atkGrowth: Get("slimeAtkGrowth", slime.AtkGrowth), defGrowth: Get("slimeDefGrowth", slime.DefGrowth),
                expGrowth: Get("slimeExpGrowth", slime.ExpGrowth));
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
        /// Plays the starting party with the autopilot on a seed, as the game does, printing one line per floor and, from
        /// action <paramref name="fromAction"/> on, every action with where everyone stands, where the foes that are after
        /// the party are, and who swapped places (by actor id).
        /// </summary>
        static int TraceParty(int seed, int fromAction)
        {
            var run = new DungeonRun(seed, new DungeonRunConfig(), Party.Select(d => new HeroProgress(d)).ToArray());
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
                                      string.Concat(run.Events.OfType<SwappedEvent>().Select(swap => $" swap {swap.ActorId}<>{swap.OtherId}")));
            }
            Console.WriteLine($"{run.State} on B{run.Floor}F after turn {run.Turn}");
            return 0;
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
                var config = tuning();
                config.Party = Party;
                var run = new DungeonRun(seed, config);
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
            for (int seed = 1; seed <= seeds; seed++)
            {
                var config = tuning();
                config.Party = Party;
                var run = new DungeonRun(seed, config);
                for (int i = 0; i < 5000 && run.State == RunState.InProgress; i++)
                {
                    bool wasBossFloor = run.IsBossFloor;
                    // Who might run to safety during this action: badly hurt, with a foe next to them.
                    var hurt = run.Party.Where(member => member.IsAlive && DungeonRun.IsBadlyHurt(member) && run.FoeAdjacent(member))
                        .Select(member => member.Id).ToList();
                    run.Execute(AutoPilot.Decide(run));
                    if (run.IsBossFloor && !wasBossFloor)
                    {
                        reachedBoss++;
                        for (int member = 0; member < run.Party.Count; member++)
                            if (run.Party[member].IsAlive) standingAtBoss[member]++;
                    }
                    foreach (var e in run.Events)
                    {
                        if (e is CombatStartedEvent)
                        {
                            if (run.IsBossFloor) bossFights++;
                            else fights++;
                        }
                        else if (e is SkillUsedEvent used && used.Skill.IsUltimate)
                        {
                            if (run.IsBossFloor) bossUltimates++;
                            else ultimates++;
                        }
                        else if (e is SwappedEvent swapped)
                        {
                            swaps++;
                            if (hurt.Contains(swapped.ActorId)) safetySwaps++;
                        }
                        else if (e is ChargeChangedEvent changed && changed.Amount > 0)
                        {
                            if (run.IsBossFloor) bossCharge += changed.Amount;
                            else charge += changed.Amount;
                        }
                    }
                }
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
            Console.WriteLine($"Fresh level-1 party ({names}; the first leads), autopilot over {seeds} seeds: won {won}, lost {lost}, stalled {stalled}");
            Console.WriteLine($"Average: floor {floorSum / (float)seeds:0.0}, level {levelSum / (float)seeds:0.0}, turns {turnSum / (float)seeds:0}, " +
                              $"heroes fallen {fallenSum / (float)seeds:0.0} of 3");
            for (int f = 1; f < floorsReached.Length; f++) Console.WriteLine($"  ended on B{f}F: {floorsReached[f]}");
            Console.WriteLine($"Reached the boss floor: {reachedBoss}; still standing on arrival: " +
                              string.Join(", ", Party.Select((definition, member) => $"{definition.Name} {standingAtBoss[member] * 100 / Math.Max(1, reachedBoss)}%")));
            Console.WriteLine($"Fights: {fights / (float)seeds:0.0} a run before the boss; per hero per fight, ultimates {ultimates / 3f / Math.Max(1, fights):0.00} " +
                              $"(charge gained {charge / 3f / Math.Max(1, fights):0}), in the boss fight {bossUltimates / 3f / Math.Max(1, bossFights):0.00} " +
                              $"(charge {bossCharge / 3f / Math.Max(1, bossFights):0}); swaps {swaps / (float)seeds:0.0} a run " +
                              $"({safetySwaps / (float)seeds:0.00} of them a badly hurt hero running to safety)");
            CampaignReport(players: seeds / 2, maxAttempts: 10, tuning);
            return 0;
        }

        /// <summary>
        /// The starting party at one level, straight into the boss arena (as the PlayMode boss test and "-fk-floors 1
        /// -fk-level N" do): how often the autopilot wins.
        /// </summary>
        static int BossReport(int level, int seeds, Func<DungeonRunConfig> tuning)
        {
            int won = 0, ultimates = 0, turns = 0;
            for (int seed = 1; seed <= seeds; seed++)
            {
                var party = Party.Select(definition => new HeroProgress(definition, level)).ToArray();
                var config = tuning();
                config.FloorCount = 1;
                var run = new DungeonRun(seed, config, party);
                for (int i = 0; i < 2000 && run.State == RunState.InProgress; i++)
                {
                    run.Execute(AutoPilot.Decide(run));
                    ultimates += run.Events.Count(e => e is SkillUsedEvent used && used.Skill.IsUltimate);
                }
                if (run.State == RunState.Won) won++;
                turns += run.Turn;
            }
            Console.WriteLine($"Starting party at Lv {level} against the King Slime: won {won} of {seeds} " +
                              $"(leader turns {turns / (float)seeds:0}, ultimates per hero {ultimates / 3f / seeds:0.00})");
            return 0;
        }

        /// <summary>Levels carried between runs, as in the real game: how many attempts until the party's first clear.</summary>
        static void CampaignReport(int players, int maxAttempts, Func<DungeonRunConfig> tuning)
        {
            int cleared = 0, attemptsSum = 0, levelSum = 0;
            var clearedOnAttempt = new int[maxAttempts + 1];
            for (int player = 1; player <= players; player++)
            {
                var party = Party.Select(definition => new HeroProgress(definition)).ToArray();
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    var run = new DungeonRun(player * 1000 + attempt, tuning(), party);
                    for (int i = 0; i < 5000 && run.State == RunState.InProgress; i++) run.Execute(AutoPilot.Decide(run));
                    if (run.State != RunState.Won) continue;
                    cleared++;
                    attemptsSum += attempt;
                    levelSum += party.Max(hero => hero.Level);
                    clearedOnAttempt[attempt]++;
                    break;
                }
            }
            Console.WriteLine();
            Console.WriteLine($"Levels kept between runs, {players} autopilot parties, up to {maxAttempts} attempts each:");
            Console.WriteLine($"  {cleared} beat the King Slime; on average on attempt {attemptsSum / (float)Math.Max(1, cleared):0.0}, at Lv {levelSum / (float)Math.Max(1, cleared):0.0}");
            for (int a = 1; a <= maxAttempts; a++)
                if (clearedOnAttempt[a] > 0) Console.WriteLine($"  first clear on attempt {a}: {clearedOnAttempt[a]}");
        }
    }
}
