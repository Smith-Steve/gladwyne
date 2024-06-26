using Gladwyne.API.Interfaces;
using Gladwyne.API.Data;
using Gladwyne.API.Services;
using Gladwyne.API.Data.DataProviders;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Gladwyne.API.Data.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors((options) => 
{
    options.AddPolicy("DevCors", (corsBuilder) => 
    {
        corsBuilder.WithOrigins("http://localhost:4200", "http://localhost:3000", "http://localhost:8000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
    options.AddPolicy("ProdCors", (corsBuilder) => 
    {
        corsBuilder.WithOrigins("https://Gladwyne.com")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

//This is linking our IUserRepository Interface as a scoped connection to UserRepository.
//This gives us access to the methods in UserRepository without actually using UserRepository.
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IOrganizationService, OrganizationService>();
// builder.Services.AddSingleton<IDataProvider, SqlDataProvider>();
string? tokenKeyString = builder.Configuration.GetSection("AppSettings:TokenKey").Value;

SymmetricSecurityKey tokenKey = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(
        tokenKeyString != null ? tokenKeyString : ""
    )
);
TokenValidationParameters tokenValidationParameters = new TokenValidationParameters()
{
    //The settings of the token validation options.
    IssuerSigningKey = tokenKey,
    ValidateIssuer = false,
    ValidateIssuerSigningKey = false,
    ValidateAudience = false,
};

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        //This is where we set our token validation options.
        options.TokenValidationParameters = tokenValidationParameters;
    });
var application = builder.Build();

// Configure the HTTP request pipeline.
if (application.Environment.IsDevelopment())
{
    application.UseCors("DevCors");
    application.UseSwagger();
    application.UseSwaggerUI();
}
else
{
    application.UseCors("ProdCors");
    application.UseHttpsRedirection();
}

application.MapControllers();

application.Run();