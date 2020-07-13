using Godot;
using System;
using System.Text;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Linq;
using Amazon.Runtime;
using Amazon.GameLift;
using Amazon.GameLift.Model;
using Aws.GameLift.Realtime.Types;
using Aws.GameLift.Realtime;
using Aws.GameLift.Realtime.Event;
using Aws.GameLift.Realtime.Command;

public class Client : Node
{
    const string PongServer = "";

    private AmazonGameLiftClient gameLiftClient;
    private const string DEFAULT_ENDPOINT = "127.0.0.1";
    private const int DEFAULT_TCP_PORT = 3001;
    private const int DEFAULT_UDP_PORT = 8921;

    public Aws.GameLift.Realtime.Client realtimeClient { get; private set; }
    private const int SET_PLAYER_NUMBER = 111;
    private const int OP_CODE_PLAYER_ACCEPTED = 113;
    private const int OP_CODE_DISCONNECT_NOTIFICATION = 114;
    private const int MATCH_READY = 115;
    private const int PLAYER_MOVEMENT = 116;
    private const int ADD_POINT = 117;
    private const int BALL_COLLISION = 118;
    private const int GAME_FINISH = 119;
    private bool matchReady = false;

    public override void _Ready()
    {
        //Bypasses certificate checks - TEMP
        //UNSAFE//
        ///////////////////////////////////////////////////////
        System.Net.ServicePointManager.ServerCertificateValidationCallback
            = (a, b, c, d) => { return true; };
        //////////////////////////////////////////////////////

        matchReady = false;
    }


    /// <summary>
    /// GameLift client
    /// </summary>
    public void SearchGameSessions()
    {
        GD.Print("starting search function");    

        BasicAWSCredentials credentials = new BasicAWSCredentials("", "");
        gameLiftClient = new AmazonGameLiftClient(credentials, Amazon.RegionEndpoint.SAEast1);

        SearchGameSessionsRequest request = new SearchGameSessionsRequest()
        {
            FilterExpression = "hasAvailablePlayerSessions=true",
            FleetId = PongServer

        };

        try
        {
            SearchGameSessionsResponse activeSessions = gameLiftClient.SearchGameSessions(request);
            List<GameSession> sessions = activeSessions.GameSessions;
            if (sessions.Count == 0) CreateSession();
            else ConnectToSession(sessions[0]);
        }
        catch (Exception e)
        {
            GD.Print(e);
        }
    }

    private void CreateSession()
    {
        GD.Print("no session found, creating new one");

        CreateGameSessionRequest sessionRequest = new CreateGameSessionRequest()
        {
            MaximumPlayerSessionCount = 2,
            FleetId = PongServer
        };
        try
        {
            CreateGameSessionResponse newSession = gameLiftClient.CreateGameSession(sessionRequest);
            GameSession session = newSession.GameSession;
            ConnectToSession(session);
        }
        catch (Exception e)
        {
            GD.Print(e);
        }

    }

    private void ConnectToSession(GameSession session)
    {
        GD.Print("entering a session");

        if (session == null)
        {
            GD.Print("wtf");
            return;
        }

        CreatePlayerSessionRequest playerSessionRequest = new CreatePlayerSessionRequest()
        {
            GameSessionId = session.GameSessionId,
            PlayerData = "data",
            PlayerId = GenerateID()
        };
        try
        {
            CreatePlayerSessionResponse playerSessionResponse = gameLiftClient.CreatePlayerSession(playerSessionRequest);
            InitRealtimeClient(playerSessionResponse.PlayerSession);
        }
        catch (Exception e)
        {
            GD.Print(e);
        }
    }

    public string GenerateID()
    {
        return Guid.NewGuid().ToString();
    }

    private void InitRealtimeClient(PlayerSession playerSession)
    {
        string endpoint = playerSession.IpAddress;
        int remoteTcpPort = playerSession.Port;
        int listeningUdpPort = FindAvailableUDPPort(DEFAULT_UDP_PORT, DEFAULT_UDP_PORT + 20);
        ConnectionType connectionType = ConnectionType.RT_OVER_WS_UDP_UNSECURED;
        string playerSessionId = playerSession.PlayerSessionId;

        RealtimeClientConnect(endpoint, remoteTcpPort, listeningUdpPort, connectionType, playerSessionId, null);
    }

    private int FindAvailableUDPPort(int firstPort, int lastPort)
    {
        System.Net.IPEndPoint[] UDPEndPoints = IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners();
        List<int> usedPorts = new List<int>();
        usedPorts.AddRange(from n in UDPEndPoints where n.Port >= firstPort && n.Port <= lastPort select n.Port);
        usedPorts.Sort();
        for (int testPort = firstPort; testPort <= lastPort; ++testPort)
        {
            if (!usedPorts.Contains(testPort))
            {
                return testPort;
            }
        }
        return -1;
    }
    
    /// <summary>
    /// Realtime client
    /// </summary>
    public Aws.GameLift.Realtime.Client GetRealtimeClient()
    {
        return realtimeClient;
    }

