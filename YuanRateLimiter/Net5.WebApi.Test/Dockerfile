#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Net5.WebApi.Test/Net5.WebApi.Test.csproj", "Net5.WebApi.Test/"]
RUN dotnet restore "./Net5.WebApi.Test/./Net5.WebApi.Test.csproj"
COPY . .
WORKDIR "/src/Net5.WebApi.Test"
RUN dotnet build "./Net5.WebApi.Test.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Net5.WebApi.Test.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Net5.WebApi.Test.dll"]