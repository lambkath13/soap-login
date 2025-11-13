FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

EXPOSE 10000
ENV ASPNETCORE_URLS=http://+:10000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["mekashron-soap-login.csproj", "./"]
RUN dotnet restore "mekashron-soap-login.csproj"

COPY . .
RUN dotnet publish "mekashron-soap-login.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "mekashron-soap-login.dll"]
