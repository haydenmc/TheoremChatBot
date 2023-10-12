using System;

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