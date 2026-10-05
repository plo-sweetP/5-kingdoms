using System.Linq;
using FiveKingdoms.Core;
using NUnit.Framework;

namespace FiveKingdoms.Tests
{
    /// <summary>What the pause menu asks of the rules (HUD.md): leaving a run, and a skill's description from its numbers.</summary>
    public class PauseMenuRulesTests
    {
        [Test]
        public void LeavingEndsTheRunNeitherWonNorLost()
        {
            var run = new DungeonRun(3);
            run.Leave();
            Assert.AreEqual(RunState.Left, run.State);
            Assert.IsTrue(run.Events.OfType<RunEndedEvent>().Any(ended => !ended.Won), "the run says it ended");

            int turn = run.Turn;
            Assert.IsFalse(run.Execute(HeroCommand.Wait), "nothing acts once the party has left");
            Assert.AreEqual(turn, run.Turn);
        }

        [Test]
        public void EverySkillOfTheStartingPartyIsDescribedWithItsNumbers()
        {
            foreach (var definition in ActorCatalog.StartingParty)
            {
                var kit = new HeroProgress(definition).Kit;
                foreach (var skill in kit.Skills.Append(kit.Ultimate).Append(kit.WeaponAttack))
                {
                    string text = SkillText.Describe(skill);
                    Assert.IsNotEmpty(text, skill.Name);
                    StringAssert.Contains(skill.Power.ToString(), text, $"{skill.Name} says how strong it is");
                    Assert.AreEqual(skill.IsUltimate, text.Contains("Ultimate"), skill.Name);
                    Assert.AreEqual(skill.IsQuick, text.Contains("Quick"), skill.Name);
                }
            }
        }

        [Test]
        public void ADescriptionSaysWhatTheSkillDoes()
        {
            StringAssert.Contains("up to 3 tiles away", SkillText.Describe(SkillCatalog.Lunge));
            StringAssert.Contains("3 hits of 90% ATK each", SkillText.Describe(SkillCatalog.TripleThrust));
            StringAssert.Contains("struck back for 250% ATK", SkillText.Describe(SkillCatalog.Riposte));
            StringAssert.Contains("Leaves a snare", SkillText.Describe(SkillCatalog.RollingShot));
            StringAssert.Contains("Not two turns in a row", SkillText.Describe(SkillCatalog.DivineStrike));
            StringAssert.Contains("Sits out the hero's next 4 turns", SkillText.Describe(SkillCatalog.Dash));
        }

        [Test]
        public void AHerosOwnCopyOfASkillReadsAsThatHeroHasIt()
        {
            var stronger = SkillCatalog.Lunge.Change(skill => skill.Power = 260);
            StringAssert.Contains("260% ATK", SkillText.Describe(stronger));
            StringAssert.Contains("200% ATK", SkillText.Describe(SkillCatalog.Lunge), "the catalog's skill is untouched");
        }
    }
}
