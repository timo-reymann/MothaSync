using Microsoft.AspNetCore.Authentication;
using MothaSync.Server.Auth;
using MothaSync.Server.Data;
using MothaSync.Server.Protobuf;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(options =>
{
    options.InputFormatters.Insert(0, new ProtobufInputFormatter());
    options.OutputFormatters.Insert(0, new ProtobufOutputFormatter());
    options.RespectBrowserAcceptHeader = true;
});
builder.Services.AddSingleton<SyncDb>();
builder.Services.AddAuthentication(DeviceAuthentication.Scheme)
    .AddScheme<AuthenticationSchemeOptions, DeviceAuthenticationHandler>(DeviceAuthentication.Scheme, null);
builder.Services.AddAuthorization();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
