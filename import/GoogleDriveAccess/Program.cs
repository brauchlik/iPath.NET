using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;


// Path to your service account JSON file
string jsonPath = "C:/Daten/ipath_sqlite/google/service-account.json";

// Load the credential from the JSON file
GoogleCredential credential;
using (var stream = new FileStream(jsonPath, FileMode.Open, FileAccess.Read))
{
    credential = GoogleCredential.FromStream(stream)
        .CreateScoped(DriveService.Scope.Drive)
        .CreateWithUser("server@ipathnetwork.org"); // For domain-wide delegation
}

var service = new DriveService(new BaseClientService.Initializer
{
    HttpClientInitializer = credential,
    ApplicationName = "iPath-Server"
});



// Replace with your target folder ID
string rootFolderId = "1loAXoV8vJD-44OBuE_ZWirEm-fEZMrKt";

// Query to get all files and folders inside the specified folder
FilesResource.ListRequest listRequest = service.Files.List();
// listRequest.Q = $"'{rootFolderId}' in parents and trashed = false";
listRequest.Q = "mimeType = 'application/vnd.google-apps.folder' and trashed = false";
listRequest.Fields = "nextPageToken, files(id, name, mimeType)";

IList<Google.Apis.Drive.v3.Data.File> items = listRequest.Execute().Files;

Console.WriteLine("Items in folder:");
if (items != null && items.Count > 0)
{
    foreach (var item in items)
    {
        string type = item.MimeType == "application/vnd.google-apps.folder" ? "Folder" : "File";
        Console.WriteLine($"{type}: {item.Name} ({item.Id})");
    }
}
else
{
    Console.WriteLine("No items found in the folder.");
}


// create a new folder
string newFolderName = "New Test Folder";
var newId = CreateOrGetFolder(rootFolderId, newFolderName);
Console.WriteLine("Folder {0}, Id={1}", newFolderName, rootFolderId);




Dictionary<string, string> GetSubFolders(string folderId)
{
    // Query to get all files and folders inside the specified folder
    FilesResource.ListRequest listRequest = service.Files.List();
    listRequest.Q = $"'{folderId}' in parents and trashed = false";
    listRequest.Fields = "nextPageToken, files(id, name, mimeType)";

    IList<Google.Apis.Drive.v3.Data.File> items = listRequest.Execute().Files;

    var ret = new Dictionary<string, string>();
    if (items != null && items.Count > 0)
    {
        foreach (var item in items)
        {
            if (item.MimeType == "application/vnd.google-apps.folder")
            {
                ret.Add(item.Name, item.Id);
            }
        }
    }

    return ret;
}

string CreateOrGetFolder(string parentId, string newFolderName)
{
    // Query for a folder with the given name in the parent folder
    string query = $"'{parentId}' in parents and name = '{newFolderName}' and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
    FilesResource.ListRequest listRequest = service.Files.List();
    listRequest.Q = query;
    listRequest.Fields = "files(id, name)";
    var result = listRequest.Execute();

    if (result.Files != null && result.Files.Count > 0)
    {
        Console.WriteLine("folder already exists");
        return result.Files[0].Id;
    }

    // Create file metadata for the new folder
    var fileMetadata = new Google.Apis.Drive.v3.Data.File
    {
        Name = newFolderName,
        MimeType = "application/vnd.google-apps.folder",
        Parents = new List<string> { parentId }
    };

    // Create the folder
    var createRequest = service.Files.Create(fileMetadata);
    createRequest.Fields = "id";
    var folder = createRequest.Execute();

    return folder.Id;
}