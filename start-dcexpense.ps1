# PowerShell Launcher script for DCExpense Backend API
$env:DOTNET_ROOT = "C:\Users\noadv\AppData\Local\Microsoft\dotnet"
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$env:ASPNETCORE_ENVIRONMENT = "Development"

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "   Starting DCExpense Web SPA & API on http://localhost:5002" -ForegroundColor Cyan
Write-Host "   Swagger UI: http://localhost:5002/swagger" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Green
dotnet run --project "D:\Agy\DCExpense\DCExpense.WebApi\DCExpense.WebApi.csproj" --urls "http://localhost:5002;https://localhost:7002"
