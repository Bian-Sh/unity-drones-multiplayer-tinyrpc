using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using zFramework.TinyRPC;
using zFramework.TinyRPC.Generated;

public class Client : MonoBehaviour
{
    #region Private Properties
    private string server_ip = "127.0.0.1";
    private int port = 5701;

    private TinyClient client;
    private Player self;
    private readonly Dictionary<int, Player> players = new();
    #endregion

    #region Public Members
    public GameObject playerPrefab;
    public GameObject view3D;
    public Text statusText;
    public Text playersCount;
    #endregion


    #region Unity Calls
    public void Start()
    {
        Application.runInBackground = true;
        view3D.SetActive(true);
        // 监听玩家上线事件
        this.AddNetworkSignal<PlayerLoginReport>(OnPlayerLogin);
        // 监听玩家下线事件
        this.AddNetworkSignal<PlayerOfflineReport>(OnPlayerOffline);
        // 监听服务器下发的玩家姿态事件
        this.AddNetworkSignal<S2C_BroadcastPlayerPose>(OnPlayerPositionRecived);
        // 监听服务器请求上报玩家姿态事件
        this.AddNetworkSignal<S2C_RequirePlayerPose>(OnSvrPlayerPoseRequired);
    }

    public void OnApplicationQuit()
    {
        client.OnClientDisconnected -= OnClientDisconnected;
        this.RemoveNetworkSignal<PlayerLoginReport>(OnPlayerLogin);
        this.RemoveNetworkSignal<PlayerOfflineReport>(OnPlayerOffline);
        this.RemoveNetworkSignal<S2C_BroadcastPlayerPose>(OnPlayerPositionRecived);
        this.RemoveNetworkSignal<S2C_RequirePlayerPose>(OnSvrPlayerPoseRequired);
        client?.Stop();
    }

    private void Update()
    {
        if (null == client || !client.IsConnected) { return; }

        // 更新其他玩家的姿态
        foreach (var player in players.Values)
        {
            if (player != this.self)
            {
                player.avatar.transform.position = Vector3.Lerp(player.avatar.transform.position, player.position, 0.1f);
                player.avatar.transform.rotation = Quaternion.Lerp(player.avatar.transform.rotation, player.rotation, 0.1f);
            }
        }

        if (statusUpdateTime != -1 && (Time.time - statusUpdateTime > statusResetRate))
        {
            SetStatus(string.Empty);
        }
    }
    #endregion

    #region TinyRPC Client Event
    // 登录：在连接按钮上通过Inspector面板绑定
    public async void ConnectAsync()
    {
        // Does the player has a name
        string playerNameInput = GameObject.Find("NameInput").GetComponent<InputField>().text;
        if (playerNameInput == "")
        {
            Debug.Log("You must enter a name");
            return;
        }
        var playerName = playerNameInput;
        SetStatus("Connecting...");
        client ??= new TinyClient(server_ip, port);
        client.OnClientDisconnected += OnClientDisconnected;

        var result_connect = await client.ConnectAsync();
        if (result_connect)
        {

            LoginRequest login = new()
            {
                name = playerName,
                password = "fake psw"
            };
            //1. 登录
            var result_login = await client.Call<LoginResponse>(login);

            if (result_login.success)
            {
                Debug.Log($"{nameof(Client)}: 登录成功！ ");
                var playerId = result_login.playerid;
                // 2. 请求当前房间信息（所有玩家的位置信息，包括自己）
                var roomInfoRequest = new GetRoomInfoRequest();
                var result_roomInfo = await client.Call<GetRoomInfoResponse>(roomInfoRequest);
                foreach (var item in result_roomInfo.playerinfo)
                {
                    var player = SpawnPlayer(item.playerid, item.name, item.playerid == playerId);
                    //3. 找到自己并更新设置
                    if (player.playerid == playerId)
                    {
                        self = player;
                        self.session = client.Session;
                        // 为自己添加3D移动能力和3D摄像机跟随
                        self.rigidbody = self.avatar.GetComponent<Rigidbody>();
                        self.rigidbody.MovePosition(item.moveinfo.position);
                        self.rigidbody.MoveRotation(item.moveinfo.rotation);
                    }
                    else
                    {
                        // 4. 设置其他玩家出生点
                        player.position = item.moveinfo.position;
                        player.velocity = item.moveinfo.velocity;
                        player.rotation = item.moveinfo.rotation;
                    }
                }
                SetStatus("Connected");
                return;
            }
            else
            {
                Debug.Log($"{nameof(Client)}: 登录失败！ ");
            }
        }
        else
        {
            Debug.Log($"{nameof(Client)}: 连接服务器失败！ ");
        }
        SetStatus("Connected Failed !");
    }

