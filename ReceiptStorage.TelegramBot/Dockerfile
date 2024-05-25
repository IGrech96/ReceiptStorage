#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER app
WORKDIR /app

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ReceiptStorage.TelegramBot/ReceiptStorage.TelegramBot.csproj", "ReceiptStorage.TelegramBot/"]
COPY ["ReceiptStorage/ReceiptStorage.csproj", "ReceiptStorage/"]
RUN dotnet restore "./ReceiptStorage.TelegramBot/ReceiptStorage.TelegramBot.csproj"
COPY . .
WORKDIR "/src/ReceiptStorage.TelegramBot"
RUN dotnet build "./ReceiptStorage.TelegramBot.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ReceiptStorage.TelegramBot.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ReceiptStorage.TelegramBot.dll"]