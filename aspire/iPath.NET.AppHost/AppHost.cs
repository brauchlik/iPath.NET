using Projects;

var builder = DistributedApplication.CreateBuilder(args);


var postgres = builder.AddPostgres("postgres");
var postgresdb = postgres.AddDatabase("postgresdb");

/*
var ollama = builder.AddOllama("ollama")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume()
    .WithOpenWebUI();
var mistral = ollama.AddModel("mistral", "mistral");
*/

var ipath = builder.AddProject<iPath_Blazor_Server>("ipath-server")
    .WithReference(postgresdb)
    .WaitFor(postgresdb);
/*
    .WithReference(ollama)
    .WaitFor(ollama);
*/

var app = builder.Build();
app.Run();
