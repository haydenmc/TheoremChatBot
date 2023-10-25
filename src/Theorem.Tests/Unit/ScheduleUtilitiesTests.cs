using Theorem.Utility;

namespace Theorem.Tests.Unit;

[TestClass]
public class ScheduleUtilitiesTests
{
    [TestMethod]
    public void TestBasicBiWeeklyRecurringSchedule()
    {
        // Every 2 Mondays starting October 30th, 2023
        WeeklyRecurringSchedule biWeeklyMondays = new(){
            TimeZone = TimeZoneInfo.Local,
            StartDate = DateOnly.ParseExact("2023-10-30", "yyyy-MM-dd"),
            AtTime = TimeOnly.ParseExact("17:00", "HH:mm"),
            WeeklyInterval = 2,
        };

        // Get the next occurrence starting from November 1st, 2023.
        // Expect November 13th, 2023.
        var nextOccurrence = biWeeklyMondays.GetNextOccurrence(new DateTime(2023, 11, 1));
        Assert.AreEqual(expected: new DateOnly(2023, 11, 13).ToDateTime(biWeeklyMondays.AtTime),
            actual: nextOccurrence);

        // Get the next occurrence starting from November 20th, 2023
        // Expect November 27th, 2023
        nextOccurrence = biWeeklyMondays.GetNextOccurrence(new DateTime(2023, 11, 20));
        Assert.AreEqual(expected: new DateOnly(2023, 11, 27).ToDateTime(biWeeklyMondays.AtTime),
            actual: nextOccurrence);
    }

    [TestMethod]
    public void TestWeeklyRecurringScheduleFutureDate()
    {
        WeeklyRecurringSchedule biWeeklyMondays = new(){
            TimeZone = TimeZoneInfo.Local,
            StartDate = DateOnly.ParseExact("2023-10-30", "yyyy-MM-dd"),
            AtTime = TimeOnly.ParseExact("17:00", "HH:mm"),
            WeeklyInterval = 2,
        };

        var nextOccurrence = biWeeklyMondays.GetNextOccurrence(new DateTime(2023, 10, 1));
        Assert.AreEqual(expected: biWeeklyMondays.StartDate.ToDateTime(biWeeklyMondays.AtTime),
            actual: nextOccurrence);
    }

    [TestMethod]
    public void TestYearBoundaryBiWeeklyRecurringSchedule()
    {
        // Every 2 Mondays starting December 23rd, 2024 @ 5pm
        WeeklyRecurringSchedule biWeeklyMondays = new(){
            TimeZone = TimeZoneInfo.Local,
            StartDate = DateOnly.ParseExact("2024-01-08", "yyyy-MM-dd"),
            AtTime = TimeOnly.ParseExact("17:00", "HH:mm"),
            WeeklyInterval = 2,
        };

        var nextOccurrence = biWeeklyMondays.GetNextOccurrence(new DateTime(2024, 12, 24));
        Assert.AreEqual(expected: new DateOnly(2025, 1, 6).ToDateTime(biWeeklyMondays.AtTime),
            actual: nextOccurrence);
    }

    [TestMethod]
    public void TestMultiYearWeekIntervalsRecurringSchedule()
    {
        // Every 2 Wednesdays starting January 4th, 2023 @ 5pm
        WeeklyRecurringSchedule schedule = new(){
            TimeZone = TimeZoneInfo.Local,
            StartDate = DateOnly.ParseExact("2023-01-04", "yyyy-MM-dd"),
            AtTime = TimeOnly.ParseExact("17:00", "HH:mm"),
            WeeklyInterval = 2,
        };

        var expected = schedule.StartDate.ToDateTime(schedule.AtTime);
        DateTime last = expected;
        for (int i = 0; i < 53; ++i)
        {
            last = schedule.GetNextOccurrence(last);
            Assert.AreEqual(expected: expected, actual: last);
            expected = expected.AddDays(14);
            last = last.AddDays(1);
        }
    }
}