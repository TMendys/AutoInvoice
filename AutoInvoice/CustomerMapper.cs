using System.Text;

namespace AutoInvoice;
public static class CustomerMapper
{
    public static List<Customer> MapFromRangeData(IEnumerable<IList<object>> values)
    {
        List<Customer> customers = new();

        foreach (var row in values)
        {
            bool isBuisness = row[16].IsTrue();
            bool isSupscriber = !row[6].IsEmpty();
            decimal discount = row[14].ToDecimal();
            bool haveDiscount = discount < 0 && isSupscriber;

            Customer customer = new
            (
                CustomerNumber: row[0].ToString()!,
                Email: row[1].IsTrue(),
                Date: row[3].ToDate(),
                SubscriptionCost: isSupscriber ?
                    (row[7], row[12].IfSeason(row[11])).ToDecimal().ExcludeVat(isBuisness) : 0,
                LaborCost: isSupscriber ?
                    (row[14], row[18]).ToDecimal(removeNegatives: true)
                        .ExcludeVat(isBuisness)
                    : (row[7], row[14], row[18]).ToDecimal().ExcludeVat(isBuisness),
                ServiceCost: (row[8], row[19]).ToDecimal().ExcludeVat(isBuisness),
                DrivingCost: row[9].ToDecimal().ExcludeVat(isBuisness),
                Discount: haveDiscount ? discount : 0,
                TotalTimeInHours: row[20].IsEmpty() ? row[10].ToHours() : row[20].ToHours(),
                Comments: row[15].ToString()
            );

            customers.Add(customer);
        }

        return customers;
    }
    public static IList<IList<object>> SetInvoicedToTrueRangeData(IList<IList<object>> values)
    {
        Queue<string> customerNumbers = new();

        string path = Path.Combine(Environment.CurrentDirectory, "Serialization.csv");
        string[] lines = File.ReadAllLines(path);
        foreach (string line in lines)
        {
            int index = line.IndexOf(',');
            if (index > 0)
            {
                customerNumbers.Enqueue(line[..index]);
            }
        }

        Console.WriteLine("Kund nummer:");
        foreach (var row in values)
        {
            if (row[0].ToString() == customerNumbers.Peek())
            {
                row[4] = "TRUE";
                var number = customerNumbers.Dequeue();
                Console.WriteLine($"{number}");
            }
            if (customerNumbers.Count == 0) break;
        }

        return values;
    }

    public static void PrintCustermers(List<Customer> customers)
    {
        Console.WriteLine($"{"Kund",4} {"Datum",10} | {"Pren.",8} | {"Arbe.",8} | {"Serv.",5} | {"Körn.",6} | {"Rabatt"} | {"Tid",3} | {"Not."}");
        foreach (var customer in customers)
        {
            Console.WriteLine($"{customer.CustomerNumber,4} {customer.Date} | {customer.SubscriptionCost,8:C0} | {customer.LaborCost,8:C0} | {customer.ServiceCost,5:C0} | {customer.DrivingCost,6:C0} | {customer.Discount,6} | {customer.TotalTimeInHours,3} | {customer.Comments}");
        }
    }

    public static void CreateCsvFile(IEnumerable<Customer> customers)
    {
        string csv = customers.Aggregate(
            new StringBuilder(),
            (sb, s) => sb.Append(s),
            sb => sb.ToString());

        string path = Path.Combine(Environment.CurrentDirectory, "Serialization.csv");
        File.WriteAllText(path, csv);
    }
}