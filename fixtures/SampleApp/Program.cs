using DotVVM.Framework;

// Minimální hostitel. Fixture se nikdy nespouští jako web — smyslem je,
// aby šla sestavit, a probe proces tak měl skutečnou assembly k načtení.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDotVVM<SampleApp.DotvvmStartup>();

var app = builder.Build();
app.UseDotVVM<SampleApp.DotvvmStartup>(builder.Environment.ContentRootPath);
app.Run();
