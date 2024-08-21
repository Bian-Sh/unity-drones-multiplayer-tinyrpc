using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using zFramework.TinyRPC;
using zFramework.TinyRPC.Generated;

[MessageHandlerProvider]
public class Server : MonoBehaviour
{
    private int port = 5701;
    static TinyServer server;
    private static List<Player> players = new List<Player>();
    private static int frame = 0;
    private float lastMovementUpdate;
    private float movementUpdateRate = 0.05f;
    static int id = 0;

    #region Server Event
    private void Server_OnServerClosed(string obj)
    {
        Debug.Log($"{nameof(Server)}: Server is Closed！ ");
    }

    private void Server_OnClientEstablished(Session session)
    {
        Debug.Log($"{nameof(Server)}: Client {session} is Connected！ ");
    }

    private void Server_OnClientDisconnected(Session session)
    {
        Debug.Log($"{nameof(Server)}: Client {session} is Disconnected！ ");
        // Remove player from player list
        var player = players.Find(c => c.session == session);
        if (player != null)
        {
            players.Remove(player);

            var report = new PlayerOfflineReport
            {
                playerid = player.playerid
            };
            server.BroadcastOthers(session, report);
        }
        else
        {
            Debug.Log($"{nameof(Server)}: Client {session} is not found！ ");
        }
    }
    #endregion

    #region Unity Callbacks
    private void Start()
    {
        Application.runInBackground = true;
        server = new TinyServer(port);
        server.OnClientDisconnected += Server_OnClientDisconnected;
        server.OnClientEstablished += Server_OnClientEstablished;
        server.OnServerClosed += Server_OnServerClosed;
        server.Start();
    }

    private void OnApplicationQuit()
    {
        server.OnClientDisconnected -= Server_OnClientDisconnected;
        server.OnClientEstablished -= Server_OnClientEstablished;
        server.OnServerClosed -= Server_OnServerClosed;
        server.Stop();
    }

    private void Update()
    {
        // Ask player for their position
        if (Time.time - lastMovementUpdate > movementUpdateRate)
        {
            lastMovementUpdate = Time.time;

            if (players.Count > 0)
            {
                // Send position to players 
                using var broadcast = ObjectPool.Allocate<S2C_BroadcastPlayerPose>();
                broadcast.frame = frame;
                broadcast.infos = CollectPlayerInfos();
                server.Broadcast(broadcast);

                // ask pose again
                using var askpos = ObjectPool.Allocate<S2C_RequirePlayerPose>();
                askpos.frame = frame++; // fake frame, have not implement frame sync yet
                server.Broadcast(askpos);
            }
        }
    }
    #endregion

    #region TinyRPC MessageHandler Task
    [MessageHandler(MessageType.RPC)]
    private static async Task OnPlayerLoginAsync(Session session, LoginRequest request, LoginResponse response)
    {
        // Add user to a list
        Player player = new()
        {
            session = session,
            playerid = id++,
            playerName = request.name,
            position = Vector3.zero + 2 * id * Vector3.right,
            rotation = Quaternion.identity,
            velocity = Vector3.zero
        };

        players.Add(player);
        response.playerid = player.playerid;
        response.success = true;

        var report = new PlayerLoginReport();
        report.playerinfo.playerid = player.playerid;
        report.playerinfo.name = player.playerName;
        report.playerinfo.moveinfo.position = player.position;
        report.playerinfo.moveinfo.velocity = player.velocity;
        report.playerinfo.moveinfo.rotation = player.rotation;
        server.BroadcastOthers(session, report);

        await Task.CompletedTask;
    }

    /// <summary>
    ///   处理当用户请求房间信息时执行
    /// </summary>
    /// <param name="session"> 请求方</param>
    /// <param name="request"></param>
    /// <param name="response"></param>
    /// <returns></returns>
    [MessageHandler(MessageType.RPC)]
    private static async Task OnRoomInfoRequestAsync(Session session, GetRoomInfoRequest request, GetRoomInfoResponse response)
    {
        response.playerinfo = CollectPlayerInfos();
        await Task.CompletedTask;
    }

    /// <summary>
    ///  处理用户上报的姿态信息
    /// </summary>
    /// <param name="session"></param>
    /// <param name="message"></param>
    [MessageHandler(MessageType.Normal)]
    private static void OnPlayerReportPose(Session session, PlayerPoseReport message)
    {
        var client = players.Find(c => c.session == session);
        client.position = message.info.moveinfo.position;
        client.velocity = message.info.moveinfo.velocity;
        client.rotation = message.info.moveinfo.rotation;
    }
    #endregion

    #region Assistant Function
    private static List<PlayerInfo> CollectPlayerInfos()
    {
        var infos = new List<PlayerInfo>();
        foreach (var c in players)
        {
            var playerinfo = new PlayerInfo()
            {
                playerid = c.playerid,
                name = c.playerName,
                moveinfo = new MoveInfo
                {
                    position = c.position,
                    velocity = c.velocity,
                    rotation = c.rotation
                },
            };
            infos.Add(playerinfo);
        }
        return infos;
    }
    #endregion
}
