@echo off
rem The gym command. Try: .\gym help
set "GYM_ROOT=%~dp0"
dotnet run "%~dp0tools\gym.cs" -- %*