    private void OnClientDisconnected()
    {
        SetStatus("Disconnected");
        // clear all players
        foreach (var item in players)
        {
            Destroy(item.Value.avatar);
        }
        players.Clear();
        client = null; // 方便下一个回合
    }
    #endregion

    #region  TinyRPC MessageHandler （观察者模式监听）
    // 其他玩家上线
    private void OnPlayerLogin(Session session, PlayerLoginReport report)
    {
        var player = SpawnPlayer(report.playerinfo.playerid, report.playerinfo.name, false);
        var moveinfo = report.playerinfo.moveinfo;
        player.position = moveinfo.position;
        player.velocity = moveinfo.velocity;
        player.rotation = moveinfo.rotation;
    }
    // 其他玩家下线
    private void OnPlayerOffline(Session session, PlayerOfflineReport report)
    {
        if (players.ContainsKey(report.playerid))
        {
            Destroy(players[report.playerid].avatar);
            players.Remove(report.playerid);
            SetPlayersCount();
            SetStatus("Player " + report.playerid + " has disconnected");
        }
    }

    // 服务器下发的玩家姿态
    private void OnPlayerPositionRecived(Session session, S2C_BroadcastPlayerPose pose)
    {
        if (this.self == null)
        {
            // not login yet
            return;
        }
        // Update everyone else
        for (int i = 0; i < pose.infos.Count; i++)
        {
            var playerinfo = pose.infos[i];
            if (playerinfo.playerid != this.self.playerid)
            {
                if (players.ContainsKey(playerinfo.playerid))
                {
                    players[playerinfo.playerid].position = playerinfo.moveinfo.position;
                    players[playerinfo.playerid].velocity = playerinfo.moveinfo.velocity;
                    players[playerinfo.playerid].rotation = playerinfo.moveinfo.rotation;
                }
            }
        }
    }

    // 服务器请求上报玩家姿态
    private void OnSvrPlayerPoseRequired(Session session, S2C_RequirePlayerPose pose)
    {
        if (self != null)
        {
            // 向服务器发送自己的姿态
            self.CapturePlayerState();
            using var report = ObjectPool.Allocate<PlayerPoseReport>();
            report.info = new PlayerInfo
            {
                playerid = this.self.playerid,
                name = this.self.playerName,
                moveinfo = new MoveInfo
                {
                    position = self.position,
                    velocity = self.velocity,
                    rotation = self.rotation
                }
            };
            client.Send(report);
        }
    }
    #endregion

    #region Assitant Members
    // 生成一个玩家
    private Player SpawnPlayer(int id, string name, bool isLocal)
    {
        GameObject go = Instantiate(playerPrefab);

        var player = new Player
        {
            playerid = id,
            playerName = name,
            avatar = go
        };
        player.avatar.GetComponentInChildren<TextMesh>().text = name;

        // self player?
        if (isLocal)
        {
            // Hide connection canvas
            GameObject.Find("ConnectionCanvas").SetActive(false);

            // Add mobility
            Add3DMobility(player);

            player.avatar.tag = "Player";
        }
        else
        {
            // Remove gravity and mess, since they are controlled by other player/environment
            var playerRigidbody = player.avatar.GetComponent<Rigidbody>();
            playerRigidbody.mass = 1;
            playerRigidbody.useGravity = false;

            SetStatus("New player logged in: " + player.playerName);
        }

        players.Add(id, player);
        SetPlayersCount();
        return player;
    }

    // 为玩家添加3D移动能力和3D摄像机跟随
    private void Add3DMobility(Player player)
    {
        player.avatar.AddComponent<DroneMovementScript>();
        var camera = GameObject.Find("3D Camera");
        var script = camera.AddComponent<CameraFollowScript>();
        script.drone = player.avatar;
        script.angle = 18;
    }

    private float statusUpdateTime = -1;
    private float statusResetRate = 5.0f; // Reset status every 5 seconds

    // 设置状态栏
    private void SetStatus(string message)
    {
        if (message == string.Empty)
        {
            statusText.text = message;
            statusUpdateTime = -1;
        }
        else
        {
            statusText.text = "[" + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "] " + message;
            statusUpdateTime = Time.time;
        }
    }

    private void SetPlayersCount()
    {
        playersCount.text = players.Count.ToString();
    }
    #endregion
}
