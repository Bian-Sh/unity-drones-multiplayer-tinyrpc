﻿/*
*代码由 TinyRPC 自动生成，请勿修改
*don't modify manually as it generated by TinyRPC
*/
using System;
using zFramework.TinyRPC.Messages;
namespace zFramework.TinyRPC.Generated
{
    /// <summary>
    ///  S2C = Server to Client
    /// </summary>
    [Serializable]
    public partial class S2C_Login : Response
    {
        public bool success;
        public string playerid;
        public override void OnRecycle()
        {
            base.OnRecycle();
            success = false;
            playerid = "";
        }
    }
}