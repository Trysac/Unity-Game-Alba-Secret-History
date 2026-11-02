using NUnit.Framework;

namespace Alba.Core.Tests
{
    public class PlayerTests
    {
        static readonly InputFrame Forward = new InputFrame(new Vec2(0f, 1f));
        static readonly InputFrame SprintForward = new InputFrame(new Vec2(0f, 1f), sprint: true);

        [Test]
        public void WalkingForwardFollowsTheViewAtWalkingSpeed()
        {
            Stage stage = Sim.Stage(Sim.Field());
            stage.Player.Yaw = 90f;
            Sim.Run(stage, 2f, Forward);
            Assert.That(stage.Player.Position.X, Is.EqualTo(2f * Tuning.WalkSpeed).Within(0.4f), "east, at walking pace");
            Assert.That(stage.Player.Position.Z, Is.EqualTo(0f).Within(0.01f));
            Assert.That(stage.Player.Stride, Is.GreaterThan(8f));
        }

        [Test]
        public void TurningChangesTheYawAndPitchIsClamped()
        {
            Stage stage = Sim.Stage(Sim.Field());
            stage.Tick(Sim.Step, new InputFrame(Vec2.Zero, yawDelta: -30f, pitchDelta: 200f));
            Assert.That(stage.Player.Yaw, Is.EqualTo(330f).Within(0.01f));
            Assert.That(stage.Player.Pitch, Is.EqualTo(80f));
        }

        [Test]
        public void SprintingIsFasterAndDrainsStaminaUntilWinded()
        {
            Stage walk = Sim.Stage(Sim.Field());
            Stage sprint = Sim.Stage(Sim.Field());
            walk.Player.Position = sprint.Player.Position = new Vec2(0f, -35f);
            Sim.Run(walk, 1.5f, Forward);
            Sim.Run(sprint, 1.5f, SprintForward);
            Assert.That(sprint.Player.Position.Z, Is.GreaterThan(walk.Player.Position.Z + 2.5f));
            Assert.That(sprint.Player.Stamina, Is.LessThan(1f));

            float sprinted = 1.5f;
            while (!sprint.Player.Winded && sprinted < 10f)
            {
                sprint.Tick(Sim.Step, SprintForward);
                sprinted += Sim.Step;
            }
            Assert.That(sprint.Player.Winded, Is.True, "ran out of breath");
            Assert.That(sprinted, Is.EqualTo(1f / Tuning.StaminaDrain).Within(0.1f));
            Assert.That(sprint.Player.Stamina, Is.Zero);
            float before = sprint.Player.Position.Z;
            Sim.Run(sprint, 1f, SprintForward);
            Assert.That(sprint.Player.Position.Z - before, Is.EqualTo(Tuning.WalkSpeed).Within(0.3f), "winded: walking pace only");

            Sim.Run(sprint, Tuning.StaminaResume / Tuning.StaminaRegen + 0.2f);
            Assert.That(sprint.Player.Winded, Is.False);
        }

        [Test]
        public void TreesStopThePlayer()
        {
            Stage stage = Sim.Stage(Sim.Field(), Sim.Tree(0f, 4f, 0.6f));
            Sim.Run(stage, 3f, Forward);
            Assert.That(stage.Player.Position.Z, Is.LessThan(4f - 0.6f - Tuning.PlayerRadius + 0.05f));
        }

        [Test]
        public void TheArenaEdgeStopsThePlayer()
        {
            Stage stage = Sim.Stage(Sim.Field());
            stage.Player.Yaw = 180f;
            Sim.Run(stage, 15f, SprintForward);
            Assert.That(stage.Player.Position.Z, Is.GreaterThanOrEqualTo(-40f + 2f + Tuning.PlayerRadius - 0.01f));
        }

        [Test]
        public void TheTorchDrainsWhileOnWarnsWhenEmptyAndRechargesWhileOff()
        {
            Stage stage = Sim.Stage(Sim.Field());
            Assert.That(stage.Player.Lit, Is.True);
            Sim.Run(stage, 10f);
            Assert.That(stage.Player.Battery, Is.EqualTo(1f - 10f * Tuning.BatteryDrain).Within(0.01f));

            stage.Player.Battery = 0.001f;
            Assert.That(Sim.RunUntil(stage, StageEventKind.TorchEmpty, 1f, InputFrame.None), Is.True);
            Assert.That(stage.Player.Lit, Is.False);
            Assert.That(stage.Player.LightOn, Is.True, "switched on but flat");

            stage.Tick(Sim.Step, new InputFrame(Vec2.Zero, toggleLight: true));
            Assert.That(Sim.Happened(stage, StageEventKind.TorchToggled), Is.True);
            Assert.That(stage.Player.LightOn, Is.False);
            Sim.Run(stage, 5f);
            Assert.That(stage.Player.Battery, Is.EqualTo(5f * Tuning.BatteryRecharge).Within(0.02f));
        }

        [Test]
        public void APotionHealsAndIsRefusedAtFullHealthOrWhenTheFlaskIsEmpty()
        {
            Stage stage = Sim.Stage(Sim.Field());
            var drink = new InputFrame(Vec2.Zero, usePotion: true);
            stage.Player.Potions = 1;
            stage.Tick(Sim.Step, drink);
            Assert.That(Sim.Happened(stage, StageEventKind.PotionRefused), Is.True, "already at full health");
            Assert.That(stage.Player.Potions, Is.EqualTo(1));

            stage.Player.Health = 30;
            stage.Tick(Sim.Step, drink);
            Assert.That(Sim.Happened(stage, StageEventKind.PotionDrunk), Is.True);
            Assert.That(stage.Player.Health, Is.EqualTo(30 + Tuning.PotionHeal));
            Assert.That(stage.Player.Potions, Is.Zero);

            stage.Tick(Sim.Step, drink);
            Assert.That(Sim.Happened(stage, StageEventKind.PotionRefused), Is.True, "the flask is empty");
            Assert.That(stage.Player.Health, Is.EqualTo(30 + Tuning.PotionHeal));
        }

        [Test]
        public void TheTorchLightsOnlyWhatIsInItsConeInRangeAndInSight()
        {
            Stage stage = Sim.Stage(Sim.Field(), Sim.Tree(6f, 6f, 1f));
            Player player = stage.Player;
            Level level = stage.Level;
            Assert.That(player.Illuminates(level, new Vec2(0f, 10f), 0.5f), Is.True, "straight ahead");
            Assert.That(player.Illuminates(level, new Vec2(10f, 3f), 0.5f), Is.False, "outside the cone");
            Assert.That(player.Illuminates(level, new Vec2(0f, Tuning.FlashlightRange + 3f), 0.5f), Is.False, "too far");
            player.Yaw = 45f;
            Assert.That(player.Illuminates(level, new Vec2(10f, 10f), 0.5f), Is.False, "behind the tree");
            player.Yaw = 0f;
            player.LightOn = false;
            Assert.That(player.Illuminates(level, new Vec2(0f, 10f), 0.5f), Is.False, "switched off");
        }
    }
}
