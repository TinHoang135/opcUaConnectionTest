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
        public bool HasSpliced { get; set; } = false;

        // Roll parameters
        public DateTimeOffset? lastSpliceTime { get; set; } = null;
        public double RollAIsActive { get; set; } = 0;
        public double RollACurrentDiameter { get; set; } = 1000; // mm
        public double RollBCurrentDiameter { get; set; } = 1000; // mm
    }
}
