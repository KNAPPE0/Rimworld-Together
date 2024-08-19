namespace Shared
{
    public class CommonEnumerators
    {
<<<<<<< HEAD
        public enum ServerFileMode 
        { 
            Configs, Actions, Sites, Events, Roads, World, Whitelist, Difficulty, Market 
        }

        public enum LogMode 
        { 
            Message, Warning, Error, Title 
        }
=======
        public enum ServerFileMode { Configs, Actions, Sites, Events, Roads, World, Whitelist, Difficulty, Market, Discord }

        public enum LogMode { Message, Warning, Error, Title, Outsider }
>>>>>>> 4fb7aebda0aa95ba5fc140c05c34cc0a3e75b4e5

        public enum CommandMode 
        { 
            Op, Deop, Broadcast, ForceSave 
        }

        public enum EventStepMode 
        { 
            Send, Receive, Recover 
        }

        public enum MarketStepMode 
        { 
            Add, Request, Reload 
        }

        public enum AidStepMode 
        { 
            Send, Receive, Accept, Reject 
        }

        public enum CaravanStepMode 
        { 
            Add, Remove, Move 
        }

        public enum RoadStepMode 
        { 
            Add, Remove 
        }

        public enum FactionManifestMode
        {
            Create,
            Delete,
            NameInUse,
            NoPower,
            AddMember,
            RemoveMember,
            AcceptInvite,
            Promote,
            Demote,
            AdminProtection,
            MemberList
        }

        public enum FactionRanks 
        { 
            Member, Moderator, Admin 
        }

        public enum Goodwill 
        { 
            Enemy, Neutral, Ally, Faction, Personal 
        }

        public enum GoodwillTarget 
        { 
            Settlement, Site 
        }

        public enum TransferMode 
        { 
            Gift, Trade, Rebound, Pod, Market 
        }

        public enum TransferLocation 
        { 
            Caravan, Settlement, Pod, World 
        }

        public enum TransferStepMode 
        { 
            TradeRequest, TradeAccept, TradeReject, TradeReRequest, TradeReAccept, TradeReReject, Recover, Pod, Market 
        }

        public enum OfflineActivityStepMode 
        { 
            Request, Deny, Unavailable 
        }

        public enum OnlineActivityStepMode 
        { 
            Request, Accept, Reject, Unavailable, Action, Create, Destroy, Damage, Hediff, Kill, TimeSpeed, GameCondition, Weather, Stop 
        }

        public enum OnlineActivityTargetFaction 
        { 
            Faction, NonFaction, None 
        }

        public enum OnlineActivityApplyMode 
        { 
            Add, Remove, None 
        }

        public enum OnlineActivityType 
        { 
            None, Visit, Raid 
        }

        public enum OfflineActivityType 
        { 
            None, Visit, Raid, Spy 
        }

        public enum ActionTargetType 
        { 
            Thing, Human, Animal, Cell, Invalid 
        }

        public enum CreationType 
        { 
            Human, Animal, Thing 
        }

        public enum SiteStepMode 
        { 
            Accept, Build, Destroy, Info, Deposit, Retrieve, Reward, WorkerError 
        }

        public enum SettlementStepMode 
        { 
            Add, Remove 
        }

        public enum SaveMode 
        { 
            Disconnect, Autosave, Strict 
        }

<<<<<<< HEAD
        public enum UserColor 
        { 
            Normal, Admin, Console, Private 
        }

        public enum MessageColor 
        { 
            Normal, Admin, Console, Private 
        }
=======
        public enum UserColor { Normal, Admin, Console, Private, Discord }

        public enum MessageColor { Normal, Admin, Console, Private, Discord }
>>>>>>> 4fb7aebda0aa95ba5fc140c05c34cc0a3e75b4e5

        public enum LoginMode 
        { 
            Login, Register 
        }

        public enum LoginResponse 
        { 
            InvalidLogin, 
            BannedLogin,
            RegisterInUse, 
            RegisterError, 
            ExtraLogin, 
            WrongMods, 
            ServerFull,
            Whitelist,
            WrongVersion,
            NoWorld
        }

        public enum WorldStepMode 
        { 
            Required, Existing 
        }
    }
}