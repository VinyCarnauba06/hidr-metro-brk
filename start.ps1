$env:PATH = "C:\Users\vinyc\.dotnet;" + $env:PATH

Write-Host "Subindo API..." -ForegroundColor Cyan
$api = Start-Process powershell -ArgumentList "-NoExit", "-Command", "
  `$env:PATH = 'C:\Users\vinyc\.dotnet;' + `$env:PATH;
  `$env:ASPNETCORE_ENVIRONMENT = 'Development';
  cd 'C:\Dev\hidrometro-brk\backend\src\HidrometroApp.Api';
  dotnet run
" -PassThru

Start-Sleep -Seconds 8

Write-Host "Subindo Web..." -ForegroundColor Cyan
$web = Start-Process powershell -ArgumentList "-NoExit", "-Command", "
  `$env:PATH = 'C:\Users\vinyc\.dotnet;' + `$env:PATH;
  `$env:ASPNETCORE_URLS = 'http://localhost:5001';
  `$env:API_URL = 'http://localhost:5000';
  cd 'C:\Dev\hidrometro-brk\web';
  dotnet run
" -PassThru

Start-Sleep -Seconds 6

Write-Host ""
Write-Host "Pronto!" -ForegroundColor Green
Write-Host "Dashboard: http://localhost:5001/Auth/Login" -ForegroundColor White
Write-Host "Swagger:   http://localhost:5000/swagger" -ForegroundColor White

Start-Process "http://localhost:5001/Auth/Login"
