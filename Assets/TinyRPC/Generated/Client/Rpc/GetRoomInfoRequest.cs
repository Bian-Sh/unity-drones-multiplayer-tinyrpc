﻿/*
*代码由 TinyRPC 自动生成，请勿修改
*don't modify manually as it generated by TinyRPC
*/
using System;
using zFramework.TinyRPC.Messages;
namespace zFramework.TinyRPC.Generated
{
    /// <summary>
    ///  用户请求房间信息，服务器返回房间内所有玩家信息
    ///  此信息用于客户端生成自身 Avatar 和其他玩家的 Avatar
    ///  暂时不需要传入什么参数
    /// </summary>
    [Serializable]
    [ResponseType(typeof(GetRoomInfoResponse))]
    public partial class GetRoomInfoRequest : Request
    {
        public override void OnRecycle()
        {
            base.OnRecycle();
        }
    }
}
