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
using bams.server.Services.Jobs;
using bams.server.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // Automatic model-validation failures use the standard ApiErrorResponse contract (not ProblemDetails),
        // so clients always receive a stable code and a readable message for the first invalid field.
        options.InvalidModelStateResponseFactory = context =>
        {
            var firstError = context.ModelState.Values
                .SelectMany(entry => entry.Errors)
                .Select(error => error.ErrorMessage)
                .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));
            var response = new ApiErrorResponse(
                (int)MessageCode.ValidationFailed,
                MessageCode.ValidationFailed.ToString(),
                firstError ?? MessageCatalog.GetMessage(MessageCode.ValidationFailed),
                context.HttpContext.TraceIdentifier);

            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(response);
        };
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
builder.Services.AddHttpContextAccessor();

// -------------------------
// Identity define here
// -------------------------


// -------------------------
// Services define here
// -------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<CustomerNumberGenerator>();
builder.Services.AddScoped<ICustomerService, CustomerService>();

// builder.Services.AddScoped<ICustomerLookUpService, CustomerLookUpService>();
// builder.Services.AddScoped<ICustomerCreationService, CustomerCreationService>();
builder.Services.AddScoped<IAccountStatusHistoryService, AccountStatusHistoryService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IAccountingReportService, AccountingReportService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IEndOfDayAuditService, EndOfDayAuditService>();
builder.Services.AddScoped<LedgerPostingService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IInterbankTransferService, InterbankTransferService>();
builder.Services.AddScoped<INrcTransferService, NrcTransferService>();
builder.Services.AddScoped<ITransactionQueryService, TransactionQueryService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAccountHolderService, AccountHolderService>();
builder.Services.AddScoped<IAccountTypeService, AccountTypeService>();
builder.Services.AddScoped<IAccountRefererService, AccountRefererService>();
builder.Services.AddScoped<IInterestRateRuleService, InterestRateRuleService>();
builder.Services.AddScoped<IFixedDepositService, FixedDepositService>();
builder.Services.AddScoped<IAccountDocumentService, AccountDocumentService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAccountTransactionService, AccountTransactionService>();
builder.Services.AddScoped<IAccountingReportService, AccountingReportService>();
builder.Services.AddScoped<IScheduledTransactionService, ScheduledTransactionService>();
builder.Services.AddScoped<IGeneralLedgerPostingService, GeneralLedgerPostingService>();
builder.Services.AddScoped<ScheduledFinancialPostingService>();
builder.Services.AddScoped<AccountMaintenanceService>();
builder.Services.AddScoped<InterestAccumulationService>();
builder.Services.AddScoped<ProductSeeder>();
builder.Services.AddScoped<InterestRateRuleSeeder>();
builder.Services.AddScoped<FeeRuleSeeder>();
builder.Services.AddScoped<TestDataSeeder>();
builder.Services.AddSingleton<FileUploadUtils>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IOtherBankService, OtherBankService>();
builder.Services.AddScoped<IInterestRateService, InterestRateService>();
builder.Services.AddScoped<IFeeRateService, FeeRateService>();
builder.Services.AddScoped<IBankPolicyService, BankPolicyService>();
builder.Services.AddScheduledJobs(builder.Configuration, jobs =>
{
    var monthlyAtMyanmarMidnight = JobSchedule.Monthly(5, TimeSpan.Zero, ScheduledJobPeriod.TimeZoneId);
    jobs.Add<AccountMaintenanceService>(
        "account-maintenance",
        "Account Maintenance",
        monthlyAtMyanmarMidnight,
        (service, context, cancellationToken) => service.ExecuteAsync(context, cancellationToken));
    jobs.Add<InterestAccumulationService>(
        "interest-accumulation",
        "Interest Accumulation",
        monthlyAtMyanmarMidnight,
        (service, context, cancellationToken) => service.ExecuteAsync(context, cancellationToken));
});

var app = builder.Build();

// Apply schema changes before idempotently populating product reference data.
await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();

    // Security data must be seeded before any other seeder adds users: on an empty database it clears the
    // Users table, which would otherwise delete the scheduled-jobs system actor created by ProductSeeder.
    await RolesAndPermissionsSeeder.SeedSecurityDataAsync(dbContext);
    await ChartOfAccountsSeeder.SeedGlAccountsAsync(dbContext);
    await BranchSeeder.SeedBranchesAsync(dbContext);

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
    // GlobalExceptionHandler below is the single error handler; there is no MVC /Home/Error page to re-execute.
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
    await OtherBankSeeder.SeedAsync(dbContext);
    await ChartOfAccountsSeeder.SeedGlAccountsAsync(dbContext);
    await BranchSeeder.SeedBranchesAsync(dbContext);
}

app.Run();
