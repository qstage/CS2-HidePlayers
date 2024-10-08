﻿using System.Runtime.InteropServices;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using Microsoft.Extensions.Logging;

namespace HidePlayers;

public sealed class Plugin : BasePlugin, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "HidePlayers";
    public override string ModuleAuthor => "xstage";
    public override string ModuleVersion => "1.0.4";
    public override string ModuleDescription => "Plugin uses code borrowed from CS2Fixes / cs2kz-metamod / hl2sdk";

    public PluginConfig Config { get; set; } = new();
    private readonly INetworkServerService networkServerService = new();

    private readonly bool[] _hide = new bool[65];
    private readonly CSPlayerState[] _oldPlayerState = new CSPlayerState[65];

    private static readonly MemoryFunctionVoid<nint, nint, int, nint, nint, nint, int, bool> CheckTransmit = new(GameData.GetSignature("CheckTransmit"));
    private static readonly MemoryFunctionVoid<CCSPlayerPawn, CSPlayerState> StateTransition = new(GameData.GetSignature("StateTransition"));

    [StructLayout(LayoutKind.Sequential)]
    public struct CCheckTransmitInfo
    {
        public CFixedBitVecBase m_pTransmitEntity;
    };

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct CFixedBitVecBase
    {
        private const int LOG2_BITS_PER_INT = 5;
        private const int MAX_EDICT_BITS = 14;
        private const int BITS_PER_INT = 32;
        private const int MAX_EDICTS = 1 << MAX_EDICT_BITS;

        private readonly uint* m_Ints;

        public void Clear(int bitNum)
        {
            if (!(bitNum >= 0 && bitNum < MAX_EDICTS))
                return;

            uint* pInt = m_Ints + BitVec_Int(bitNum);
            *pInt &= ~(uint)BitVec_Bit(bitNum);
        }

        private int BitVec_Int(int bitNum) => bitNum >> LOG2_BITS_PER_INT;
        private int BitVec_Bit(int bitNum) => 1 << ((bitNum) & (BITS_PER_INT - 1));
    }

    public override void Load(bool hotReload)
    {
        StateTransition.Hook(Hook_StateTransition, HookMode.Post);
        CheckTransmit.Hook(Hook_CheckTransmit, HookMode.Post);

        RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
        {
            var player = @event.Userid;

            if (player == null) return HookResult.Continue;

            _hide[player.Index] = false;
            _oldPlayerState[player.Index] = CSPlayerState.STATE_WELCOME;

            return HookResult.Continue;
        });

        AddCommand(Config.Command, "Hide players models", (player, info) =>
        {
            player?.PrintToChat(Localizer["Player.Hide", Localizer["Plugin.Tag"], (_hide[player.Index] ^= true) ? Localizer["Plugin.Enable"] : Localizer["Plugin.Disable"]]);
        });
    }

    public override void Unload(bool hotReload)
    {
        StateTransition.Unhook(Hook_StateTransition, HookMode.Post);
        CheckTransmit.Unhook(Hook_CheckTransmit, HookMode.Post);
    }

    private void ForceFullUpdate(CCSPlayerController? player)
    {
        if (player is null || !player.IsValid) return;

        var networkGameServer = networkServerService.GetIGameServer();
        networkGameServer.GetClientBySlot(player.Slot)?.ForceFullUpdate();

        player.PlayerPawn.Value?.Teleport(null, player.PlayerPawn.Value.EyeAngles, null);
    }

    private unsafe HookResult Hook_CheckTransmit(DynamicHook hook)
    {
        nint* ppInfoList = (nint*)hook.GetParam<nint>(1);
        int infoCount = hook.GetParam<int>(2);

        for (int i = 0; i < infoCount; i++)
        {
            nint pInfo = ppInfoList[i];
            byte slot = *(byte*)(pInfo + GameData.GetOffset("CheckTransmitPlayerSlot"));

            var player = Utilities.GetPlayerFromSlot(slot);
            var info = Marshal.PtrToStructure<CCheckTransmitInfo>(pInfo);

            if (player == null || player.Connected != PlayerConnectedState.PlayerConnected)
                continue;

            foreach (var target in Utilities.GetPlayers())
            {
                if (target == null || target.IsHLTV || target.Slot == slot)
                    continue;

                var pawn = target.PlayerPawn.Value!;

                if (player.Pawn.Value?.As<CCSPlayerPawnBase>().PlayerState == CSPlayerState.STATE_OBSERVER_MODE)
                    continue;
                
                if (pawn == null)
                    continue;
                
                if ((LifeState_t)pawn.LifeState != LifeState_t.LIFE_ALIVE)
                {
                    info.m_pTransmitEntity.Clear((int)pawn.Index);
                    continue;
                }

                if (_hide[player.Index] && (Config.Hidden.Equals("@enemy") && player.Team != target.Team || Config.Hidden.Equals("@team") && player.Team == target.Team || Config.Hidden.Equals("@all")))
                {
                    info.m_pTransmitEntity.Clear((int)pawn.Index);
                }
            }
        }

        return HookResult.Continue;
    }

    private HookResult Hook_StateTransition(DynamicHook hook)
    {
        var player = hook.GetParam<CCSPlayerPawn>(0).OriginalController.Value;
        var state = hook.GetParam<CSPlayerState>(1);

        if (player is null) return HookResult.Continue;

        if (state != _oldPlayerState[player.Index])
        {
            if (state == CSPlayerState.STATE_OBSERVER_MODE || _oldPlayerState[player.Index] == CSPlayerState.STATE_OBSERVER_MODE)
            {
                ForceFullUpdate(player);
            }
        }

        _oldPlayerState[player.Index] = state;

        return HookResult.Continue;
    }

    public void OnConfigParsed(PluginConfig config)
    {
        if (config.Version < Config.Version)
        {
            Logger.LogWarning("Update plugin config. New version: {v}", Config.Version);
        }
        
        Config = config;
    }
}
