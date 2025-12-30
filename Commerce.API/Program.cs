using Commerce.Application.Common.Interfaces;
using StackExchange.Redis;
using Commerce.Application.Features.Auth;
using Commerce.Application.Features.Auth.DTOs;
using Commerce.Application.Features.Carts;
using Commerce.Application.Features.Orders;
using Commerce.Application.Features.Products;
using Commerce.Application.Features.Users; // ✅ ADDED for IUserManagementService
using Commerce.Application.Features.Payments; // ✅ ADDED for IPaymentService
using Commerce.Infrastructure.Data;
using Commerce.Infrastructure.Identity;
using Commerce.Infrastructure.Repositories;
using Commerce.Application.Features.Inventory;
using Commerce.Infrastructure.Services;
using Commerce.Infrastructure.Settings;
using Commerce.Infrastructure.Configuration;
using Commerce.Domain.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using DotNetEnv;

// Load .env file from current or parent directories
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Bridge .env to Configuration
var khaltiSecret = Environment.GetEnvironmentVariable("KHALTI_SECRET_KEY");
var khaltiPublic = Environment.GetEnvironmentVariable("KHALTI_PUBLIC_KEY");
var googleClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
var googleClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");

// URLs
var backendUrl = Environment.GetEnvironmentVariable("BACKEND_URL");
var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL");
var adminAppUrl = Environment.GetEnvironmentVariable("ADMIN_APP_URL");
var googleRedirectUri = Environment.GetEnvironmentVariable("GOOGLE_REDIRECT_URI");
var khaltiBaseUrl = Environment.GetEnvironmentVariable("KHALTI_BASE_URL");
var khaltiReturnUrl = Environment.GetEnvironmentVariable("KHALTI_RETURN_URL");
var khaltiWebsiteUrl = Environment.GetEnvironmentVariable("KHALTI_WEBSITE_URL");

// Apply to configuration
if (!string.IsNullOrEmpty(khaltiSecret)) builder.Configuration["Khalti:SecretKey"] = khaltiSecret;
if (!string.IsNullOrEmpty(khaltiPublic)) builder.Configuration["Khalti:PublicKey"] = khaltiPublic;
if (!string.IsNullOrEmpty(googleClientId)) builder.Configuration["Authentication:Google:ClientId"] = googleClientId;
if (!string.IsNullOrEmpty(googleClientSecret)) builder.Configuration["Authentication:Google:ClientSecret"] = googleClientSecret;

// URLs
if (!string.IsNullOrEmpty(backendUrl)) builder.Configuration["BackendUrl"] = backendUrl;
if (!string.IsNullOrEmpty(frontendUrl)) builder.Configuration["FrontendUrl"] = frontendUrl;
if (!string.IsNullOrEmpty(adminAppUrl)) builder.Configuration["AdminAppUrl"] = adminAppUrl;
if (!string.IsNullOrEmpty(googleRedirectUri)) builder.Configuration["Authentication:Google:RedirectUri"] = googleRedirectUri;
if (!string.IsNullOrEmpty(khaltiBaseUrl)) builder.Configuration["Khalti:BaseUrl"] = khaltiBaseUrl;
if (!string.IsNullOrEmpty(khaltiReturnUrl)) builder.Configuration["Khalti:ReturnUrl"] = khaltiReturnUrl;
if (!string.IsNullOrEmpty(khaltiWebsiteUrl)) builder.Configuration["Khalti:WebsiteUrl"] = khaltiWebsiteUrl;

// Add services to the container.

// 1. Database Configuration
builder.Services.AddDbContext<CommerceDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 1.1 Redis Configuration
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

// 2. Identity Configuration
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<CommerceDbContext>()
    .AddDefaultTokenProviders();

// 3. Authentication & JWT Configuration
var jwtSettings = builder.Configuration.GetSection("Jwt");
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
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Secret"]!)),
            ClockSkew = TimeSpan.Zero
        };
    });

// 4. Dependency Injection
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ISecurityLogger, SecurityLogger>();
builder.Services.AddScoped<IImageStorageService, CloudinaryImageStorageService>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<IReturnService, ReturnService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

// 5. Cloudinary Configuration
builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("Cloudinary"));

// 6. Khalti Configuration & Services
builder.Services.Configure<KhaltiSettings>(builder.Configuration.GetSection("Khalti"));

// 7. Inventory Configuration
builder.Services.Configure<InventoryConfiguration>(builder.Configuration.GetSection("Inventory"));

builder.Services.AddHttpClient<IKhaltiPaymentService, KhaltiPaymentService>();
builder.Services.AddHostedService<PaymentReconciliationService>();

// 8. Google OAuth Service
builder.Services.AddHttpClient<IGoogleAuthService, GoogleAuthService>();
builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy =>
        policy.RequireRole(UserRoles.Admin, UserRoles.SuperAdmin));
});

// 5. API Configuration
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Commerce API", Version = "v1" });
    
    // JWT Support in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Seed Roles
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    
    var roles = new[] { "SuperAdmin", "Admin", "Warehouse", "Support", "Customer" };
    
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // Seed Admin User
    var adminEmail = "admin@ecommerce.com";
    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        var admin = new ApplicationUser 
        { 
            UserName = adminEmail, 
            Email = adminEmail, 
            EmailConfirmed = true,
            MfaEnabled = false // Disabled for testing simplicity
        };
        await userManager.CreateAsync(admin, "Admin123!");
        await userManager.AddToRoleAsync(admin, "Admin");
    }
}

app.Run();