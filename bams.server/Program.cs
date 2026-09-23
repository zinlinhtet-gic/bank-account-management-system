using System.Text.Json.Serialization;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using bams.server.Constants;
using bams.server.Configuration;
using bams.server.Data;
using bams.server.Data.Seeders;
using bams.server.DTO.Common;
using bams.server.Messages;
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

// Configure JWT authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not found in configuration");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer not found in configuration");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience not found in configuration");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };

    // Return the standard ApiErrorResponse instead of an empty 401 when [Authorize] rejects a request.
    options.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();

            const MessageCode messageCode = MessageCode.AuthenticationRequired;
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new ApiErrorResponse(
                (int)messageCode,
                messageCode.ToString(),
                MessageCatalog.GetMessage(messageCode),
                context.HttpContext.TraceIdentifier));
        }
    };
});

// Permissions are enforced per endpoint by [RequirePermission] (Middlewares/MiddlewareAttribute.cs).
// AddAuthorization is still required for [Authorize].
builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// -------------------------
// Identity define here
// -------------------------


// -------------------------
// Services define here
// -------------------------

builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
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
app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapStaticAssets();

// Seed security data on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await RolesAndPermissionsSeeder.SeedSecurityDataAsync(dbContext);
}

app.Run();
