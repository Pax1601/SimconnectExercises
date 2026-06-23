
public class Converters
{
    public static double KnotsToFeetPerMinute(double knots)
    {
        return knots * 101.2686; // 1 knot = 101.2686 feet per minute
    }

    public static double FeetPerMinuteToKnots(double feetPerMinute)
    {
        return feetPerMinute / 101.2686; // 1 knot = 101.2686 feet per minute
    }

    public static double RadiansToDegrees(double radians)
    {
        return radians * (180.0 / Math.PI);
    }

    public static double DegreesToRadians(double degrees)
    {
        return degrees * (Math.PI / 180.0);
    }
}
