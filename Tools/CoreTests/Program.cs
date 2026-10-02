using System;
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
        static int Main(string[] args)
        {
            if (args.Contains("-balance")) return BalanceReport(seeds: 200);
            int mapIndex = Array.IndexOf(args, "-map");
            if (mapIndex >= 0) return PrintFloor(int.Parse(args[mapIndex + 1]));
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

        /// <summary>How far the autopilot gets with the default tuning; a quick sanity check after balance changes.</summary>
        static int BalanceReport(int seeds)
        {
            int won = 0, lost = 0, stalled = 0, floorSum = 0, levelSum = 0, turnSum = 0;
            var floorsReached = new int[new DungeonRunConfig().FloorCount + 1];
            for (int seed = 1; seed <= seeds; seed++)
            {
                var run = new DungeonRun(seed);
                for (int i = 0; i < 5000 && run.State == RunState.InProgress; i++) run.Execute(AutoPilot.Decide(run));
                if (run.State == RunState.Won) won++;
                else if (run.State == RunState.Lost) lost++;
                else
                {
                    stalled++;
                    Console.WriteLine($"Stalled: seed {seed} on B{run.Floor}F, hero at {run.Hero.Pos}, stairs {run.Map.Stairs}, " +
                                      $"next command {AutoPilot.Decide(run)}, actors: {string.Join("; ", run.Actors)}");
                }
                floorSum += run.Floor;
                levelSum += run.Hero.Level;
                turnSum += run.Turn;
                floorsReached[run.Floor]++;
            }
            Console.WriteLine($"Fresh level-1 runs, autopilot over {seeds} seeds: won {won}, lost {lost}, stalled {stalled}");
            Console.WriteLine($"Average: floor {floorSum / (float)seeds:0.0}, level {levelSum / (float)seeds:0.0}, turns {turnSum / (float)seeds:0}");
            for (int f = 1; f < floorsReached.Length; f++) Console.WriteLine($"  ended on B{f}F: {floorsReached[f]}");
            CampaignReport(players: 100, maxAttempts: 10);
            return 0;
        }

        /// <summary>Levels carried between runs, as in the real game: how many attempts until the first clear.</summary>
        static void CampaignReport(int players, int maxAttempts)
        {
            int cleared = 0, attemptsSum = 0, levelSum = 0;
            var clearedOnAttempt = new int[maxAttempts + 1];
            for (int player = 1; player <= players; player++)
            {
                var progress = new HeroProgress(ActorCatalog.Uzuki);
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    var run = new DungeonRun(player * 1000 + attempt, null, progress);
                    for (int i = 0; i < 5000 && run.State == RunState.InProgress; i++) run.Execute(AutoPilot.Decide(run));
                    if (run.State != RunState.Won) continue;
                    cleared++;
                    attemptsSum += attempt;
                    levelSum += progress.Level;
                    clearedOnAttempt[attempt]++;
                    break;
                }
            }
            Console.WriteLine($"\nLevels kept between runs, {players} autopilot players, up to {maxAttempts} attempts each:");
            Console.WriteLine($"  {cleared} beat the King Slime; on average on attempt {attemptsSum / (float)Math.Max(1, cleared):0.0}, at Lv {levelSum / (float)Math.Max(1, cleared):0.0}");
            for (int a = 1; a <= maxAttempts; a++)
                if (clearedOnAttempt[a] > 0) Console.WriteLine($"  first clear on attempt {a}: {clearedOnAttempt[a]}");
        }
    }
}
