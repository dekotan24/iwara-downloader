@echo off
REM iwara_setup.bat - Pythonセットアップ（Embeddable Python対応）
REM 使用方法: set PYTHON_PATH=C:\...\python.exe && iwara_setup.bat

setlocal enabledelayedexpansion
chcp 65001 >nul

echo ========================================
echo iwara-downloader Pythonセットアップ
echo ========================================
echo.

REM Pythonパスを確認
if not defined PYTHON_PATH (
    echo PYTHON_PATHが設定されていません。
    where python >nul 2>&1
    if !errorlevel! equ 0 (
        set "PYTHON_PATH=python"
        echo pythonコマンドを使用します。
    ) else (
        echo Pythonが見つかりません。
        exit /b 1
    )
)

REM クォートを削除
set "PYTHON_PATH=%PYTHON_PATH:"=%"
echo Pythonパス: %PYTHON_PATH%
echo.

REM Pythonのディレクトリを取得
for %%I in ("%PYTHON_PATH%") do set "PYTHON_DIR=%%~dpI"
echo Pythonディレクトリ: %PYTHON_DIR%

REM Pythonバージョン確認
echo Pythonバージョンを確認中...
"%PYTHON_PATH%" --version
if %errorlevel% neq 0 (
    echo Pythonの実行に失敗しました。
    exit /b 1
)
echo.

REM Embeddable Python用: python3*._pthファイルを探して修正（全バージョン対応）
set "PTH_FOUND=0"
for %%F in ("%PYTHON_DIR%python3*._pth") do (
    echo Embeddable Pythonを検出しました: %%~nxF
    echo _pthファイルを修正中...
    powershell -Command "(Get-Content '%%F') -replace '#import site', 'import site' | Set-Content '%%F'"
    echo _pth修正完了
    set "PTH_FOUND=1"
    echo.
)

REM pipが存在するか確認
"%PYTHON_PATH%" -m pip --version >nul 2>&1
if %errorlevel% neq 0 (
    echo pipが見つかりません。get-pip.pyをダウンロードしてインストールします...
    
    REM get-pip.pyをダウンロード
    curl -sS https://bootstrap.pypa.io/get-pip.py -o "%PYTHON_DIR%get-pip.py"
    if %errorlevel% neq 0 (
        echo get-pip.pyのダウンロードに失敗しました。
        exit /b 1
    )
    
    REM pipをインストール
    "%PYTHON_PATH%" "%PYTHON_DIR%get-pip.py" --no-warn-script-location
    if %errorlevel% neq 0 (
        echo pipのインストールに失敗しました。
        exit /b 1
    )
    echo pipインストール完了
    echo.
)

REM pipバージョン確認
echo pipバージョン:
"%PYTHON_PATH%" -m pip --version
echo.

REM cloudscraperをインストール
echo cloudscraperをインストール中...
"%PYTHON_PATH%" -m pip install cloudscraper --quiet --no-warn-script-location
if %errorlevel% neq 0 (
    echo cloudscraperのインストールに失敗しました。
    exit /b 1
)

REM インストール確認
echo インストール確認中...
"%PYTHON_PATH%" -c "import cloudscraper; print('cloudscraper OK')"
if %errorlevel% neq 0 (
    echo cloudscraperのインポートに失敗しました。
    exit /b 1
)

echo.
echo ========================================
echo セットアップ完了！
echo ========================================
echo.
echo Python: %PYTHON_PATH%
echo.

REM 完了マーカーファイルを作成
echo %date% %time% > "%~dp0.python_setup_done"

exit /b 0
