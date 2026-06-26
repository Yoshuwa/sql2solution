using MiningFleetOps.Api.CompositionRoot;
using MiningFleetOps.Api.Realtime;
using MiningFleetOps.Infrastructure.Persistence;




using Microsoft.EntityFrameworkCore;




var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHostedService<DatabaseChangePollingService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.CustomSchemaIds(type => (type.FullName ?? type.Name).Replace("+", ".")));

builder.Services.AddCustomApplicationServices(builder.Configuration);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (string.IsNullOrWhiteSpace(connectionString))
        options.UseInMemoryDatabase("GeneratedApi");
    else
        options.UseSqlServer(connectionString);
    
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}



app.UseHttpsRedirection();


app.MapHub<DataChangeHub>(DataChangeHub.Route);
app.MapControllers();
app.Run();

public partial class Program { }