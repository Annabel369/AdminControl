#!/bin/bash

# Exit on error
set -e

# Target directory (optional)
TARGET_DIR="$1"

# Adiciona o diretório local do .NET (instalado via script) no início do PATH, se existir.
# Isso evita usar o runtime global (/usr/bin/dotnet) que não possui o SDK.
if [ -d "$HOME/.dotnet" ]; then
    export DOTNET_ROOT="$HOME/.dotnet"
    export PATH="$HOME/.dotnet:$PATH"
fi

echo "=== Compilando o plugin AdminControlPlugin ==="

# Verifica se o dotnet está no PATH e se há algum SDK instalado
if ! command -v dotnet &> /dev/null || [ -z "$(dotnet --list-sdks)" ]; then
    echo "Erro: O .NET SDK não foi encontrado no sistema (apenas o runtime ou nenhum .NET está instalado)."
    echo "Para compilar o plugin, você precisa instalar o .NET SDK 8.0."
    echo "No Debian/Ubuntu, você pode instalar rodando:"
    echo "  sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0"
    exit 1
fi

# Compila e publica
echo "Compilando..."
dotnet publish -c Release

echo "=== Compilação concluída! ==="
echo "Saída em: bin/Release/net8.0/publish/AdminControlPlugin/"

if [ -n "$TARGET_DIR" ]; then
    if [ -d "$TARGET_DIR" ]; then
        echo "Copiando arquivos compilados para: $TARGET_DIR"
        cp -r bin/Release/net8.0/publish/AdminControlPlugin/* "$TARGET_DIR/"
        echo "Cópia concluída!"
    else
        echo "Aviso: O diretório de destino '$TARGET_DIR' não existe. Pulando cópia."
    fi
fi
