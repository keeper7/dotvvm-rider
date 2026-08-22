using DotVVM.Framework;

// A minimal host. The fixture is never run as a web application; the point is that it can be
// built, so the probe process has a real assembly to load.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDotVVM<SampleApp.DotvvmStartup>();

var app = builder.Build();
app.UseDotVVM<SampleApp.DotvvmStartup>(builder.Environment.ContentRootPath);
app.Run();
