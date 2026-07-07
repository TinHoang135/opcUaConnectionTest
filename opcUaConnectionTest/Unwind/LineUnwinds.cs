namespace unwindRollRuntime.Unwind
{
    public sealed class LineUnwinds
    {
        public required string ProductionLine { get; set; }
        public required string BusinessUnit { get; set; }
        public List<Unwind> Unwinds { get; set; } = new();
    }

    public sealed class Unwind
    {
        // Basic properties
        public required string Name { get; set; }
        public required bool HasSpliced { get; set; } = false;

        // Roll parameters
        public required double RollAIsActive { get; set; } = 0;
        public required double RollACurrentDiameter { get; set; } = 1000; // mm
        public required double RollBCurrentDiameter { get; set; } = 1000; // mm
    }
}
