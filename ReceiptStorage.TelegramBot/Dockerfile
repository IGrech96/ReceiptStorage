#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
WORKDIR /src
COPY ["ReceiptStorage.TelegramBot/ReceiptStorage.TelegramBot.csproj", "ReceiptStorage.TelegramBot/"]
COPY ["ReceiptStorage/ReceiptStorage.csproj", "ReceiptStorage/"]
RUN dotnet restore "./ReceiptStorage.TelegramBot/ReceiptStorage.TelegramBot.csproj" -a $TARGETARCH

COPY . .
WORKDIR "/src/ReceiptStorage.TelegramBot"
RUN dotnet publish "./ReceiptStorage.TelegramBot.csproj" -a $TARGETARCH --no-restore -c $BUILD_CONFIGURATION -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["./ReceiptStorage.TelegramBot"]