@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo.
echo ==============================================
echo   JR Optimizer Pro 2.0 - Publicacao leve
echo ==============================================
echo.
if exist "Publicado" rmdir /s /q "Publicado"
dotnet publish "JROptimizerPro\JROptimizerPro.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:PublishReadyToRun=false -p:DebugType=None -p:DebugSymbols=false -o "Publicado"
if errorlevel 1 (
  echo.
  echo ERRO: a publicacao falhou. Abra o projeto no Visual Studio e use Compilacao ^> Recompilar Solucao.
  pause
  exit /b 1
)
echo.
echo Publicacao concluida.
echo Abra a pasta: %~dp0Publicado
start "" "%~dp0Publicado"
pause
