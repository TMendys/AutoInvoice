namespace AutoInvoiceCLI;
public static class ExtensionMethods
{
    internal static IEnumerable<IList<object>> ToInvoice(this IList<IList<object>> values) =>
    values.Where(x => !x[0].IsEmpty()
    && !x[3].IsEmpty()
    && Char.IsDigit(x[3].ToString()![0])
    && x[4].IsFalse());

    internal static bool IsEmpty(this object obj) => String.IsNullOrWhiteSpace(obj.ToString());

    internal static bool IsFalse(this object obj) => !Boolean.Parse(obj.ToString() ?? "false");
}
