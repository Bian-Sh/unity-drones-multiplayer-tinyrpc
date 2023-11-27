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
    private static List<Player> clients = new List<Player>();
    private static int frame = 0;
    private float lastMovementUpdate;
    private float movementUpdateRate = 0.05f;
    static int id = 0;
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
        var player = clients.Find(c => c.session == session);
        if (player != null)
        {
            clients.Remove(player);

            var report_disconnected = ObjectPool.Allocate<S2C_PlayerOffline>();
            report_disconnected.playerid = player.playerid;
            server.Boardcast(report_disconnected);
        }
        else
        {
            Debug.Log($"{nameof(Server)}: Client {session} is not found！ ");
        }
    }

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
        server.Stop();
    }

    private void Update()
    {
        // Ask player for their position
        if (Time.time - lastMovementUpdate > movementUpdateRate)
        {
            lastMovementUpdate = Time.time;

            // Send position to players 
            var broadcast = ObjectPool.Allocate<S2C_BroadcastPlayerPose>();
            broadcast.frame = frame;
            broadcast.infos = CollectPlayerInfos();
            server.Boardcast(broadcast);

            // ask pose again
            var askpos = ObjectPool.Allocate<S2C_RequirePlayerPose>();
            askpos.frame = frame++;
            server.Boardcast(askpos);
        }

    }

    [MessageHandler(MessageType.RPC)]
    private static async Task OnPlayerLoginAsync(Session session, C2S_Login request, S2C_Login response)
    {
        // Add user to a list
        Player player = new()
        {
            session = session,
            playerid = id,
            playerName = request.name,
            position = Vector3.zero + Vector3.right * id,
            rotation = Quaternion.identity,
            velocity = Vector3.zero
        };

        clients.Add(player);
        Debug.Log($"{nameof(Server)}: player count = {clients.Count}");
        response.playerid = player.playerid;
        response.success = true;
        id++;
        await Task.CompletedTask;
        Debug.Log($"{nameof(Server)}: finish login ,player id= {player.playerName}");

        var report_login = ObjectPool.Allocate<S2C_PlayerLogin>();
        report_login.playerinfo.playerid = player.playerid;
        report_login.playerinfo.name = player.playerName;
        report_login.playerinfo.moveinfo.position = player.position;
        report_login.playerinfo.moveinfo.velocity = player.velocity;
        report_login.playerinfo.moveinfo.rotation = player.rotation;
        server.BoardcastOthers(session, report_login);
    }

    /// <summary>
    ///   处理当用户请求房间信息时执行
    /// </summary>
    /// <param name="session"> 请求方</param>
    /// <param name="request"></param>
    /// <param name="response"></param>
    /// <returns></returns>
    [MessageHandler(MessageType.RPC)]
    private static async Task OnRoomInfoRequestAsync(Session session, C2S_RoomInfo request, S2C_RoomInfo response)
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
    private static void OnPlayerReportPose(Session session, C2S_ReportPlayerPose message)
    {
        var client = clients.Find(c => c.session == session);
        client.position = message.info.moveinfo.position;
        client.velocity = message.info.moveinfo.velocity;
        client.rotation = message.info.moveinfo.rotation;
    }

    private static List<PlayerInfo> CollectPlayerInfos()
    {
        var infos = new List<PlayerInfo>();
        foreach (var c in clients)
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
}
