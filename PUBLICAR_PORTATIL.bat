@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo.
echo ==================================================
echo   JR Optimizer Pro 2.0 - EXE portatil completo
echo ==================================================
echo.
if exist "Publicado-Portatil" rmdir /s /q "Publicado-Portatil"
dotnet publish "JROptimizerPro\JROptimizerPro.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:PublishReadyToRun=false -p:DebugType=None -p:DebugSymbols=false -o "Publicado-Portatil"
if errorlevel 1 (
  echo.
  echo ERRO: a publicacao falhou. Verifique a conexao e os componentes do .NET 8 no Visual Studio.
  pause
  exit /b 1
)
echo.
echo Publicacao concluida.
start "" "%~dp0Publicado-Portatil"
pause
