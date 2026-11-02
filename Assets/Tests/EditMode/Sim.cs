using Alba.Core;

namespace Alba.Core.Tests
{
    /// <summary>Small hand-made levels and helpers to drive a stage step by step.</summary>
    static class Sim
    {
        public const float Step = 1f / 60f;

        /// <summary>A flat empty field with the player at the origin looking north and the portal far away.</summary>
        public static ChapterDef Field(params Vec2[] gems) => new ChapterDef
        {
            Index = 0,
            Title = "Field",
            Seed = 7,
            Half = 40f,
            Hills = 0f,
            Start = Vec2.Zero,
            StartYaw = 0f,
            Portal = new Vec2(0f, 30f),
            Page = new Vec2(-30f, -30f),
            Gems = gems,
        };

        public static Stage Stage(ChapterDef def, params Obstacle[] obstacles)
        {
            Level level = LevelBuilder.Build(def);
            level.Obstacles.AddRange(obstacles);
            return new Stage(level, 99);
        }

        public static Obstacle Tree(float x, float z, float radius = 0.5f) =>
            new Obstacle { Kind = ObstacleKind.Pine, Center = new Vec2(x, z), Radius = radius, Height = 8f };

        public static void Run(Stage stage, float seconds, InputFrame input)
        {
            for (float t = 0f; t < seconds; t += Step) stage.Tick(Step, input);
        }

        public static void Run(Stage stage, float seconds) => Run(stage, seconds, InputFrame.None);

        public static bool Happened(Stage stage, StageEventKind kind)
        {
            foreach (StageEvent e in stage.Events) if (e.Kind == kind) return true;
            return false;
        }

        /// <summary>Runs until an event happens (true) or the time is up (false).</summary>
        public static bool RunUntil(Stage stage, StageEventKind kind, float seconds, InputFrame input)
        {
            for (float t = 0f; t < seconds; t += Step)
            {
                stage.Tick(Step, input);
                if (Happened(stage, kind)) return true;
            }
            return false;
        }

        public static InputFrame Fire => new InputFrame(Vec2.Zero, firePressed: true, fireHeld: true);
    }
}
