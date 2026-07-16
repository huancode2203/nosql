FROM mongo:7.0 AS mongotools
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY EduManageLms.Api.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
COPY --from=mongotools /usr/bin/mongodump /usr/local/bin/mongodump
COPY --from=mongotools /usr/bin/mongorestore /usr/local/bin/mongorestore
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet","EduManageLms.Api.dll"]
