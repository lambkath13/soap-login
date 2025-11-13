using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(o => o.AddPolicy("AllowAll",
    p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    o.SerializerOptions.WriteIndented = false;
});

var app = builder.Build();
app.UseCors("AllowAll");

var soapEndpoint = "http://isapi.mekashron.com/icu-tech/icutech-test.dll";

app.MapGet("/", () => Results.Ok(new
{
    name = "mekashron-soap-login backend",
    status = "ok",
    endpoints = new[] { "GET /health", "POST /api/auth/login", "POST /api/auth/register" }
}));

app.MapGet("/health", () => new { status = "ok" });

app.MapPost("/api/auth/login", async (HttpContext ctx) =>
{
    var dto = await ctx.Request.ReadFromJsonAsync<LoginDto>();
    if (dto is null) return Results.BadRequest(new { ok = false, error = "Empty body" });

    var soap = new Mekashron.Soap.SoapClient(soapEndpoint);
    var (ok, message, payload) = await soap.CallManyAsync(
        "Login",
        new[]
        {
            Mekashron.Soap.SoapEnvelopeTemplates.Login_v1(dto.Username, dto.Password),
            Mekashron.Soap.SoapEnvelopeTemplates.Login_v2(dto.Username, dto.Password),
        }
    );

    return ok ? Results.Ok(new { ok, message, payload })
              : Results.BadRequest(new { ok, message, payload });
});

app.MapPost("/api/auth/register", async (HttpContext ctx) =>
{
    var dto = await ctx.Request.ReadFromJsonAsync<RegisterDto>();
    if (dto is null) return Results.BadRequest(new { ok = false, error = "Empty body" });

    var soap = new Mekashron.Soap.SoapClient(soapEndpoint);
    var (ok, message, payload) = await soap.CallManyAsync(
        "RegisterNewCustomer",
        new[]
        {
            Mekashron.Soap.SoapEnvelopeTemplates.Register_v1(
                dto.Username, dto.Password, dto.Email, dto.FirstName, dto.LastName, dto.Mobile),
            Mekashron.Soap.SoapEnvelopeTemplates.Register_v2(
                dto.Username, dto.Password, dto.Email, dto.FirstName, dto.LastName, dto.Mobile),
        }
    );

    return ok ? Results.Ok(new { ok, message, payload })
              : Results.BadRequest(new { ok, message, payload });
});

app.Run();

public record LoginDto(string Username, string Password);
public record RegisterDto(string Username, string Password, string Email, string FirstName, string LastName, string Mobile);

namespace Mekashron.Soap { }
