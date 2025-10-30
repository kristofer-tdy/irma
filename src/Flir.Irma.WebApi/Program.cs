using Flir.Irma.WebApi.Data;
using Flir.Irma.WebApi.Infrastructure;
using Flir.Irma.WebApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Irma Web API",
        Version = "v1",
        Description = "Skeleton implementation of the Irma Web API. TODO: Connect to Azure AI Foundry."
    });
});

builder.Services.AddDbContext<IrmaDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    }

    options.UseSqlServer(connectionString);
});

builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddProblemDetails();
builder.Services.AddLogging();

builder.Services.AddSingleton<ApplicationMetadata>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseSwagger();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseRouting();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<IrmaDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapControllers();

app.Run();

public partial class Program;
