using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GrpcContracts.Account
{
    [ProtoContract]
    public class AuthNonceResponce
    {
        [ProtoMember(1)] public string nonce { get; set; } = "";
    }

    [ProtoContract]
    public class AuthNonceRequest
    {
        [ProtoMember(1)] public int UserId {get; set; }
    }
}