    private void RealtimeClientConnect(string endpoint, int remoteTcpPort, int listeningUdpPort, ConnectionType connectionType,
                    string playerSessionId, byte[] connectionPayload)
    {
        ClientLogger.LogHandler = (x) => GD.Print(x);

        // Create a client configuration to specify a secure or unsecure connection type
        // Best practice is to set up a secure connection using the connection type RT_OVER_WSS_DTLS_TLS12.
        ClientConfiguration clientConfiguration = new ClientConfiguration()
        {
            // C# notation to set the field ConnectionType in the new instance of ClientConfiguration
            ConnectionType = connectionType
        };
      
        realtimeClient = new Aws.GameLift.Realtime.Client(clientConfiguration);
        realtimeClient.DataReceived += OnDataReceived;
        ConnectionToken connectionToken = new ConnectionToken(playerSessionId, connectionPayload);
        realtimeClient.Connect(endpoint, remoteTcpPort, listeningUdpPort, connectionToken);
    }
    
    public void Disconnect()
    {
        if (realtimeClient.Connected)
        {
            realtimeClient.Disconnect();
        }
    }

    public bool IsConnected()
    {
        return realtimeClient.Connected;
    }
    
    public void SendMessage(bool fast, int opCode, String str, int target)
    {
        RTMessage msg = realtimeClient.NewMessage(opCode);
        DeliveryIntent intent = fast ? DeliveryIntent.Fast : DeliveryIntent.Reliable;
        Byte[] payload = StringToBytes(str);
        realtimeClient.SendMessage(msg.WithDeliveryIntent(intent).WithPayload(payload).WithTargetPlayer(target));
    }

    [Signal]
    public delegate void PlayerNumber(int number);
    [Signal]
    public delegate void BallThrow(List<int> scores);
    [Signal]
    public delegate void PlayerDirection(int direction);
    [Signal]
    public delegate void BallCollision(float velX, float velY, float dirX, float dirY);
    [Signal]
    public delegate void GameFinish(List<int> scores);
    [Signal]
    public delegate void PlayerDisconnected();

    public virtual void OnDataReceived(object sender, DataReceivedEventArgs e)
    {
        string data = BytesToString(e.Data);
        GD.Print("code " + e.OpCode + " recieved. -> " + data);
        String[] separator = { ":" };
        int count = 4;

        switch (e.OpCode)
        {
            case SET_PLAYER_NUMBER:
                {
                    int playerNumber = -1;
                    int.TryParse(data, out playerNumber);

                    EmitSignal(nameof(PlayerNumber), playerNumber - 1);

                    break;
                }
            case MATCH_READY:
                {
                    string[] digits = data.Split(separator, count, StringSplitOptions.RemoveEmptyEntries);
                    List<int> numbers = new List<int>();
                    foreach (string value in digits)
                    {
                        int number;
                        if (int.TryParse(value, out number))
                        {
                           numbers.Add(number);
                        }
                    }
                    EmitSignal(nameof(BallThrow), numbers);
                    break;

                }
            case PLAYER_MOVEMENT:
                {
                    string[] digits = data.Split(separator, count, StringSplitOptions.RemoveEmptyEntries);
                    List<float> numbers = new List<float>();
                    foreach (string value in digits)
                    {
                        float number;
                        if (float.TryParse(value, out number))
                        {
                            numbers.Add(number);
                        }
                    }
                    EmitSignal(nameof(PlayerDirection), numbers);
                    break;
                }
            case BALL_COLLISION:
                {
                    string[] digits = data.Split(separator, count, StringSplitOptions.RemoveEmptyEntries);
                    List<float> numbers = new List<float>();
                    foreach (string value in digits)
                    {
                        float number;
                        if (float.TryParse(value, out number))
                        {
                            numbers.Add(number);
                        }
                    }
                    EmitSignal(nameof(BallCollision), numbers);
                    break;
                }
            case GAME_FINISH:
                {
                    string[] digits = data.Split(separator, count, StringSplitOptions.RemoveEmptyEntries);
                    List<float> numbers = new List<float>();
                    foreach (string value in digits)
                    {
                        float number;
                        if (float.TryParse(value, out number))
                        {
                            numbers.Add(number);
                        }
                    }
                    EmitSignal(nameof(GameFinish), numbers);
                    break;
                }
            case OP_CODE_PLAYER_ACCEPTED:
                {
                    break;
                }
            case OP_CODE_DISCONNECT_NOTIFICATION:
                {
                    EmitSignal(nameof(PlayerDisconnected));
                    break;
                }
            default:
                {
                    break;
                }
        }
    }

    //Helper method to simplify task of sending/receiving payloads.
    public static byte[] StringToBytes(string str)
    {
        return Encoding.UTF8.GetBytes(str);
    }

    //Helper method to simplify task of sending/receiving payloads.
    public static string BytesToString(byte[] bytes)
    {
        return Encoding.UTF8.GetString(bytes);
    }
}


