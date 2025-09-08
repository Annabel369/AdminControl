# AdminControlPlugin
<img width="1770" height="986" alt="image" src="https://github.com/user-attachments/assets/51a8073f-c42e-4770-a397-bc255c04bc15" />

Admin Control with MySQL & CFG Sync for Counter-Strike 2

https://github.com/Annabel369/AdminControl_PHP_PAINEL/tree/main

mariadb mariadb-11.3.2-winx64.msi https://mariadb.org/download/

PhP php-8.4.12-Win32-vc15-x64.zip https://php.watch/versions/8.4/releases/8.4.12

3 Apache httpd-2.4.59-240404-win64-VS17.zip https://www.apachelounge.com/download/

https://learn.microsoft.com/pt-br/cpp/windows/latest-supported-vc-redist?view=msvc-170

C:\Apache24\bin\httpd.exe -k install


	httpd.exe -k start
	httpd.exe -k stop
	ApacheMonitor.exe
	WEB FILES http://localhost C:\Apache24\htdocs
 
PhpmyAdmin phpMyAdmin-5.2.1-all-languages.zip https://www.phpmyadmin.net/

	WEB FILES http://localhost/PhpmyAdmin C:\Apache24\htdocs\PhpmyAdmin\
	edit or creat C:\Apache24\htdocs\phpMyAdmin\config.inc.php
 	creat  http://localhost/PhpmyAdmin/setup donwload config.inc.php
  	Add C:\Apache24\htdocs\phpMyAdmin\config.inc.php


AdminControlPlugin is a comprehensive Counter-Strike 2 plugin that provides robust administrative controls, including player and IP banning, muting, and RCON execution. It seamlessly integrates with a MySQL database for persistent storage and synchronizes with native server configuration files. The plugin's modular design makes it easy to maintain and expand.


# 🚀 Features
MySQL Database Integration: All admin, ban, and mute data is stored persistently in a MySQL database.


Configurable Commands: A wide range of commands for managing players, admins, and server settings.


Localization Support: Easily translate all in-game and console messages using simple JSON files.


Modular Architecture: The code is split into separate, logical files (Main.cs, AdminCommands.cs, BanCommands.cs, etc.) for improved readability and maintainability.


In-Game Admin Menu: An interactive chat menu for performing administrative actions without needing to use console commands.


Auto-Sync: Admins, bans, and mutes are automatically synchronized with the server's native banned_user.cfg and banned_ip.cfg files.


# 📦 Installation

Prerequisites


A running Counter-Strike 2 server with a compatible version of CounterStrikeSharp.


A MySQL database server.


(Optional) CS2MenuManager.API for the in-game admin menu.


Steps


Download the Plugin: Obtain the latest release of AdminControlPlugin from your preferred source.


Move Files: Place the plugin files and folders into your server's csgo/addons/counterstrikesharp/plugins/ directory. The structure should look like this:

			└── plugins/
				└── AdminControlPlugin/
					├─── commands/
					│    ├─── Admin.cs
					│    ├─── Ban.cs
					│    ├─── Mute.cs
					│    └─── Player.cs
					├─── lang/
					│    ├─── en.json
					│    └─── pt.json
					├─── Config.cs
					└─── AdminControlPlugin.cs


Configure MySQL: Open the Config.cs file and update the MySQL connection details to match your database server.

# 🛠️ Configuration
The plugin's configuration is managed through two main methods: the Config.cs file and the language files in the lang directory.


Config.cs


This file contains the core settings for the plugin, such as MySQL connection details.

			public class AdminControlConfig : BasePluginConfig
			{
				[JsonPropertyName("MySQLHost")]
				public string Host { get; set; } = "localhost";

				[JsonPropertyName("MySQLUser")]
				public string User { get; set; } = "root";

				[JsonPropertyName("MySQLPassword")]
				public string Password { get; set; } = "0073007";

				[JsonPropertyName("MySQLDatabase")]
				public string Database { get; set; } = "mariusbd";

				[JsonPropertyName("RequiredFlags")]
				public List<string> RequiredFlags { get; set; } = new List<string> { "@css/root", "@css/ban" };
			}


RequiredFlags: This list defines the permissions an administrator needs to be able to use the plugin's commands.


Language Files


All user-facing text, including chat messages and log entries, can be edited in the lang/en.json (or pt.json) file.


Example en.json snippet:

			{
			"plugin_loaded": "Plugin loaded successfully!",
			"kick_ban_message": "You have been banned from this server! Reason: {0}",
			"menu_admin_title": "👮 Admin Menu",
			"player_kicked": "✅ {0} has been kicked."
			}

			You can customize these strings to fit your server's needs or add new language files for more translation options.

			🎮 Commands
			The plugin adds several commands that can be used by authorized administrators.

			Command

			Description

			Permission Required

			css_menu

			Opens the main admin menu.

			@css/admin

			!adminmenu

			Opens the main admin menu from chat.

			@css/admin

			css_ban

			Bans a player by their SteamID64.

			@css/ban

			css_unban

			Unbans a player by their SteamID64.

			@css/unban

			css_ipban

			Bans a player's IP address.

			@css/ban

			css_unbanip

			Unbans an IP address.

			@css/unban

			css_mute

# Mutes a player by name or SteamID.

		@css/chat

		css_unmute

#Unmutes a player.

		@css/chat

		!kick

#Kicks a player from the server.

		@css/kick

		!swapteam

#Moves a player to the other team.

		@css/slay

		css_addadmin

#Grants a custom admin permission.

		@css/root

		css_removeadmin

#Removes an admin by SteamID64.

		@css/root

		css_reloadadmins

#Reloads the admin list from the database.

		@css/root

		css_rcon

		Executes an RCON command on the server.

		@css/rcon

#🤝 Contributing
We welcome contributions! If you encounter any bugs, have a feature request, or want to contribute to the code, please feel free to open an issue or submit a pull request on the project's GitHub repository.

#📄 License
This project is licensed under the MIT License. See the LICENSE file for details.
