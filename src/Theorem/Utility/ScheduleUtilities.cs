using System;
using System.Globalization;

namespace Theorem.Utility;

public static class SchedulingHelpers
{
    public static int ToIsoDayOfWeekNum(this DayOfWeek dayOfWeek)
    {
        if (dayOfWeek == DayOfWeek.Sunday)
        {
            return 7;
        }
        return (int)dayOfWeek;
    }
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

        // Advance through each year to determine the exact week delta between the recurrence
        // start date and the "search from" date.
        int weekDelta = 0;
        int yearOffset = 0;
        int firstOccurrenceYear = ISOWeek.GetYear(firstOccurrenceDateTime);
        int searchFromYear = ISOWeek.GetYear(searchFromDateTime);
        for (int year = firstOccurrenceYear; year <= searchFromYear; ++year)
        {
            if (year < searchFromYear)
            {
                yearOffset++;
                weekDelta += ISOWeek.GetWeeksInYear(year);
            }
            else if (year == searchFromYear)
            {
                var firstOccurrenceWeek = ISOWeek.GetWeekOfYear(firstOccurrenceDateTime);
                var searchWeek = ISOWeek.GetWeekOfYear(searchFromDateTime);
                weekDelta += (searchWeek - firstOccurrenceWeek);
            }
        }

        // If the "search from" date aligns with the recurrence interval, but the
        // recurrence time has already passed, we need to advance to the next interval
        var searchFromTimeOnly = new TimeOnly(searchFromDateTime.Hour, searchFromDateTime.Minute);
        var isSameWeek = (weekDelta % WeeklyInterval == 0);
        var isSameDayButAfterTime = ((searchFromTimeOnly > AtTime) &&
            (searchFromDateTime.DayOfWeek == firstOccurrenceDateTime.DayOfWeek));
        var isLaterDay = (searchFromDateTime.DayOfWeek.ToIsoDayOfWeekNum() >
            firstOccurrenceDateTime.DayOfWeek.ToIsoDayOfWeekNum());
        if (isSameWeek && (isLaterDay || isSameDayButAfterTime))
        {
            weekDelta += (int)WeeklyInterval;
        }

        // Line up the week delta with the weekly interval
        weekDelta += weekDelta % (int)WeeklyInterval;

        // Translate the week delta to an exact date by iterating from the start week
        var weeks = ISOWeek.GetWeekOfYear(firstOccurrenceDateTime) + weekDelta;
        var occurrenceYear = firstOccurrenceYear;
        while (true)
        {
            var currentYearWeeks = ISOWeek.GetWeeksInYear(occurrenceYear);
            if (weeks > currentYearWeeks)
            {
                weeks -= currentYearWeeks;
            }
            else
            {
                break;
            }
            ++occurrenceYear;
        }
        return ISOWeek.ToDateTime(occurrenceYear, weeks, firstOccurrenceDateTime.DayOfWeek)
            .Add(AtTime.ToTimeSpan());
    }
}