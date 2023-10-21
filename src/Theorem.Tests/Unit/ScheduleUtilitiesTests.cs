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
    public void TestMultiYearBiWeeklyRecurringSchedule()
    {
        // Every 2 Mondays starting January 8th, 2024 @ 5pm
        WeeklyRecurringSchedule biWeeklyMondays = new(){
            TimeZone = TimeZoneInfo.Local,
            StartDate = DateOnly.ParseExact("2024-01-08", "yyyy-MM-dd"),
            AtTime = TimeOnly.ParseExact("17:00", "HH:mm"),
            WeeklyInterval = 2,
        };

        // This isn't working:
        Console.WriteLine(biWeeklyMondays.GetNextOccurrence(new DateTime(2024, 12, 23, 17, 01, 00)));

        DateTime next = new(2024, 1, 8, 17, 00, 00);
        for (int i = 0; i < 60; ++i)
        {
            next = biWeeklyMondays.GetNextOccurrence(next);
            Console.WriteLine(next);
            next = next.AddMinutes(1);
        }
        // Get the next occurrence starting from February 1st, 2025.
        // 2024
        // Jan 22
        // Feb 5
        // Feb 19
        // Mar 4
        // Mar 18
        // Apr 1
        // Apr 15
        // Apr 29
        // May 13
        // May 27
        // Jun 10
        // Jun 24
        // Jul 8
        // Jul 22
        // Aug 5
        // Aug 19
        // Sept 2
        // Sept 16
        // Sept 30
        // Oct 14
        // Oct 28
        // Nov 11
        // Nov 25
        // Dec 9
        // Dec 23
        // 2025
        // Jan 6
        // Jan 20
        // Feb 3
        // Expect Feb 10th, 2025.
        var nextOccurrence = biWeeklyMondays.GetNextOccurrence(new DateTime(2025, 2, 1));
        Assert.AreEqual(expected: new DateOnly(2025, 2, 10).ToDateTime(biWeeklyMondays.AtTime),
            actual: nextOccurrence);
    }
}