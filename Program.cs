using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using SevenShows.Api.Data;
using SevenShows.Api.Modules.Auth.Services;
using SevenShows.Api.Modules.Tenants.Services;

var builder = WebApplication.CreateBuilder(args);


// ======================================================
// BANCO DE DADOS
// ======================================================

var connectionString =
    builder.Configuration.GetConnectionString(
        "DefaultConnection"
    );

builder.Services.AddDbContext<AppDbContext>(
    options =>
        options.UseMySql(
            connectionString,
            ServerVersion.AutoDetect(
                connectionString
            )
        )
);


// ======================================================
// JWT
// ======================================================

var jwtSection =
    builder.Configuration.GetSection("Jwt");

var key =
    Encoding.ASCII.GetBytes(
        jwtSection["Secret"]!
    );

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        key
                    ),

                ValidateIssuer = true,

                ValidIssuer =
                    jwtSection["Issuer"],

                ValidateAudience = true,

                ValidAudience =
                    jwtSection["Audience"],

                ValidateLifetime = true,

                ClockSkew = TimeSpan.Zero
            };
    });


// ======================================================
// SERVIÇOS
// ======================================================

builder.Services.AddHttpClient<AsaasWalletService>();
builder.Services.AddHttpClient<AsaasService>();

builder.Services.AddControllers();

builder.Services.AddOpenApi();

builder.Services.AddScoped<TokenService>();


// ======================================================
// CORS
// ======================================================

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "SevenShowsCorsPolicy",
        policy =>
        {
            policy
                .WithOrigins(
                    "http://localhost:8081"
                )
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    );
});


var app = builder.Build();


// ======================================================
// PIPELINE HTTP
// ======================================================

app.UseHttpsRedirection();

app.UseRouting();


// ======================================================
// CORS DA API
// ======================================================

app.UseCors(
    "SevenShowsCorsPolicy"
);


// ======================================================
// ARQUIVOS ESTÁTICOS
// wwwroot/uploads/...
//
// Importante:
// as imagens do Designer são carregadas pelo Vue
// em http://localhost:8081 e servidas pela API
// em http://localhost:5297.
//
// O header abaixo permite que fetch(), Canvas,
// Konva e MODNet leiam essas imagens.
// ======================================================

app.UseStaticFiles(
    new StaticFileOptions
    {
        OnPrepareResponse = context =>
        {
            context.Context
                .Response
                .Headers[
                    HeaderNames.AccessControlAllowOrigin
                ] =
                "http://localhost:8081";
        }
    }
);


// ======================================================
// OPENAPI / SWAGGER
// ======================================================

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "SevenShows API v1"
        );

        options.RoutePrefix =
            "swagger";
    });
}


// ======================================================
// AUTENTICAÇÃO / AUTORIZAÇÃO
// ======================================================

app.UseAuthentication();

app.UseAuthorization();


// ======================================================
// CONTROLLERS
// ======================================================

app.MapControllers();


// ======================================================
// START
// ======================================================

app.Run();