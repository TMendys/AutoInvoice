using AutoInvoice.Google.Api.Service;
using AutoInvoice.Models;
using Microsoft.Extensions.Configuration;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace AutoInvoice.Google.Api;
public class Googlesheets
{
    readonly SpreadsheetsResource.ValuesResource resource = new GoogleSheetsService().Service.Spreadsheets.Values;
    readonly ValueRange response;
    readonly string range;
    readonly string id;

    public Googlesheets(string? tab)
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        range = $"{tab}!A3:V";
        id = config.GetRequiredSection("SpreadsheetId").Value;
        response = resource.Get(id, range).Execute();
        Values = response.Values;
    }

    public IList<IList<object>> Values { get; set; }

    public static List<Customer>? ReadData(IEnumerable<IList<object>> values)
    {
        if (values != null && values.Any())
        {
            List<Customer> customers = CustomerMapper.MapFromRangeData(values);
            return customers;
        }
        else
        {
            Console.WriteLine("No data found.");
            return null;
        }
    }

    public void UpdateData(IList<IList<object>> values)
    {
        var valueRange = new ValueRange
        {
            Values = CustomerMapper.SetInvoicedToTrueRangeData(values)
        };

        var updateRequest = resource.Update(valueRange, id, range);
        updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource
            .UpdateRequest.ValueInputOptionEnum.USERENTERED;
        updateRequest.Execute();
    }

}
