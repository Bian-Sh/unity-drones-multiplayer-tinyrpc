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
    private int playerId = -1;
    private bool isConnected = false;
    private string playerName;

    private TinyClient client;
    private Dictionary<int, Player> players = new Dictionary<int, Player>();
    #endregion

    #region Public Members
    public GameObject playerPrefab;
    public GameObject view3D;
    public Text statusText;
    public Text playersCount;
    #endregion

    public void Start()
    {
        Application.runInBackground = true;
        view3D.SetActive(true);
        // 监听玩家上线事件
        this.AddNetworkSignal<S2C_PlayerLogin>(OnPlayerLogin);
        // 监听玩家下线事件
        this.AddNetworkSignal<S2C_PlayerOffline>(OnPlayerOffline);
        // 监听服务器请求上报玩家姿态事件
        this.AddNetworkSignal<S2C_BroadcastPlayerPose>(OnPlayerPositionRecived);

    }

    public void OnApplicationQuit()
    {
        client?.Stop();
    }

    private void OnPlayerPositionRecived(Session session, S2C_BroadcastPlayerPose pose)
    {
        // Update everyone else
        for (int i = 1; i < pose.infos.Count; i++)
        {
            var playerinfo = pose.infos[i];
            if (playerinfo.playerid != playerId)
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

    // button click event，在连接按钮上通过Inspector面板绑定
    public async void ConnectAsync()
    {
        // Does the player has a name
        string playerNameInput = GameObject.Find("NameInput").GetComponent<InputField>().text;
        if (playerNameInput == "")
        {
            Debug.Log("You must enter a name");
            return;
        }
        playerName = playerNameInput;
        SetStatus("Connecting...");
        client ??= new TinyClient(server_ip, port);
        client.OnClientDisconnected += OnClientDisconnected;

        var result_connect = await client.ConnectAsync();
        if (result_connect)
        {
            C2S_Login login = ObjectPool.Allocate<C2S_Login>();
            login.name = playerName;
            login.password = "fake psw";
            var result_login = await client.Call<S2C_Login>(login);

            if (result_login.success)
            {
                Debug.Log($"{nameof(Client)}: 登录成功！ ");
                this.playerId = result_login.playerid;
                // todo : 请求当前房间信息（所有玩家的位置信息，包括自己）
                var result_roomInfo = await client.Call<S2C_RoomInfo>(ObjectPool.Allocate<C2S_RoomInfo>());
                foreach (var item in result_roomInfo.playerinfo)
                {
                    var player = SpawnPlayer(item.playerid, item.name);
                    // 无差别对姿态赋初始值
                    player.position = item.moveinfo.position;
                    player.velocity = item.moveinfo.velocity;
                    player.rotation = item.moveinfo.rotation;
                }
                isConnected = true;
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
        isConnected = false;
        SetStatus("Connected Failed !");
    }

    private void Update()
    {
        if (!isConnected) { return; }

        // Update players positions
        foreach (var player in players.Values)
        {
            if (player.playerid != playerId)
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

    private Player SpawnPlayer(int id, string name)
    {
        //  if(players.ContainsKey(id))return players[id];

        GameObject go = Instantiate(playerPrefab);

        var player = new Player();
        player.playerid = id;
        player.playerName = name;
        player.avatar = go;
        player.avatar.GetComponentInChildren<TextMesh>().text = name;

        // Is this ours
        if (id == playerId)
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

    private void Add3DMobility(Player player)
    {
        player.avatar.AddComponent<DroneMovementScript>();
        var camera = GameObject.Find("3D Camera");
        var script = camera.AddComponent<CameraFollowScript>();
        script.drone = player.avatar;
        script.angle = 18;

    }

    private void OnPlayerLogin(Session session, S2C_PlayerLogin login)
    {
        // 由于自己会在登录完成后请求房间信息并创建角色，所以这里无需再次创建
        // 并且，房间请求只能执行依次，避免重复创建
        var player = SpawnPlayer(login.playerinfo.playerid, login.playerinfo.name);
        // 无差别对姿态赋初始值
        var moveinfo = login.playerinfo.moveinfo;
        player.position = moveinfo.position;
        player.velocity = moveinfo.velocity;
        player.rotation = moveinfo.rotation;
    }

    private void OnPlayerOffline(Session session, S2C_PlayerOffline offline)
    {
        if (players.ContainsKey(offline.playerid))
        {
            Destroy(players[offline.playerid].avatar);
            players.Remove(offline.playerid);
            SetPlayersCount();
            SetStatus("Player " + offline.playerid + " has disconnected");
        }
    }
    private void OnClientDisconnected()
    {
        isConnected = false;
        SetStatus("Disconnected");
        // clear all players
        foreach (var item in players)
        {
            Destroy(item.Value.avatar);
        }
        players.Clear();
        client = null; // 方便下一个回合
    }

    private float statusUpdateTime = -1;
    private float statusResetRate = 5.0f; // Reset status every 5 seconds
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
}
