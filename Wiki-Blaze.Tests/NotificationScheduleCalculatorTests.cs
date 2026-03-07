using Wiki_Blaze.Services.Notifications;

namespace Wiki_Blaze.Tests;

public class NotificationScheduleCalculatorTests
{
    [Fact]
    public void DueMonday_FirstReminderIsPreviousWednesday()
    {
        var dueDate = new DateTime(2026, 3, 16);

        var reminderDate = NotificationScheduleCalculator.GetFirstReminderDate(dueDate);

        Assert.Equal(new DateTime(2026, 3, 11), reminderDate);
    }

    [Fact]
    public void WeekendIsNotBusinessDay()
    {
        Assert.False(NotificationScheduleCalculator.IsBusinessDay(new DateTime(2026, 3, 14))); // Saturday
        Assert.False(NotificationScheduleCalculator.IsBusinessDay(new DateTime(2026, 3, 15))); // Sunday
        Assert.True(NotificationScheduleCalculator.IsBusinessDay(new DateTime(2026, 3, 16))); // Monday
    }

    [Fact]
    public void BusinessDaysDifference_SkipsWeekendDays()
    {
        var friday = new DateTime(2026, 3, 13);
        var monday = new DateTime(2026, 3, 16);

        var businessDaysToMonday = NotificationScheduleCalculator.BusinessDaysDifference(friday, monday);

        Assert.Equal(1, businessDaysToMonday);
    }
}
