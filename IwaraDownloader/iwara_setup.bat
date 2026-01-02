@echo off
REM iwara_setup.bat - Python Setup (Embeddable Python Support)
REM Usage: set PYTHON_PATH=C:¥...¥python.exe && iwara_setup.bat

setlocal enabledelayedexpansion

echo ========================================
echo iwara-downloader Python Setup
echo ========================================
echo.

REM Check Python path
if not defined PYTHON_PATH (
    echo PYTHON_PATH is not set.
    where python >nul 2>&1
    if !errorlevel! equ 0 (
        set "PYTHON_PATH=python"
        echo Using python command.
    ) else (
        echo Python not found.
        exit /b 1
    )
)

REM Remove quotes
set "PYTHON_PATH=%PYTHON_PATH:"=%"
echo Python path: %PYTHON_PATH%
echo.

REM Get Python directory
for %%I in ("%PYTHON_PATH%") do set "PYTHON_DIR=%%‾dpI"
echo Python directory: %PYTHON_DIR%

REM Check Python version
echo Checking Python version...
"%PYTHON_PATH%" --version
if %errorlevel% neq 0 (
    echo Failed to execute Python.
    exit /b 1
)
echo.

REM For Embeddable Python: Find and modify python3*._pth file (all versions)
set "PTH_FOUND=0"
for %%F in ("%PYTHON_DIR%python3*._pth") do (
    echo Embeddable Python detected: %%‾nxF
    echo Modifying _pth file...
    powershell -Command "(Get-Content '%%F') -replace '#import site', 'import site' | Set-Content '%%F'"
    echo _pth file modified.
    set "PTH_FOUND=1"
    echo.
)

REM Check if pip exists
"%PYTHON_PATH%" -m pip --version >nul 2>&1
if %errorlevel% neq 0 (
    echo pip not found. Downloading and installing get-pip.py...
    
    REM Download get-pip.py
    curl -sS https://bootstrap.pypa.io/get-pip.py -o "%PYTHON_DIR%get-pip.py"
    if %errorlevel% neq 0 (
        echo Failed to download get-pip.py.
        exit /b 1
    )
    
    REM Install pip
    "%PYTHON_PATH%" "%PYTHON_DIR%get-pip.py" --no-warn-script-location
    if %errorlevel% neq 0 (
        echo Failed to install pip.
        exit /b 1
    )
    echo pip installed successfully.
    echo.
)

REM Check pip version
echo pip version:
"%PYTHON_PATH%" -m pip --version
echo.

REM Install cloudscraper
echo Installing cloudscraper...
"%PYTHON_PATH%" -m pip install cloudscraper --quiet --no-warn-script-location
if %errorlevel% neq 0 (
    echo Failed to install cloudscraper.
    exit /b 1
)

REM Verify installation
echo Verifying installation...
"%PYTHON_PATH%" -c "import cloudscraper; print('cloudscraper OK')"
if %errorlevel% neq 0 (
    echo Failed to import cloudscraper.
    exit /b 1
)

echo.
echo ========================================
echo Setup completed successfully!
echo ========================================
echo.
echo Python: %PYTHON_PATH%
echo.

REM Create completion marker file
echo %date% %time% > "%‾dp0.python_setup_done"

exit /b 0
