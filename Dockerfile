FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ShopAPI.sln ./
COPY ShopAPI.API/ShopAPI.API.csproj ShopAPI.API/
COPY ShopAPI.Application/ShopAPI.Application.csproj ShopAPI.Application/
COPY ShopAPI.Domain/ShopAPI.Domain.csproj ShopAPI.Domain/
COPY ShopAPI.Infrastructure/ShopAPI.Infrastructure.csproj ShopAPI.Infrastructure/
RUN dotnet restore ShopAPI.API/ShopAPI.API.csproj
COPY . .
RUN dotnet publish ShopAPI.API/ShopAPI.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN mkdir -p /app/logs
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ShopAPI.API.dll"]
