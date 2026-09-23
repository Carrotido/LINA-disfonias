@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0ExportAndRun.ps1"
if errorlevel 1 (
  echo.
  echo La exportacion no pudo completarse. Revisa el mensaje anterior.
  pause
)
endlocal
