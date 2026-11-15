using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PremiumImport.Api.Data;
using PremiumImport.Api.Infrastructure;
using PremiumImport.Api.Services;
using PremiumImport.Api.Services.Import;
using PremiumImport.Api.Services.Query;

var builder = WebApplication.CreateBuilder(args);

// Data
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// Business services
builder.Services.AddScoped<IPremiumMethodService, PremiumMethodService>();
builder.Services.AddScoped<IMetricService, MetricService>();
builder.Services.AddScoped<IImportService, ImportService>();
builder.Services.AddScoped<IMetricDataQueryService, MetricDataQueryService>();
builder.Services.AddScoped<SchemaResolver>();

// Import strategies: adding a file format or data type = registering one more class here.
builder.Services.AddSingleton<ITabularFileReader, ExcelFileReader>();
builder.Services.AddSingleton<ITabularFileReader, CsvFileReader>();
builder.Services.AddSingleton<IFieldValueConverter, TextConverter>();
builder.Services.AddSingleton<IFieldValueConverter, NumberConverter>();
builder.Services.AddSingleton<IFieldValueConverter, DateConverter>();
builder.Services.AddSingleton<IFieldValueConverter, BooleanConverter>();
builder.Services.AddSingleton<FieldValueConverters>();

// Web
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options => options.AddPolicy("Angular", policy =>
    policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("Angular");
app.MapControllers();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DemoData.SeedAsync(db);
}

app.Run();

public partial class Program;
