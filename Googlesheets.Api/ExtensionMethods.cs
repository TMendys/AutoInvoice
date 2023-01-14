using System.Text;

namespace Googlesheets.Api;

internal static class ExtensionMethods
{
    internal static object IfSeason(this object obj, object obj2)
    {
        if (obj2.IsEmpty(out string? season) || season == "Å")
        {
            return obj;
        }

        int todaysMonth = DateTime.Today.Month;
        var (startMonth, endMonth) = season?.Split('-') switch
        {
            var a => (a![0].Month(), a[1].Month())
        };

        if (todaysMonth > startMonth && todaysMonth < endMonth)
        {
            return obj;
        }

        return 0;
    }

    internal static int Month(this string letter) => letter switch
    {
        "F" => 2,
        "M" => 3,
        "A" => 4,
        "S" => 9,
        "O" => 10,
        "N" => 11,
        _ => throw new ArgumentException("Månaden existerar inte som ett val.")
    };

    internal static bool IsEmpty(this object obj) => String.IsNullOrWhiteSpace(obj.ToString());

    internal static bool IsEmpty(this object obj, out string? output) =>
        String.IsNullOrWhiteSpace(output = obj.ToString());

    internal static decimal ExcludeVat(this decimal includeVat, bool isBuisness) =>
        isBuisness ? (includeVat * 0.8M) : includeVat;

    internal static bool IsTrue(this object obj)
    {
        return Boolean.TryParse(obj.ToString() ?? "false", out _);
    }

    internal static bool IsFalse(this object obj) => !Boolean.Parse(obj.ToString() ?? "false");

    internal static DateOnly ToDate(this object dayNumber)
    {
        var dateArray = dayNumber?.ToString()?.Split('/');

        if (dateArray?.Length == 2)
        {
            DateOnly newDate =
                new(DateTime.Now.Year,
                Convert.ToInt32(dateArray[1]),
                Convert.ToInt32(dateArray[0]));

            // Before returning the date, check if the date is in the future, if true then reduce one year.
            return newDate > DateOnly.FromDateTime(DateTime.Now) ? newDate.AddYears(-1) : newDate;
        }

        var date = DateTime.Today;
        while (date.Day != Convert.ToInt32(dayNumber))
        {
            date = date.AddDays(-1);
        }
        return DateOnly.FromDateTime(date);
    }

    internal static int ToHours(this object obj)
    {
        if (obj.IsEmpty(out string? time))
        {
            return 1;
        }

        var timeArray = time?.Split(new Char[] { ':', '.' },
            StringSplitOptions.RemoveEmptyEntries);
        int hours;
        if (timeArray?.Length == 1)
        {
            hours = Convert.ToInt32(timeArray[0]) / 60;
            return hours < 1 ? 1 : hours;
        }

        hours = Convert.ToInt32(timeArray?[0]);
        int minutes = Convert.ToInt32(timeArray?[1]) / 60;
        int total = hours + minutes;
        return total == 0 ? 1 : total;
    }

    internal static decimal ToDecimal(this object obj, bool removeNegatives = false)
    {
        var cellText = obj.ToString();
        if (String.IsNullOrWhiteSpace(cellText))
        {
            return 0;
        }

        StringBuilder sb = new();
        if (cellText[0] == '−')
        {
            if (removeNegatives) return 0;
            sb.Append('-');
        }

        foreach (char c in cellText)
        {
            if (Char.IsDigit(c))
            {
                sb.Append(c);
            }
        }

        if (sb.Length == 0) return 0;
        return Decimal.Parse(sb.ToString());
    }

    internal static decimal ToDecimal(
        this (object T1, object T2) obj, bool removeNegatives = false) =>
        obj.T1.ToDecimal(removeNegatives) + obj.T2.ToDecimal(removeNegatives);

    internal static decimal ToDecimal(
        this (object T1, object T2, object T3) obj, bool removeNegatives = false) =>
        obj.T1.ToDecimal(removeNegatives) + obj.T2.ToDecimal(removeNegatives) + obj.T3.ToDecimal(removeNegatives);

    internal static IEnumerable<IList<object>> ToInvoice(this IList<IList<object>> values) =>
        values.Where(x => !x[0].IsEmpty()
        && !x[3].IsEmpty()
        && Char.IsDigit(x[3].ToString()![0])
        && x[4].IsFalse());
}