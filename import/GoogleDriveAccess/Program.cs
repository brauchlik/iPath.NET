using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;


// If modifying these scopes, delete your previously saved credentials
// at ~/.credentials/drive-dotnet-quickstart.json
string[] Scopes = { DriveService.Scope.DriveReadonly }; // Example: read-only access

UserCredential credential;

using (var stream = new FileStream("C:/Daten/ipath_sqlite/google/client_secret.json", FileMode.Open, FileAccess.Read))
{
    // The file client_secrets.json contains your client ID and client secret.
    // Download it from the Google Cloud Console after creating an OAuth 2.0 Client ID.
    credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
        GoogleClientSecrets.FromStream(stream).Secrets,
        Scopes,
        "user", // User ID to store credentials
        CancellationToken.None,
        new FileDataStore("DriveApiTokenStore")); // Stores tokens locally
}

// Create Drive API service.
var service = new DriveService(new BaseClientService.Initializer()
{
    HttpClientInitializer = credential,
    ApplicationName = "YourDriveApp",
});

// Example: List files in the user's Drive
FilesResource.ListRequest listRequest = service.Files.List();
listRequest.PageSize = 10;
listRequest.Fields = "nextPageToken, files(id, name)";

IList<Google.Apis.Drive.v3.Data.File> files = listRequest.Execute().Files;
Console.WriteLine("Files:");
if (files != null && files.Count > 0)
{
    foreach (var file in files)
    {
        Console.WriteLine("{0} ({1})", file.Name, file.Id);
    }
}
else
{
    Console.WriteLine("No files found.");
}

Console.ReadKey();