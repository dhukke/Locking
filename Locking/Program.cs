using Locking;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Data;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

builder.Services.AddDbContext<DataContext>(options =>

    options
        .UseSqlServer("Server=localhost,1433;User ID=sa;Password=yourStrong(!)Password;Initial Catalog=lockingDb;TrustServerCertificate=true;")
        .UseLoggerFactory(
            LoggerFactory.Create(builder => builder.AddConsole())
        )
);

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/users", (DataContext dataContext) =>
{
    return dataContext.Users.ToList();
})
.WithName("GetUsers");

app.MapPost("/users", async (DataContext dataContext, User user) =>
{
    dataContext.Users.Add(user);
    await dataContext.SaveChangesAsync();
})
.WithName("CreateUser");

app.MapDelete("/users-pessimistic/{userId:guid}", async (DataContext dataContext, ILoggerFactory loggerFactory, Guid userId) =>
{
    using var transaction = dataContext.Database.BeginTransaction(IsolationLevel.Serializable);

    try
    {
        var user = dataContext.Users.FirstOrDefault(x => x.Id == userId);

        if (user is null)
        {
            return Results.NotFound();
        }

        dataContext.Users.Remove(user);
        await dataContext.SaveChangesAsync();

        transaction.Commit();
        return Results.NoContent();

    }
    catch (Exception ex)
    {
        var logger = loggerFactory.CreateLogger("ApiIntegrationLog.Api");
        logger.LogInformation("Error removing user with id: {@UserId}. Error message: {@ErrorMessage}", userId, ex.Message);

        transaction.Rollback();
        return Results.Conflict();
    }

})
.WithName("DeleteUserPessimisticLocking");

// good choice when is not expected a large number of colisions
app.MapDelete("/users-optimistic/{userId:guid}", async (DataContext dataContext, ILoggerFactory loggerFactory, Guid userId) =>
{
    var user = dataContext.Users.FirstOrDefault(x => x.Id == userId);

    if (user is null)
    {
        return Results.NotFound();
    }

    dataContext.Users.Remove(user);

    try
    {
        await dataContext.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException ex)
    {
        var logger = loggerFactory.CreateLogger("ApiIntegrationLog.Api");
        logger.LogInformation("Error removing user with id: {@UserId}. Error message: {@ErrorMessage}", userId, ex.Message);

        return Results.Conflict();
    }

    return Results.NoContent();
})
.WithName("DeleteUserOptimisticLocking");


app.Run();