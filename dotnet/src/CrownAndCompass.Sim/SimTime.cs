namespace CrownAndCompass.Sim;

/// <summary>
/// Time model for real-time-with-pause: the client advances the simulation one hour per step
/// at whatever speed the player chooses (or not at all while paused). Strategic systems run on the daily tick.
/// </summary>
public static class SimTime
{
    public const int HoursPerDay = 24;

    public static int HourOf(int tick) => tick % HoursPerDay;
    public static int DayOf(int tick) => tick / HoursPerDay + 1;

    public static bool IsNight(int hour) => hour < 6 || hour >= 23;
    public static bool IsWorkHours(int hour) => hour >= 7 && hour < 17;
    public static bool IsEvening(int hour) => hour >= 17 && hour < 23;
}
