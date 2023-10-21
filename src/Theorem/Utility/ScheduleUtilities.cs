using System;
using System.Globalization;

namespace Theorem.Utility;

[Flags]
public enum WeekOfMonth
{
    None = 0,
    First = 1,
    Second = 2,
    Third = 4,
    Fourth = 8,
    Fifth = 16,
    All = First | Second | Third | Fourth | Fifth,
}

public class WeeklyRecurringTime
{
    public uint WeekInterval { get; set; } = 1;
    public DateTimeOffset StartDateTime { get; set; }
}

public record WeeklyRecurringSchedule
{
    public TimeZoneInfo TimeZone { get; set; } = default!;
    public DateOnly StartDate { get; set; }
    public TimeOnly AtTime { get; set; }
    public uint WeeklyInterval { get; set; }

    public DateTime GetNextOccurrence(in DateTime searchFromDateTime)
    {
        var firstOccurrenceDateTime = StartDate.ToDateTime(AtTime);

        // If the first occurrence is in the future, return that.
        if (firstOccurrenceDateTime > searchFromDateTime)
        {
            return firstOccurrenceDateTime;
        }

        // Determine the number of weeks that have passed between the start date
        // and the "search from" date.
        var firstOccurrenceWeek = ISOWeek.GetWeekOfYear(firstOccurrenceDateTime);
        var searchWeek = ISOWeek.GetWeekOfYear(searchFromDateTime);

        // If we're searching in a future year, count those weeks first
        int weekDelta = 0;
        int targetYearOffset = 0;
        for (int year = ISOWeek.GetYear(firstOccurrenceDateTime);
            year < ISOWeek.GetYear(searchFromDateTime); ++year)
        {
            targetYearOffset++;
            weekDelta += ISOWeek.GetWeeksInYear(year);
        }
        // Then apply the delta between the search week number and scheduled week number
        weekDelta += (searchWeek - firstOccurrenceWeek);

        // Determine how far the "search from" week is from the weekly interval
        DateTime nextOccurrenceDateTime;
        while (true)
        {
            int targetWeekOffset = weekDelta % (int)WeeklyInterval;
            nextOccurrenceDateTime = ISOWeek.ToDateTime(
                (searchFromDateTime.Year + targetYearOffset),
                searchWeek + targetWeekOffset, firstOccurrenceDateTime.DayOfWeek)
                .AddHours(AtTime.Hour).AddMinutes(AtTime.Minute);

            // If the next occurrence date is before the "search from" date, roll over
            // to the next weekly interval.
            if (nextOccurrenceDateTime < searchFromDateTime)
            {
                weekDelta += (int)WeeklyInterval;
                int maxWeeks = ISOWeek.GetWeeksInYear(searchFromDateTime.Year + targetYearOffset);
                if (weekDelta >= maxWeeks)
                {
                    targetYearOffset += 1;
                    weekDelta %= maxWeeks;
                    searchWeek = 1;
                }
                continue;
            }
            break;
        }

        return nextOccurrenceDateTime;
    }
}