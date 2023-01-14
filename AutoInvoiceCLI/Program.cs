using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Googlesheets.Api;
using AutoInvoiceCLI;
using AutoInvoice.Models;

// How the sheet is builed
// Kundnr	Mailfaktura	Putsare	Datum	Fakturerad	Namn	Sms	Pris	Service	Framkörning	Tid	Altan	Pris	Tid	Extra	Kommentarer	Företag	Ins.	Pris	Service	Tid Total	Spröjs (Avtagbara)	Källare	Övervåning	Adress	Stad/Stadsdel	Tel.	Tel. 2	E-post

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
var tabArgument = new Argument<string?>
{
    Description = "Vilken flik som ska hämtas"
};

fetchCommand.AddArgument(tabArgument);
invoicedCommand.AddArgument(tabArgument);
rootCommand.Add(fetchCommand);
rootCommand.Add(invoicedCommand);

// GET
// Fetch customers to invoice
fetchCommand.SetHandler((string tab) =>
{
    Googlesheets.Api.Googlesheets sheet = new(tab);
    var customers = Googlesheets.Api.Googlesheets.ReadData(sheet.Values.ToInvoice());
    if (customers is not null)
    {
        CustomerMapper.PrintCustermers(customers);
        CustomerMapper.CreateCsvFile(customers);
    }
}, tabArgument);

// PUT
// Mark invoiced customers on Google sheet
invoicedCommand.SetHandler((string tab) =>
{
    Googlesheets.Api.Googlesheets sheet = new(tab);
    sheet.UpdateData(CustomerMapper.SetInvoicedToTrueRangeData(sheet.Values));
}, tabArgument);

return await new CommandLineBuilder(rootCommand)
    .UseDefaults()
    .Build()
    .InvokeAsync(args);