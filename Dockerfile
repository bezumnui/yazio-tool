FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/YazioTool.Core/YazioTool.Core.csproj src/YazioTool.Core/
COPY src/YazioTool.Mcp/YazioTool.Mcp.csproj src/YazioTool.Mcp/
RUN dotnet restore src/YazioTool.Mcp/YazioTool.Mcp.csproj
COPY src/YazioTool.Core/ src/YazioTool.Core/
COPY src/YazioTool.Mcp/ src/YazioTool.Mcp/
RUN dotnet publish src/YazioTool.Mcp/YazioTool.Mcp.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "YazioTool.Mcp.dll"]
