<?php
/**
 * Classe Rcon para comunicação com servidores Source Engine (CS2, CS:GO, etc.)
 * Desenvolvida por Amauri Bueno dos Santos com suporte técnico da Copilot (Microsoft)
 * Última atualização: 28/08/2025
 *
 * Esta classe permite autenticação via RCON, envio de comandos e leitura de respostas.
 * Foi refinada para funcionar com servidores CS2 modernos, incluindo suporte a pacotes múltiplos e respostas limpas.
 */

class Rcon
{
    // Configurações básicas
    private $host;
    private $port;
    private $password;
    private $timeout;

    // Estado interno
    private $socket;
    private $authorized = false;
    private $last_response;

    // Constantes do protocolo RCON
    const PACKET_AUTHORIZE = 5;
    const PACKET_COMMAND = 6;

    const SERVERDATA_AUTH = 3;
    const SERVERDATA_AUTH_RESPONSE = 2;
    const SERVERDATA_EXECCOMMAND = 2;
    const SERVERDATA_RESPONSE_VALUE = 0;

    /**
     * Construtor da classe
     * @param string $host IP ou hostname do servidor
     * @param int $port Porta RCON (geralmente igual à porta do jogo)
     * @param string $password Senha RCON definida no servidor
     * @param int $timeout Tempo limite de conexão
     */
    public function __construct($host, $port, $password, $timeout)
    {
        $this->host = $host;
        $this->port = $port;
        $this->password = $password;
        $this->timeout = $timeout;
    }

    /**
     * Retorna a última resposta recebida do servidor
     */
    public function get_response()
    {
        return $this->last_response;
    }

    /**
     * Estabelece conexão com o servidor e realiza autenticação RCON
     */
    public function connect()
    {
        @$this->socket = fsockopen($this->host, $this->port, $errno, $errstr, $this->timeout);

        if (!$this->socket) {
            $this->last_response = $errstr;
            return false;
        }

        stream_set_timeout($this->socket, 3, 0);
        return $this->authorize();
    }

    /**
     * Encerra a conexão com o servidor
     */
    public function disconnect()
    {
        if ($this->socket) {
            fclose($this->socket);
        }
    }

    /**
     * Verifica se a conexão está autorizada
     */
    public function is_connected()
    {
        return $this->authorized;
    }

    /**
     * Envia um comando RCON e retorna a resposta do servidor
     * @param string $command Comando a ser executado
     * @return string|false Resposta do servidor ou false em caso de erro
     */
    public function send_command($command)
    {
        if (!$this->is_connected()) {
            return false;
        }

        $packet_id = rand(1000, 9999);
        $this->write_packet($packet_id, self::SERVERDATA_EXECCOMMAND, $command);
        $this->write_packet($packet_id, self::SERVERDATA_RESPONSE_VALUE, '');

        $response = '';
        for ($i = 0; $i < 10; $i++) {
            $packet = $this->read_packet();
            if ($packet['body'] === '' || $packet['body'] === "\x01") {
                break;
            }
            $response .= $packet['body'];
        }

        $this->last_response = $response;
        return $response;
    }

    /**
     * Realiza autenticação com o servidor usando a senha RCON
     */
    private function authorize()
    {
        $packet_id = rand(1, 999);
        $this->write_packet($packet_id, self::SERVERDATA_AUTH, $this->password);

        $auth_success = false;
        for ($i = 0; $i < 2; $i++) {
            $packet = $this->read_packet();
            if ($packet['type'] == self::SERVERDATA_AUTH_RESPONSE && $packet['id'] == $packet_id) {
                $auth_success = true;
                break;
            }
        }

        if ($auth_success) {
            $this->authorized = true;
            return true;
        }

        $this->disconnect();
        return false;
    }

    /**
     * Monta e envia um pacote RCON para o servidor
     */
    private function write_packet($packet_id, $packet_type, $packet_body)
    {
        $packet = pack("VV", $packet_id, $packet_type) . $packet_body . "\x00\x00";
        $packet_size = strlen($packet);
        $packet = pack("V", $packet_size) . $packet;
        fwrite($this->socket, $packet);
    }

    /**
     * Lê um pacote RCON da resposta do servidor
     * @return array Pacote com campos: id, type, body
     */
    private function read_packet()
    {
        $size_data = fread($this->socket, 4);
        if (strlen($size_data) < 4) {
            return ['id' => -1, 'type' => -1, 'body' => ''];
        }

        $size = unpack("V", $size_data)[1];
        $packet_data = fread($this->socket, $size);
        if (strlen($packet_data) < $size) {
            return ['id' => -1, 'type' => -1, 'body' => ''];
        }

        return unpack("V1id/V1type/a*body", $packet_data);
    }
}?>