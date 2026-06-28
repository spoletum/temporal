using PayR.Temporal.Psp.Payout.Client;
using PayR.Temporal.SayHello.Client;
using PayR.Temporal.Web.Components;
using PayR.Temporal.Web.Workflows;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Temporal integration.
builder.Services.Configure<TemporalSettings>(
    builder.Configuration.GetSection("Temporal"));
builder.Services.AddSingleton<TemporalClientProvider>();
builder.Services.AddSingleton<WorkflowRunner>();

// Register workflow definitions here. Adding a new workflow to the UI
// is just one line: register its IWorkflowDefinition implementation from
// the workflow's .Client package.
builder.Services.AddSingleton<IWorkflowDefinition, SayHelloWorkflowDefinition>();
builder.Services.AddSingleton<IWorkflowDefinition, PayoutWorkflowDefinition>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
