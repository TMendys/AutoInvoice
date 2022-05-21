using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Configuration;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using AutoInvoice;

// How the sheet is builed
// Kundnr	Mailfaktura	Putsare	Datum	Fakturerad	Namn	Sms	Pris	Service	Framkörning	Tid	Altan	Pris	Tid	Extra	Kommentarer	Företag	Ins.	Pris	Service	Tid Total	Spröjs (Avtagbara)	Källare	Övervåning	Adress	Stad/Stadsdel	Tel.	Tel. 2	E-post

IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

// Fetch customers from a Google sheet and save it to a cvs file 
// to be used to automate sending invoice.
var rootCommand = new RootCommand("Hämtar kunder som ska faktureras från Google kalkylblad för att sedan skicka dem via Visma Eekonomi.");

// Fetch the customers
var fetchCommand = new Command("hämta", "Skapa ny csv-fil.");
fetchCommand.AddAlias("fetch");

// Mark the customers that have been invoiced in the Google sheet.
var invoicedCommand = new Command("fakturerat", "Bocka i kundlistan med alla kunder som är fakturerade.");
invoicedCommand.AddAlias("invoiced");

// What tab to fetch or mark on
var tabArgument = new Argument<string?>();
tabArgument.Description = "Vilken flik som ska hämtas";

fetchCommand.AddArgument(tabArgument);
invoicedCommand.AddArgument(tabArgument);
rootCommand.Add(fetchCommand);
rootCommand.Add(invoicedCommand);

// GET
// Fetch customers to invoice
fetchCommand.SetHandler((string tab) =>
{
    var resource = new GoogleSheetsService().Service.Spreadsheets.Values;
    var range = $"{tab}!A3:V";
    var id = config.GetRequiredSection("SpreadsheetId").Value;
    var response = resource.Get(id, range).Execute();

    var values = response.Values.ToInvoice();
    if (values != null && values.Any())
    {
        var customers = CustomerMapper.MapFromRangeData(values);
        CustomerMapper.PrintCustermers(customers);
        CustomerMapper.CreateCsvFile(customers);
    }
    else
    {
        Console.WriteLine("No data found.");
    }
}, tabArgument);

// PUT
// Mark invoiced customers on Google sheet
invoicedCommand.SetHandler((string tab) =>
{
    var resource = new GoogleSheetsService().Service.Spreadsheets.Values;
    var range = $"{tab}!A3:V";
    var id = config.GetRequiredSection("SpreadsheetId").Value;
    var response = resource.Get(id, range).Execute();
    var values = response.Values;

    var valueRange = new ValueRange
    {
        Values = CustomerMapper.SetInvoicedToTrueRangeData(values)
    };

    var updateRequest = resource.Update(valueRange, id, range);
    updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource
        .UpdateRequest.ValueInputOptionEnum.USERENTERED;
    updateRequest.Execute();
}, tabArgument);

return await new CommandLineBuilder(rootCommand)
    .UseDefaults()
    .Build()
    .InvokeAsync(args);