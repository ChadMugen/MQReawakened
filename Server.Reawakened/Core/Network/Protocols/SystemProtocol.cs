﻿using System.Xml;

namespace Server.Reawakened.Core.Network.Protocols;

public abstract class SystemProtocol : BaseProtocol
{
    public abstract void Run(XmlDocument xmlDoc);
}
