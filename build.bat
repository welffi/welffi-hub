@echo off
"C:\Program Files\dotnet\dotnet.exe" publish "%~dp0src\whub\whub.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:PublishReadyToRun=true -o "%~dp0dist"
