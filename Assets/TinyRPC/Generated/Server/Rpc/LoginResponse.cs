﻿/*
*代码由 TinyRPC 自动生成，请勿修改
*don't modify manually as it generated by TinyRPC
*/
using System;
using zFramework.TinyRPC.Messages;
namespace zFramework.TinyRPC.Generated
{
    [Serializable]
    public partial class LoginResponse : Response
    {
        public bool success;
        public int playerid;
        public override void OnRecycle()
        {
            base.OnRecycle();
            success = false;
            playerid = 0;
        }
    }
}