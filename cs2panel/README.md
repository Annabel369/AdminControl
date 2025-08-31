Control Panel for CS2 Servers
<img width="1854" height="896" alt="image" src="https://github.com/user-attachments/assets/43ad1a39-b5b0-4aa5-a416-aac37e3a8411" />

This project is a lightweight and secure control panel, designed to remotely manage Counter-Strike 2 (CS2) servers via RCON. The panel includes user authentication features, registration control, and a user-friendly interface to send commands to the server.

Core Features
Login and Registration System: Securely authenticate users using encrypted passwords (hashing). New user registration can be enabled or disabled by an administrator, with success and error messages in different languages.

Secure Access: The main page (index.php) is protected by a session system that requires a login. If the user is not authenticated, they are redirected to the login page.

RCON Connection: Send RCON commands to the CS2 server. The interface displays the server's response with icons that indicate the command status (success, error, information).

Integrated Database: The panel connects to a MySQL database to manage user information, such as usernames and passwords. The users table is created automatically if it does not exist.

Internationalization (i18n): The panel automatically detects the user's browser language and displays messages in Portuguese or English, using translation files (pt.json and en.json).

Responsive and Modern Design: The login page layout has been optimized to be compact and visually consistent with the rest of the panel.

Project Structure
index.php: The main panel page, protected by login.

login.php: The login page and authentication logic.

register.php: The page for new user registration, with activation/deactivation control.

db_connect.php: A configuration file that establishes the database connection via PDO and defines global settings such as registration activation.

api.php: API to get administration data from the database (admins, bans, mutes, etc.).

rcon2.php: Script for communication with the CS2 server via RCON.

style.css: Stylesheet for the panel's visual design.

lang/: Folder containing the translation files (pt.json and en.json).

img/: Folder to store project images and icons.

Setup and Usage
Configure the Database: Edit the db_connect.php file with your MySQL credentials ($host, $db, $user, $pass).

Enable Registration: In the db_connect.php file, set $allow_registration = true; to allow new registrations. After the first registration, it is recommended to set the value back to false.


Access: Access login.php in your browser. 
