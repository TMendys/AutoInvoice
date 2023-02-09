using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.CommandLine.Hosting;
using AutoInvoice.Google.Api;
using AutoInvoice.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

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

var vismaCommand = new Command("visma");

fetchCommand.AddArgument(tabArgument);
invoicedCommand.AddArgument(tabArgument);
rootCommand.Add(fetchCommand);
rootCommand.Add(invoicedCommand);
rootCommand.Add(vismaCommand);

vismaCommand.SetHandler(async (context) =>
{
    var host = context.GetHost();
    var httpClientFactory = host.Services.GetRequiredService<IHttpClientFactory>();
    // var httpClientFactory = context.BindingContext.GetRequiredService<IHttpClientFactory>();
    var client = httpClientFactory.CreateClient();

    using var request = new HttpRequestMessage(
        HttpMethod.Get, "https://localhost:44300/jwt");
    using var response = await client.SendAsync(request);
    var readResponse = await response.Content.ReadAsStringAsync();
    Console.WriteLine(readResponse);
});

// GET
// Fetch customers to invoice
fetchCommand.SetHandler((string tab) =>
{
    Googlesheets sheet = new(tab);
    var toInvoice = CustomerMapper.ToInvoice(sheet.Values);
    var customers = Googlesheets.ReadData(toInvoice);
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
    Googlesheets sheet = new(tab);
    sheet.UpdateData(CustomerMapper.SetInvoicedToTrueRangeData(sheet.Values));
}, tabArgument);

return await new CommandLineBuilder(rootCommand)
    .UseHost(_ => new HostBuilder(), (builder) => builder
        .ConfigureServices((_, services) =>
        {
            services.AddHttpClient();
        }))
    .UseDefaults()
    .Build()
    .InvokeAsync(args);