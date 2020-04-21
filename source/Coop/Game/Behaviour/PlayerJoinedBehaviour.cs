﻿using Coop.Common;
using System;
using TaleWorlds.CampaignSystem;

namespace Coop.Game
{
    public class PlayerJoinedBehaviour : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnGameLoaded));
            CoopClient.Instance.Events.OnBeforePlayerPartySpawned.AddNonSerializedListener(this, new Action<MobileParty>(this.OnPlayerControlledPartySpawned));
        }

        public override void SyncData(IDataStore dataStore)
        {
            Log.Info($"{this}.SyncData");
        }

        private void OnGameLoaded(CampaignGameStarter gameStarter)
        {
            CoopClient.Instance.Events.OnBeforePlayerPartySpawned.Invoke(MobileParty.MainParty);
        }

        private void OnPlayerControlledPartySpawned(MobileParty party)
        {
            CoopClient.Instance.GameState.AddPlayerControllerParty(party);
        }
    }
}