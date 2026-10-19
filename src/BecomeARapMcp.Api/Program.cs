var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSqliteConnection("sqlite");
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/hello", () => TypedResults.Ok(new { message = "Hello" }))
    .WithName("Hello");

app.MapDefaultEndpoints();
app.Run();
