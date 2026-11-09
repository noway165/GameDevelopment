namespace Metropolis.Core
{
    public enum SimulationSpeed { Paused, X1, X2, X4 }

    /// <summary>Converts real time and speed into whole days to run.</summary>
    public sealed class SimulationClock
    {
        readonly SimulationConfig _config;
        double _accumulatedDays;

        public SimulationClock(SimulationConfig config)
        {
            _config = config;
        }

        public SimulationSpeed Speed { get; set; } = SimulationSpeed.X1;

        public int Advance(double realSeconds)
        {
            if (Speed == SimulationSpeed.Paused || !(realSeconds > 0))
                return 0;

            _accumulatedDays += realSeconds * Multiplier(Speed) / _config.SecondsPerDay;

            // A long hitch must not queue a backlog of days that freezes the game.
            if (_accumulatedDays > _config.MaxDaysPerAdvance)
            {
                _accumulatedDays = 0;
                return _config.MaxDaysPerAdvance;
            }

            int days = (int)_accumulatedDays;
            _accumulatedDays -= days;
            return days;
        }

        static int Multiplier(SimulationSpeed speed)
        {
            switch (speed)
            {
                case SimulationSpeed.X2: return 2;
                case SimulationSpeed.X4: return 4;
                default: return 1;
            }
        }
    }
}
