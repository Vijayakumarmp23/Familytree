using FamilyTree.Api.Data;
using FamilyTree.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---- Services ----
builder.Services.AddDbContext<FamilyTreeContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")
                      ?? "Data Source=familytree.db"));

builder.Services.AddScoped<GenealogyService>();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // Avoid cycles if navigation objects are ever serialized directly.
        o.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        // Accept & emit RelationshipType as strings ("Spouse"/"ParentChild")
        // instead of the enum's numeric value, so POST bodies match GET output.
        o.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS so the Vue dev server (Vite) can call the API.
const string CorsPolicy = "VueDev";
builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicy, p => p
        .WithOrigins("http://localhost:5173", "http://localhost:3000")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

// ---- Seed the database on startup ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FamilyTreeContext>();
    await SeedData.InitializeAsync(db);
}

// ---- HTTP pipeline ----
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);
app.MapControllers();

app.Run();
