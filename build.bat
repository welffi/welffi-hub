@echo off
set PATH=%USERPROFILE%\.cargo\bin;%PATH%
cd /d "%~dp0"
cargo build --release || exit /b 1
if not exist dist mkdir dist
copy /y "target\release\welffi_hub.exe" "dist\welffi hub.exe" >nul || (echo Close welffi hub first, then run this again & exit /b 1)
echo Built dist\welffi hub.exe
