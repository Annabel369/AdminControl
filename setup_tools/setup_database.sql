-- setup_database.sql
-- Script de configuração automática para o painel CS2 (mariusbd)
-- Criado por Antigravity para Amauri Bueno dos Santos

CREATE DATABASE IF NOT EXISTS mariusbd;
USE mariusbd;

-- Tabela de Administradores
CREATE TABLE IF NOT EXISTS admins (
    steamid BIGINT UNSIGNED PRIMARY KEY,
    name VARCHAR(64),
    permission VARCHAR(64),
    level INT NOT NULL,
    expires_at DATETIME,
    granted_by BIGINT UNSIGNED,
    timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Tabela de Banimentos (SteamID)
CREATE TABLE IF NOT EXISTS bans (
    steamid BIGINT UNSIGNED PRIMARY KEY,
    reason VARCHAR(255),
    unbanned TINYINT(1) NOT NULL DEFAULT 0,
    timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Tabela de Banimentos por IP
CREATE TABLE IF NOT EXISTS ip_bans (
    ip_address VARCHAR(45) PRIMARY KEY,
    reason VARCHAR(255),
    unbanned TINYINT(1) NOT NULL DEFAULT 0,
    timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Tabela de Silenciados (Mutes)
CREATE TABLE IF NOT EXISTS mutes (
    steamid BIGINT UNSIGNED PRIMARY KEY,
    reason VARCHAR(255),
    timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    unmuted TINYINT(1) NOT NULL DEFAULT 0
);

-- Tabela de Usuários do Painel
CREATE TABLE IF NOT EXISTS users (
    id INT AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(50) NOT NULL UNIQUE,
    password VARCHAR(255) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
