# 🔧 AdminControl
AdminControl é um sistema básico de controle administrativo para servidores de Counter-Strike 2 (CS2), projetado para facilitar a depuração e o gerenciamento de permissões e comandos.

🖥️ Fiz uma página PHP simples e de fácil acesso.

⚙️ Basta configurar o banco de dados no arquivo .

✅ É extremamente fácil de usar — ideal até para quem tem pouca experiência.

🔌 É só ligar e está funcionando

<img width="1899" height="1079" alt="image" src="https://github.com/user-attachments/assets/ee47ace9-a8e6-4fa3-b161-7575d8a4296c" />

<img width="1919" height="1079" alt="image" src="https://github.com/user-attachments/assets/ca234267-2ce9-4ef6-bc27-e86a09c51610" />



Exemplos de Uso dos Comandos
1. css_ban
Função: Bane um jogador usando seu SteamID64 e um motivo.

Exemplo:
```
css_ban 76561198123456789 "Uso de hack"
```

76561198123456789: O SteamID64 do jogador a ser banido.

"Uso de hack": O motivo do banimento. Use aspas se o motivo contiver mais de uma palavra.

2. css_unban
Função: Desbane um jogador que está banido.

Exemplo:
```
css_unban 76561198123456789
```

3. css_listbans
Função: Lista todos os jogadores banidos que estão no banco de dados.

Exemplo:
```
css_listbans
```

Este comando exibirá no console do servidor uma lista com o SteamID, motivo e data de cada banimento ativo.

4. css_rcon
Função: Executa um comando RCON.

Exemplos:

Comando Simples:
```
css_rcon sv_cheats 1
```

Comando com Múltiplas Palavras:
```
css_rcon say "Olá a todos!"
```

5. css_admin
Função: Concede um admin básico a um jogador.

Exemplo:
```
css_admin 76561198906880449
```

Isto adicionará o SteamID 76561198906880449 à tabela de admins com permissões básicas.

6. css_removeadmin
Função: Remove um jogador da lista de administradores.

Exemplo:
```
css_removeadmin 76561198906880449
```

7. css_addadmin
Função: Concede um admin personalizado com nome, permissão, nível e duração.

Exemplos:

Admin Customizado (com tempo):
```
css_addadmin 76561198906880449 Katara @css/custom-permission 40 40000
```

76561198906880449: SteamID64 do admin.

Katara: O nome do admin.

@css/custom-permission: A permissão personalizada.

40: O nível do admin.

40000: Duração em minutos.

Admin Master:
```
css_addadmin 76561199737411180 Astral2 @css/root 99 99999
```

@css/root: Geralmente significa permissão total.

99: Um nível alto.

99999: Uma duração muito longa (quase permanente).
