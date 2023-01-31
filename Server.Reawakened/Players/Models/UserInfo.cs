﻿using Server.Base.Core.Models;
using Server.Reawakened.Characters.Models;
using Server.Reawakened.Core.Network.Services;
using Server.Reawakened.Players.Enums;

namespace Server.Reawakened.Players.Models;

public class UserInfo : PersistantData
{
    public string LastCharacterSelected { get; set; }

    public Dictionary<int, CharacterDataModel> Characters { get; set; }

    public string AuthToken { get; set; }

    public Gender Gender { get; set; }

    public DateTime DateOfBirth { get; set; }

    public bool Member { get; set; }

    public string SignUpExperience { get; set; }

    public string Region { get; set; }

    public string TrackingShortId { get; set; }

    public int ChatLevel { get; set; }

    public UserInfo()
    {
    }

    public UserInfo(int userId, Gender gender, DateTime dateOfBirth, string region, RandomKeyGenerator kGen)
    {
        Region = region;
        UserId = userId;
        Gender = gender;
        DateOfBirth = dateOfBirth;
        LastCharacterSelected = string.Empty;
        Characters = new Dictionary<int, CharacterDataModel>();
        SignUpExperience = "unknown";
        Member = true;
        TrackingShortId = "false";
        AuthToken = kGen.GetRandomKey<UserInfo>(userId.ToString());
        ChatLevel = 3;
    }
}