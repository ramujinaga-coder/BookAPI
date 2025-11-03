FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["BookApi.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build "BookApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BookApi.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN mkdir -p /app/data
ENV ASPNETCORE_URLS=http://+:80
ENTRYPOINT ["dotnet", "BookApi.dll"]