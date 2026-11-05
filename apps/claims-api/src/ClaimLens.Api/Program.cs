using ClaimLens.Api;
using ClaimLens.Api.Persistence;

var builder = WebApplication.CreateBuilder(args);

// `migrate` and `seed` run and exit. They use the owner's connection; the app never does.
if (args is ["migrate"] or ["seed"])
{
    return MigrationRunner.RunCommand(args[0], builder.Configuration);
}

builder.AddClaimLens();
var app = builder.Build();
app.UseClaimLens();
app.Run();
return 0;

public partial class Program;
