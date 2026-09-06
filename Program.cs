using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SevenShows.Api.Data;
using SevenShows.Api.Modules.Auth.Services;
using SevenShows.Api.Modules.Tenants.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Conexão existente com o MySQL (Mantida original e intacta)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// 2. Configura o serviço autenticador do JWT (Mantido estável)
var jwtSection = builder.Configuration.GetSection("Jwt");
var key = Encoding.ASCII.GetBytes(jwtSection["Secret"]!);

builder.Services.AddHttpClient<AsaasWalletService>();

builder.Services.AddHttpClient<AsaasService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSection["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddControllers();

// 3. OpenAPI Nativo estável do .NET 10
builder.Services.AddOpenApi();

builder.Services.AddScoped<TokenService>();

// 1. Definição da Política de CORS (Adicionar ANTES de builder.Build())
builder.Services.AddCors(options =>
{
    options.AddPolicy("SevenShowsCorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:8081") // A URL exata do seu painel Vue
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// 2. Ativação do Middleware de CORS (Adicionar OBRIGATORIAMENTE DEPOIS de app.UseRouting())
app.UseCors("SevenShowsCorsPolicy");

// NOVO: Permite que o .NET sirva as fotos salvas na pasta wwwroot/uploads
app.UseStaticFiles();

// 4. Ativa o Swagger UI oficial lendo o arquivo OpenAPI nativo
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // Gera o JSON padronizado em /openapi/v1.json
    
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "SevenShows API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication(); 
app.UseAuthorization();  

app.MapControllers();

app.Run();
