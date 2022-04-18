using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;

namespace AutoInvoice;
public class GoogleSheetsHelper
{
    static readonly string[] Scopes = { SheetsService.Scope.Spreadsheets };
    const string ApplicationName = "AutoInvoice";
    public SheetsService Service { get; }

    public GoogleSheetsHelper()
    {
        using var stream = new FileStream(
            "credentials.json", FileMode.Open, FileAccess.Read);

        // The file token.json stores the user's access and refresh tokens, and is created
        // automatically when the authorization flow completes for the first time.
        UserCredential credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(stream).Secrets,
            Scopes,
            "user",
            CancellationToken.None,
            new FileDataStore("token.json", true)).Result;

        // Create Google Sheets API service.
        Service = new SheetsService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName,
        });
    }
}