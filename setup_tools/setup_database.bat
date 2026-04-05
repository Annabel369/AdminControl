@echo off
setlocal
title Instalador de Banco de Dados CS2 (mariusbd)
echo ====================================================
echo   CONFIGURACAO DO BANCO DE DADOS CS2 (mariusbd)     
echo ====================================================
echo.

set /p MYSQL_PASS="Digite a senha do usuario ROOT do MySQL (se nao tiver, de ENTER): "

:: Tentar encontrar o MySQL em locais comuns
set "MYSQL_BIN="
if exist "C:\xampp\mysql\bin\mysql.exe" set "MYSQL_BIN=C:\xampp\mysql\bin\mysql.exe"
if exist "C:\Program Files\MariaDB 12.2\bin\mysql.exe" set "MYSQL_BIN=C:\Program Files\MariaDB 12.2\bin\mysql.exe"

if "%MYSQL_BIN%" == "" (
    mysql --version >nul 2>&1
    if %ERRORLEVEL% equ 0 (
        set "MYSQL_BIN=mysql"
    ) else (
        echo [ERRO] Nao consegui encontrar o MySQL.exe automaticamente.
        echo Por favor, certifique-se de que o MySQL esta no seu PATH ou instalado no XAMPP/MariaDB padrao.
        pause
        exit /b
    )
)

echo.
echo Executando scripts SQL...
echo.

:: Executar o comando SQL. Se a senha for vazia, não usa o parâmetro -p
if "%MYSQL_PASS%" == "" (
    "%MYSQL_BIN%" -u root < "%~dp0setup_database.sql"
) else (
    "%MYSQL_BIN%" -u root -p%MYSQL_PASS% < "%~dp0setup_database.sql"
)

if %ERRORLEVEL% equ 0 (
    echo.
    echo ====================================================
    echo   SUCESSO! O banco mariusbd e as tabelas foram      
    echo   criadas ou ja existem.                            
    echo ====================================================
) else (
    echo.
    echo [ERRO] Falha ao configurar o banco de dados. 
    echo Verifique se a senha esta correta e se o servico do MySQL esta rodando.
)

echo.
pause
