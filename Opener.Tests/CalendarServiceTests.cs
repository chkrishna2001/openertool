using System;
using Opener.Services;
using Xunit;

namespace Opener.Tests;

public class CalendarServiceTests
{
    private readonly DateTime _baseDate = new DateTime(2026, 6, 18, 10, 30, 0); // Thursday 10:30 AM

    [Fact]
    public void ParseRelativeDateTime_ExactDateTime_ParsesCorrectly()
    {
        var input = "2026-06-25 14:00:00";
        var result = CalendarService.ParseRelativeDateTime(input, _baseDate);
        Assert.Equal(new DateTime(2026, 6, 25, 14, 0, 0), result);
    }

    [Fact]
    public void ParseRelativeDateTime_EmptyInput_DefaultsToNextHour()
    {
        var result = CalendarService.ParseRelativeDateTime("", _baseDate);
        // Base is 10:30, next hour should be 11:00
        Assert.Equal(new DateTime(2026, 6, 18, 11, 0, 0), result);
    }

    [Fact]
    public void ParseRelativeDateTime_TomorrowWithTime_ParsesCorrectly()
    {
        var result = CalendarService.ParseRelativeDateTime("tomorrow 9am", _baseDate);
        Assert.Equal(new DateTime(2026, 6, 19, 9, 0, 0), result);
    }

    [Fact]
    public void ParseRelativeDateTime_TodayWithTime_ParsesCorrectly()
    {
        var result = CalendarService.ParseRelativeDateTime("today 3pm", _baseDate);
        Assert.Equal(new DateTime(2026, 6, 18, 15, 0, 0), result);
    }

    [Fact]
    public void ParseRelativeDateTime_NextWeekday_ParsesCorrectly()
    {
        // Base is June 18, 2026 (Thursday)
        // Next Monday should be June 22, 2026
        var result = CalendarService.ParseRelativeDateTime("next monday 10:30", _baseDate);
        Assert.Equal(new DateTime(2026, 6, 22, 10, 30, 0), result);
    }

    [Fact]
    public void ParseRelativeDateTime_DayOfWeekOnly_ParsesCorrectly()
    {
        // Base is June 18, 2026 (Thursday)
        // Friday should be June 19, 2026
        var result = CalendarService.ParseRelativeDateTime("friday 4pm", _baseDate);
        Assert.Equal(new DateTime(2026, 6, 19, 16, 0, 0), result);
    }
}
