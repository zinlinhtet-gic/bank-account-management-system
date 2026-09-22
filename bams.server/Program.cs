using System.Text.Json.Serialization;
using bams.server.Configuration;
using bams.server.Data;
using bams.server.Data.Seeders;
using bams.server.Middlewares;
using bams.server.Services;
using bams.server.Services.Interfaces;
using bams.server.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Register the EF Core context using the configured MySQL connection string.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySQL(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.Configure<FileUploadOptions>(
    builder.Configuration.GetSection(FileUploadOptions.SectionName));
var maximumUploadRequestSize = builder.Configuration.GetValue<long?>(
    $"{FileUploadOptions.SectionName}:MaximumRequestSizeBytes")
    ?? FileUploadOptions.DefaultMaximumRequestSizeBytes;
builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = maximumUploadRequestSize);
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = maximumUploadRequestSize);

// builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
//     .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// -------------------------
// Identity define here
// -------------------------


// -------------------------
// Services define here
// -------------------------

builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IAccountHolderService, AccountHolderService>();
builder.Services.AddScoped<IAccountTypeService, AccountTypeService>();
builder.Services.AddScoped<IFixedDepositService, FixedDepositService>();
builder.Services.AddScoped<IAccountDocumentService, AccountDocumentService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAccountTransactionService, AccountTransactionService>();
builder.Services.AddScoped<IAccountingReportService, AccountingReportService>();
builder.Services.AddScoped<ProductSeeder>();
builder.Services.AddScoped<TestDataSeeder>();
builder.Services.AddSingleton<FileUploadUtils>();

var app = builder.Build();

// Apply schema changes before idempotently populating product reference data.
await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();

    var productSeeder = scope.ServiceProvider.GetRequiredService<ProductSeeder>();
    await productSeeder.SeedAsync();

    // Populate deterministic sample customers and accounts only in development environments.
    if (app.Environment.IsDevelopment())
    {
        var testDataSeeder = scope.ServiceProvider.GetRequiredService<TestDataSeeder>();
        await testDataSeeder.SeedAsync();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseMiddleware<GlobalExceptionHandler>();
app.MapControllers();
app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();


app.Run();
