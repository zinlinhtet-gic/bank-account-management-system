using System.Text.Json.Serialization;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using bams.server.Constants;
using bams.server.Data;
using bams.server.Data.Seeders;
using bams.server.Middlewares;
using bams.server.Services;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

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
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("user_management", policy =>
        policy.Requirements.Add(new PermissionRequirement(SecurityConstants.UserManagement)));
    options.AddPolicy("customer_management", policy =>
        policy.Requirements.Add(new PermissionRequirement(SecurityConstants.CustomerManagement)));
    options.AddPolicy("customer_kyc", policy =>
        policy.Requirements.Add(new PermissionRequirement(SecurityConstants.CustomerKyc)));
    options.AddPolicy("accounting", policy =>
        policy.Requirements.Add(new PermissionRequirement(SecurityConstants.Accounting)));
    options.AddPolicy("configuration", policy =>
        policy.Requirements.Add(new PermissionRequirement(SecurityConstants.Configuration)));
    options.AddPolicy("operation", policy =>
        policy.Requirements.Add(new PermissionRequirement(SecurityConstants.Operation)));
    options.AddPolicy("account_management", policy =>
        policy.Requirements.Add(new PermissionRequirement(SecurityConstants.AccountManagement)));
    options.AddPolicy("transactions", policy =>
        policy.Requirements.Add(new PermissionRequirement(SecurityConstants.Transactions)));
    options.AddPolicy("transaction_history", policy =>
        policy.Requirements.Add(new PermissionRequirement(SecurityConstants.TransactionHistory)));
    options.AddPolicy("audit", policy =>
        policy.Requirements.Add(new PermissionRequirement(SecurityConstants.Audit)));
    options.AddPolicy("customer_list", policy =>
        policy.Requirements.Add(new PermissionRequirement(SecurityConstants.CustomerList)));
});

builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
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

var app = builder.Build();

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
    await SecuritySeeder.SeedSecurityDataAsync(dbContext);
}

app.Run();
