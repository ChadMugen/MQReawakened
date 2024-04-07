﻿using Microsoft.Extensions.Logging;
using Server.Reawakened.Core.Enums;
using Server.Reawakened.Players.Models.Character;
using Server.Reawakened.XMLs.Abstractions.Enums;
using Server.Reawakened.XMLs.Abstractions.Extensions;
using Server.Reawakened.XMLs.Abstractions.Interfaces;
using Server.Reawakened.XMLs.Bundles.Base;
using System.Xml;

namespace Server.Reawakened.XMLs.Bundles.Internal;

public class InternalQuestItem : InternalXml
{
    public override string BundleName => "InternalQuestItem";
    public override BundlePriority Priority => BundlePriority.Low;

    public ILogger<InternalQuestItem> Logger { get; set; }
    public ItemCatalog ItemCatalog { get; set; }

    public Dictionary<GameVersion, Dictionary<int, List<ItemModel>>> QuestItemList;

    public override void InitializeVariables() =>
        QuestItemList = [];

    public override void ReadDescription(XmlDocument xml)
    {
        foreach (XmlNode questItemXml in xml.ChildNodes)
        {
            if (!(questItemXml.Name == "QuestItems")) continue;

            foreach (XmlNode gVXml in questItemXml.ChildNodes)
            {
                if (!(gVXml.Name == "GameVersion")) continue;

                var gameVersion = GameVersion.Unknown;

                foreach (XmlAttribute gVAttribute in gVXml.Attributes)
                    switch (gVAttribute.Name)
                    {
                        case "version":
                            gameVersion = gameVersion.GetEnumValue(gVAttribute.Value, Logger);
                            break;
                    }

                QuestItemList.TryAdd(gameVersion, []);

                foreach (XmlNode quest in gVXml.ChildNodes)
                {
                    if (!(quest.Name == "Quest")) continue;

                    var questId = -1;

                    foreach (XmlAttribute questAttribute in quest.Attributes)
                        switch (questAttribute.Name)
                        {
                            case "questId":
                                questId = int.Parse(questAttribute.Value);
                                break;
                        }

                    var itemList = quest.GetXmlItems(ItemCatalog, Logger);

                    QuestItemList[gameVersion].TryAdd(questId, itemList);
                }
            }
        }
    }
}