'use strict';

// Timing mechanism used to trigger end of game session. Defines how long, in milliseconds, between each tick in the example tick loop
const tickTime = 500;

// Defines how to long to wait in Seconds before beginning early termination check in the example tick loop
const minimumElapsedTime = 120;

var session;                        // The Realtime server session object
var logger;                         // Log at appropriate level via .info(), .warn(), .error(), .debug()
var startTime;                      // Records the time the process started
var activePlayers = 0;              // Records the number of connected players
var onProcessStartedCalled = false; // Record if onProcessStarted has been called
var scores = [0, 0];

//Informa ao cliente se ele é o jogador 1 ou 2
const SET_PLAYER_NUMBER = 111;
//Informa o cliente de que ele foi aceito na sessão
const OP_CODE_PLAYER_ACCEPTED = 113;
//Avisa que o outro jogador foi desconectado da partida
const OP_CODE_DISCONNECT_NOTIFICATION = 114;
//Informa que a partida/round pode começar
const MATCH_READY = 115;
//Utilizado pelos clientes para informar a movimentação do jogador
const PLAYER_MOVEMENT = 116;
//Clientes informam que um dos jogadores fez um ponto
const ADD_POINT = 117;
//Utilizado pelos clientes para informar a colisão da bola na raquete
const BALL_COLLISION = 118;
//Informa aos clientes o término da partida
const GAME_FINISH = 119;

const TOTAL_PLAYER_COUNT = 2;
const MAX_SCORE = 3;


function startGame() {
    //envia mensagem informando os clientes quem é o jogador 1 e quem é o 2
    session.getPlayers().forEach((player, playerId) => {
        let gameMessage = session.newTextGameMessage(SET_PLAYER_NUMBER, session.getServerId(), playerId.toString());
        session.sendReliableMessage(gameMessage, playerId);
    });
    //envia mensagem informando os clientes que devem iniciar a partida
    session.getPlayers().forEach((player, playerId) => {
        let gameMessage = session.newTextGameMessage(MATCH_READY, session.getServerId(), "0:0");
        session.sendReliableMessage(gameMessage, playerId);
    });
}

function stopGame() {    
    activePlayers = 0;
    if(session != null)
    {
        // processEnding will stop this instance of the game running
        // and will tell the game session to terminate
        session.processEnding().then(function(outcome) {
            session.getLogger().info("Completed process ending with: " + outcome);
            process.exit(0);
        });
    }
}

// Called when game server is initialized, passed server's object of current session
function init(rtSession) {
    session = rtSession;
    logger = session.getLogger();
}

// On Process Started is called when the process has begun and we need to perform any
// bootstrapping.  This is where the developer should insert any code to prepare
// the process to be able to host a game session, for example load some settings or set state
//
// Return true if the process has been appropriately prepared and it is okay to invoke the
// GameLift ProcessReady() call.
function onProcessStarted(args) {
    onProcessStartedCalled = true;
    logger.info("Starting process with args: " + args);
    logger.info("Ready to host games...");

    return true;
}

// Called when a new game session is started on the process
function onStartGameSession(gameSession) {
    // Complete any game session set-up

    // Set up an example tick loop to perform server initiated actions
    startTime = getTimeInS();
    tickLoop();
}

// On Player Connect is called when a player has passed initial validation
// Return true if player should connect, false to reject
function onPlayerConnect(connectMsg) {
    // Perform any validation needed for connectMsg.payload, connectMsg.peerId
    return true;
}

// Called when a Player is accepted into the game
function onPlayerAccepted(player) {
    // This player was accepted -- let's send them a message
    const msg = session.newTextGameMessage(OP_CODE_PLAYER_ACCEPTED, player.peerId,
                                             "Peer " + player.peerId + " accepted");
    session.sendReliableMessage(msg, player.peerId);    
    activePlayers++;
    if (activePlayers == TOTAL_PLAYER_COUNT) {
        startGame();
    }
}

// On Player Disconnect is called when a player has left or been forcibly terminated
// Is only called for players that actually connected to the server and not those rejected by validation
// This is called before the player is removed from the player list
function onPlayerDisconnect(peerId) {
    // send a message to each remaining player letting them know about the disconnect
    const outMessage = session.newTextGameMessage(OP_CODE_DISCONNECT_NOTIFICATION,
                                                session.getServerId(),
                                                "Peer " + peerId + " disconnected");
    session.getPlayers().forEach((player, playerId) => {
        if (playerId != peerId) {
            session.sendReliableMessage(outMessage, playerId);
        }
    });
    stopGame();
}

//recebe mensagens
function onMessage(gameMessage) {
    //converte para string o conteúdo da mensagem 
    var payload = String.fromCharCode.apply(String, gameMessage.payload);

    switch (gameMessage.opCode) {
        //atualiza a pontuação da partida caso 
        //o código da mensagem seja o de adicionar ponto
        case ADD_POINT: {
            updateScore(parseInt(payload));            
        }
    }
}

// A simple tick loop example
// Checks to see if a minimum amount of time has passed before seeing if the game has ended
async function tickLoop() {
    const elapsedTime = getTimeInS() - startTime;
    logger.info("Tick... " + elapsedTime + " activePlayers: " + activePlayers);

    // In Tick loop - see if all players have left early after a minimum period of time has passed
    // Call processEnding() to terminate the process and quit
    if ( (activePlayers == 0) && (elapsedTime > minimumElapsedTime)) {
        logger.info("All players disconnected. Ending game");
        const outcome = await session.processEnding();
        logger.info("Completed process ending with: " + outcome);
        process.exit(0);
    }
    else {
        setTimeout(tickLoop, tickTime);
    }
}

function updateScore(playerScore) {
    scores[playerScore]++;
    logger.info("Score: " + scores.toString() + "playerScore: " + playerScore.toString());

    if (scores[playerScore] == MAX_SCORE) {
        session.getPlayers().forEach((player, playerId) => {
            let gameFinishMsg = session.newTextGameMessage(GAME_FINISH, session.getServerId(), scores[0].toString() + ":" + scores[1].toString());
            session.sendReliableMessage(gameFinishMsg, playerId);
        });
        stopGame();
    }
    else {
        session.getPlayers().forEach((player, playerId) => {
            let throwBallMsg = session.newTextGameMessage(MATCH_READY, session.getServerId(), scores[0].toString() + ":" + scores[1].toString());
            session.sendReliableMessage(throwBallMsg, playerId);
        });
    }
}

// Calculates the current time in seconds
function getTimeInS() {
    return Math.round(new Date().getTime()/1000);
}

exports.ssExports = {
    init: init,
    onProcessStarted: onProcessStarted,
    onMessage: onMessage,
    onPlayerConnect: onPlayerConnect,
    onPlayerAccepted: onPlayerAccepted,
    onPlayerDisconnect: onPlayerDisconnect,
    onStartGameSession: onStartGameSession,
};