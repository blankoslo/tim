using System.Globalization;

internal class ISOWeekShim
{
    public static int GetYear(DateOnly date)
    {
#if NET10_0_OR_GREATER
        return ISOWeek.GetYear(date);
#else
        return ISOWeek.GetYear(new DateTime(date, TimeOnly.MinValue));
#endif
    }

    public static int GetWeekOfYear(DateOnly date)
    {
#if NET10_0_OR_GREATER
        return ISOWeek.GetWeekOfYear(date);
#else
        return ISOWeek.GetWeekOfYear(new DateTime(date, TimeOnly.MinValue));
#endif
    }
}
