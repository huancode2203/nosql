using System.Text;
using EduManageLms.Api.Application;
using EduManageLms.Api.Hubs;
using EduManageLms.Api.Infrastructure;
using EduManageLms.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using Serilog;

var conventionPack = new ConventionPack { new CamelCaseElementNameConvention() };
ConventionRegistry.Register("camelCase", conventionPack, _ => true);

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));
builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection(MongoOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<BackupOptions>(builder.Configuration.GetSection(BackupOptions.SectionName));
builder.Services.Configure<FormOptions>(x => x.MultipartBodyLengthLimit = 50 * 1024 * 1024);

builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<IndexInitializer>();
builder.Services.AddSingleton<DataSeeder>();
builder.Services.AddSingleton<ExtendedDataSeeder>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IStudentAnalyticsService, StudentAnalyticsService>();
builder.Services.AddScoped<ScoreNormalizationService>();
builder.Services.AddScoped<IGradebookService, GradebookService>();
builder.Services.AddScoped<AdminGradePublicationService>();
builder.Services.AddScoped<IAdminResourceService, AdminResourceService>();
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddScoped<IAdminAcademicService, AdminAcademicService>();
builder.Services.AddScoped<AdminAcademicService>();
builder.Services.AddScoped<ILecturerPortalService, LecturerPortalService>();
builder.Services.AddScoped<IStudentPortalService, StudentPortalService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IImportExportService, ImportExportService>();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var token = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs")) context.Token = token;
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers().AddJsonOptions(x => x.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "EduManage LMS API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
    });
});

var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("login", limiter =>
{
    limiter.PermitLimit = 10;
    limiter.Window = TimeSpan.FromMinutes(1);
    limiter.QueueLimit = 0;
}));

var app = builder.Build();
app.UseMiddleware<ExceptionMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseSerilogRequestLogging();
app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.MapGet("/health/live", () => Results.Ok(new
{
    status = "healthy",
    service = "EduManageLms.Api",
    time = DateTime.UtcNow
}));

static async Task<IResult> DatabaseHealthAsync(MongoContext mongo, CancellationToken ct)
{
    try
    {
        await mongo.Database.RunCommandAsync<BsonDocument>(
            new BsonDocument("ping", 1),
            cancellationToken: ct);
        return Results.Ok(new
        {
            status = "healthy",
            database = "connected",
            time = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            status = "unhealthy",
            database = "disconnected",
            error = ex.Message,
            time = DateTime.UtcNow
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

app.MapGet("/health/ready", DatabaseHealthAsync);
app.MapGet("/health", DatabaseHealthAsync);

using (var scope = app.Services.CreateScope())
{
    var indexes = scope.ServiceProvider.GetRequiredService<IndexInitializer>();
    var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
    var extendedSeeder = scope.ServiceProvider.GetRequiredService<ExtendedDataSeeder>();
    try
    {
        await indexes.InitializeAsync();
        if (app.Configuration.GetValue<bool>("SeedData"))
        {
            await seeder.SeedAsync();
            await extendedSeeder.SeedAsync();
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "MongoDB initialization failed");
        throw;
    }
}

app.Run();
public partial class Program { }
