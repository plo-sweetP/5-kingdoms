using System.Collections.Generic;
using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    public class TimelineTests
    {
        static Actor Make(int id, int speed, Team team = Team.Enemy) =>
            new Actor(id, new ActorDefinition("test" + id, "Test" + id, 10, 1, 1, 0, speed: speed), team, default);

        /// <summary>The times of an actor's first <paramref name="turns"/> turns when it fights alone.</summary>
        static List<AvTime> TurnTimes(int speed, int turns)
        {
            var actor = Make(1, speed, Team.Hero);
            var timeline = new Timeline();
            timeline.Start(new[] { actor });
            var times = new List<AvTime>();
            for (int i = 0; i < turns; i++)
            {
                timeline.AdvanceTo(timeline.PeekNext(actor));
                times.Add(timeline.Now);
                timeline.EndTurn(actor, 100);
            }
            return times;
        }

        [Test]
        public void Speed134GetsTwoTurnsInTheFirstCycle()
        {
            var times = TurnTimes(134, 2);
            Assert.AreEqual(0, Timeline.CycleOf(times[0]), "74.63 AV");
            Assert.AreEqual(0, Timeline.CycleOf(times[1]), "149.25 AV is still inside the first 150");
            Assert.AreEqual(new AvTime(20000, 134), times[1]);
        }

        [Test]
        public void Speed133DoesNot()
        {
            var times = TurnTimes(133, 2);
            Assert.AreEqual(0, Timeline.CycleOf(times[0]), "75.19 AV");
            Assert.AreEqual(1, Timeline.CycleOf(times[1]), "150.38 AV spills into the next cycle");
        }

        [Test]
        public void Speed120ThirdTurnLandsExactlyOnTheCycleBoundary()
        {
            var times = TurnTimes(120, 4);
            Assert.AreEqual(AvTime.FromWhole(250), times[2], "exactly 250, no rounding");
            Assert.AreEqual(1, Timeline.CycleOf(times[2]), "a turn on the boundary belongs to the cycle ending there");
            Assert.AreEqual(2, Timeline.CycleOf(times[3]));
        }

        [Test]
        public void CyclesAre150ThenEvery100()
        {
            Assert.AreEqual(0, Timeline.CycleOf(AvTime.Zero));
            Assert.AreEqual(0, Timeline.CycleOf(AvTime.FromWhole(150)));
            Assert.AreEqual(1, Timeline.CycleOf(new AvTime(150001, 1000)));
            Assert.AreEqual(1, Timeline.CycleOf(AvTime.FromWhole(250)));
            Assert.AreEqual(2, Timeline.CycleOf(AvTime.FromWhole(251)));
            Assert.AreEqual(9, Timeline.CycleOf(AvTime.FromWhole(1050)));
        }

        [Test]
        public void TiesGoToTheLeaderThenTheLowerId()
        {
            var lowId = Make(2, 100);
            var leader = Make(9, 100, Team.Hero);
            var other = Make(5, 100);
            var timeline = new Timeline();
            timeline.Start(new[] { other, lowId, leader });

            var order = timeline.Forecast(leader, 3).Select(turn => turn.Actor.Id).ToArray();
            CollectionAssert.AreEqual(new[] { 9, 2, 5 }, order);
        }

        [Test]
        public void FasterActorsActMoreOften()
        {
            var hero = Make(1, 100, Team.Hero);
            var quick = Make(2, 150);
            var timeline = new Timeline();
            timeline.Start(new[] { hero, quick });

            var turns = timeline.Forecast(hero, 15).Where(turn => turn.Time <= AvTime.FromWhole(600)).ToList();
            Assert.AreEqual(6, turns.Count(turn => turn.Actor == hero));
            Assert.AreEqual(9, turns.Count(turn => turn.Actor == quick));
        }

        [Test]
        public void ForecastDoesNotChangeTheTimeline()
        {
            var hero = Make(1, 100, Team.Hero);
            var timeline = new Timeline();
            timeline.Start(new[] { hero, Make(2, 90) });
            var before = timeline.NextTurnOf(1);
            timeline.Forecast(hero, 10);
            Assert.AreEqual(before, timeline.NextTurnOf(1));
            Assert.AreEqual(AvTime.Zero, timeline.Now);
        }

        [Test]
        public void ASpeedChangeKeepsTheDistanceLeft()
        {
            var slow = Make(1, 100, Team.Hero);
            var fast = Make(2, 200);
            var timeline = new Timeline();
            timeline.Start(new[] { slow, fast }); // slow at 100 AV, fast at 50 AV.

            timeline.AdvanceTo(timeline.PeekNext(slow));
            Assert.AreEqual(AvTime.FromWhole(50), timeline.Now);
            timeline.ChangeSpeed(slow, 200); // Half its gauge is left: 5000 at speed 200 is 25 AV.

            Assert.AreEqual(200, slow.Speed);
            Assert.AreEqual(AvTime.FromWhole(75), timeline.NextTurnOf(slow.Id), "not reset to a full turn (100 AV from now)");
        }

        [Test]
        public void ActionCostsScaleTheWait()
        {
            Assert.AreEqual(AvTime.FromWhole(100), Timeline.TurnLength(100));
            Assert.AreEqual(AvTime.FromWhole(50), Timeline.TurnLength(100, 50));
            Assert.AreEqual(new AvTime(13000, 100), Timeline.TurnLength(100, 130));
        }

        [Test]
        public void HandlesSeveralActorsPerTeam()
        {
            var party = new[] { Make(1, 100, Team.Hero), Make(2, 110, Team.Hero), Make(3, 95, Team.Hero), Make(4, 120, Team.Hero) };
            var enemies = new[] { Make(5, 90), Make(6, 100), Make(7, 130) };
            var timeline = new Timeline();
            timeline.Start(party.Concat(enemies));

            var turns = timeline.Forecast(party[0], 40);
            Assert.AreEqual(40, turns.Count);
            for (int i = 1; i < turns.Count; i++) Assert.IsTrue(turns[i - 1].Time <= turns[i].Time, $"turn {i} is out of order");
            Assert.IsTrue(party.Concat(enemies).All(actor => turns.Any(turn => turn.Actor == actor)));
        }
    }
}
