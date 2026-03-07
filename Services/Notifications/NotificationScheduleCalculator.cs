namespace Wiki_Blaze.Services.Notifications;

public static class NotificationScheduleCalculator
{
    public static bool IsBusinessDay(DateTime date)
    {
        return date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
    }

    public static DateTime GetFirstReminderDate(DateTime dueDate)
    {
        return SubtractBusinessDays(dueDate.Date, 3);
    }

    public static DateTime SubtractBusinessDays(DateTime date, int businessDays)
    {
        if (businessDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(businessDays), "businessDays must be >= 0.");
        }

        var result = date.Date;
        var remaining = businessDays;
        while (remaining > 0)
        {
            result = result.AddDays(-1);
            if (IsBusinessDay(result))
            {
                remaining--;
            }
        }

        return result;
    }

    public static int BusinessDaysDifference(DateTime fromDate, DateTime toDate)
    {
        var from = fromDate.Date;
        var to = toDate.Date;

        if (from == to)
        {
            return 0;
        }

        if (from > to)
        {
            return -BusinessDaysDifference(to, from);
        }

        var cursor = from;
        var count = 0;
        while (cursor < to)
        {
            cursor = cursor.AddDays(1);
            if (IsBusinessDay(cursor))
            {
                count++;
            }
        }

        return count;
    }
}
