using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using System.Security.AccessControl;
using TaleWorlds.CampaignSystem.Overlay;
using HarmonyLib;
using SandBox.Tournaments.MissionLogics;
using System.Reflection;
using SandBox.Tournaments;
using SandBox;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.Engine;
using TaleWorlds.ObjectSystem;
using SandBox.ViewModelCollection.Tournament;
using SandBox.Missions.MissionLogics.Arena;
using SandBox.Missions.MissionLogics;
using System.Text.RegularExpressions;
using TaleWorlds.MountAndBlade.Source.Missions;
using System.Xml.Linq;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;




/// @namespace Yigu.AnnualTournament.Beta
/// 提供年度比武相关的模块和行为。
namespace Yigu.AnnualTournament.Beta
{
    /// @class SubModule
    /// 继承自 MBSubModuleBase，定义了比武模块的加载和初始化逻辑。
    public class SubModule : MBSubModuleBase
    {
        /// <summary>
        /// 游戏处于加载界面时最先被调用的函数，你应该在这个函数中完成初始化的主要部分
        /// </summary>
        protected override void OnSubModuleLoad()
        {
            //这里放Mod加载时的动作
        }
        /// @brief 在游戏初始化时调用，为单人战役模式添加自定义行为。
        /// @param game 当前游戏实例。
        /// @param starterObject 游戏启动对象，通常是 CampaignGameStarter。
        protected override void InitializeGameStarter(Game game, IGameStarter starterObject)
        {
            // 检查传入的starterObject是否是CampaignGameStarter类型
            // CampaignGameStarter用于单人战役游戏模式，包含了管理战役相关功能的方法
            if (!(starterObject is CampaignGameStarter campaignGameStarter))
                return; // 如果不是，直接返回，不执行任何操作

            // 向游戏添加自定义行为
            campaignGameStarter.AddBehavior(new BiWuBehavior());
        }

    }
    /// @class BiWuBehavior
    /// 继承自 CampaignBehaviorBase，定义了年度比武的具体行为逻辑。
    public class BiWuBehavior : CampaignBehaviorBase
    {
        /// @brief 注册事件，用于在新游戏或游戏加载时触发逻辑。
        public override void RegisterEvents()
        {
            // 在新游戏创建时和游戏加载时添加非序列化监听器，以触发OnSessionLoad方法
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnSessionLoad);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnSessionLoad);
        }
        /// @brief 用于同步存档数据
        /// @param dataStore 用于存储或加载数据的接口。
        public override void SyncData(IDataStore dataStore)
        {

        }
        private void OnDailyTick()
        {

        }
        /// @brief 在会话加载时调用，添加自定义游戏菜单。
        /// @param campaignGameStart 战役游戏启动器。
        private void OnSessionLoad(CampaignGameStarter campaignGameStart)
        {
            AddGameMenus(campaignGameStart);
            AnnualTournamentManager.Instance.Initialize();
        }
        /// @brief 添加比武相关的自定义菜单选项。
        /// @param campaignGameStarter 战役游戏启动器。
        private void AddGameMenus(CampaignGameStarter campaignGameStarter)
        {
            // 在城镇菜单中添加一个新的菜单选项，用于进入

            campaignGameStarter.AddGameMenuOption(
                "town_arena", // 目标菜单-竞技场
                "yigu_biwu", // 菜单选项的唯一标识
                "{=*}触发年度比武", // 菜单选项显示的文本
                (MenuCallbackArgs args) => { return AnnualTournamentManager.Instance.isGameBeingHeld; }, // 显示条件,有比赛时才显示
                game_menu_town_biwu_on_consequence); // 选中后的行动

            campaignGameStarter.AddGameMenu("biwu_to_join", "{=*}年度比武正在如火如荼地举办，请按顺序参加比赛", new OnInitDelegate(this.game_menu_biwu_join_on_init), GameOverlays.MenuOverlayType.SettlementWithBoth, GameMenu.MenuFlags.None, null);

            campaignGameStarter.AddGameMenuOption(
                "biwu_to_join", // 目标菜单-竞技场
                "yigu_biwu_archery", // 菜单选项的唯一标识
                "{=*}射箭比赛(勇冠三军第一项)", // 菜单选项显示的文本
                (MenuCallbackArgs args) => { return true; }, // 显示条件,一直可以显示
                game_menu_town_biwu_archery_on_consequence); // 选中后的行动

            campaignGameStarter.AddGameMenuOption(
                "biwu_to_join", // 目标菜单-竞技场
                "yigu_biwu_jousting", // 菜单选项的唯一标识
                "{=*}骑马对枪(勇冠三军第二项)", // 菜单选项显示的文本
                (MenuCallbackArgs args) => { return true; }, // 显示条件,一直可以显示
                game_menu_town_biwu_jousting_on_consequence); // 选中后的行动
            campaignGameStarter.AddGameMenuOption(
                "biwu_to_join", // 目标菜单-竞技场
                "yigu_biwu_boxing", // 菜单选项的唯一标识
                "{=*}拳击(勇冠三军第三项)", // 菜单选项显示的文本
                (MenuCallbackArgs args) => { return true; }, // 显示条件,一直可以显示
                game_menu_town_biwu_boxing_on_consequence); // 选中后的行动
            campaignGameStarter.AddGameMenuOption(
                "biwu_to_join", // 目标菜单-竞技场
                "yigu_biwu_fight", // 菜单选项的唯一标识
                "{=*}武斗(龙争虎斗)", // 菜单选项显示的文本
                (MenuCallbackArgs args) => { return true; }, // 显示条件,一直可以显示
                game_menu_town_biwu_fight_on_consequence); // 选中后的行动
            //campaignGameStarter.AddGameMenuOption(
            //    "biwu_to_join", // 目标菜单-竞技场
            //    "yigu_biwu_chiji", // 菜单选项的唯一标识
            //    "{=*}吃鸡大乱斗(鹿死谁手)", // 菜单选项显示的文本
            //    (MenuCallbackArgs args) => { return true; }, // 显示条件,一直可以显示
            //    game_menu_town_biwu_chiji_on_consequence); // 选中后的行动

            campaignGameStarter.AddGameMenuOption(
                "biwu_to_join", // 目标菜单-竞技场
                "finish_biwu", // 菜单选项的唯一标识
                "{=*}比武完成", // 菜单选项显示的文本
                (MenuCallbackArgs args) => { return true; }, // 显示条件,一直可以显示
                game_menu_town_biwu_finish_on_consequence); // 选中后的行动

            campaignGameStarter.AddGameMenuOption(
                "biwu_to_join", // 目标菜单-竞技场
                "yigu_biwu_leave", // 菜单选项的唯一标识
                "{=*}返回", // 菜单选项显示的文本
                (MenuCallbackArgs args) => { return true; }, // 显示条件,一直可以显示
                delegate (MenuCallbackArgs args)
                {
                    GameMenu.SwitchToMenu("town_arena");
                }); // 选中后的行动
        }

        /// @brief 初始化比武菜单的回调函数。
        /// @param args 菜单回调参数。
        private void game_menu_biwu_join_on_init(MenuCallbackArgs args)
        {

        }

        /// @brief 触发年度比武的后续逻辑。
        /// @param args 菜单回调参数。
        private static void game_menu_town_biwu_on_consequence(MenuCallbackArgs args)
        {
            InformationManager.DisplayMessage(new InformationMessage("{=*}2024年度比武触发!"));
            GameMenu.SwitchToMenu("biwu_to_join");

        }
        /// @brief 点击菜单选项后，打开比武这个任务。
        /// @param myGame 比武大会单场赛事
        /// @param args 菜单回调参数。
        private static void openMyGame(TournamentGame myGame, MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Mission;
            myGame.PrepareForTournamentGame(true);
            CampaignEventDispatcher.Instance.OnPlayerJoinedTournament(Settlement.CurrentSettlement.Town, true);
          
        }
        /// @brief 射箭比赛选项的后续逻辑。
        /// @param args 菜单回调参数。
        private static void game_menu_town_biwu_archery_on_consequence(MenuCallbackArgs args)
        {
            ArcheryTournamentGame myGame = AnnualTournamentManager.Instance.currentTournament.archeryGame;
            if (myGame != null)
            {
                openMyGame(myGame, args);
            }

        }
        /// @brief 骑马对枪选项的后续逻辑。
        /// @param args 菜单回调参数。
        private static void game_menu_town_biwu_jousting_on_consequence(MenuCallbackArgs args)
        {

            BiWuGame myGame = AnnualTournamentManager.Instance.currentTournament.joustingGame;
            if (myGame != null)
            {
                openMyGame(myGame, args);
            }
        }
        /// @brief 拳击选项的后续逻辑。
        /// @param args 菜单回调参数。
        private static void game_menu_town_biwu_boxing_on_consequence(MenuCallbackArgs args)
        {

            BiWuGame myGame = AnnualTournamentManager.Instance.currentTournament.boxingGame;
            if (myGame != null)
            {
                openMyGame(myGame, args);
            }
        }
        /// @brief 自由比武选项的后续逻辑。
        /// @param args 菜单回调参数。
        private static void game_menu_town_biwu_fight_on_consequence(MenuCallbackArgs args)
        {

            BiWuGame myGame = AnnualTournamentManager.Instance.currentTournament.fightGame;
            if (myGame != null)
            {
                openMyGame(myGame, args);
            }
        }
        private static void game_menu_town_biwu_chiji_on_consequence(MenuCallbackArgs args)
        {

        }
        private static void game_menu_town_biwu_finish_on_consequence(MenuCallbackArgs args)
        {
            if(AnnualTournamentManager.Instance.isGameBeingHeld)
            {
                if (AnnualTournamentManager.Instance.currentTournament.IsActive)
                    if (AnnualTournamentManager.Instance.currentTournament.AllFinished)
                    {
                        AnnualTournamentManager.Instance.currentTournament.OnEnd();
                    }
                    else
                    {
                        InformationManager.DisplayMessage(new InformationMessage("{=*}比武尚未结束，请参与完所有比赛后点击"));
                    }
            }
            
        }

    }

    public class AnnualTournamentManager
    {
        private static AnnualTournamentManager _instance;
        public AnnualTournament currentTournament; // 当前正在进行的比赛实例
        public bool isGameBeingHeld { get; private set; }// 表示当前是否正在进行一场比赛
        private int endDay = 25;
        private int startDay = 1;
        public Town town { get; private set; }
        public static AnnualTournamentManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new AnnualTournamentManager();
                }
                return _instance;
            }
        }

        private AnnualTournamentManager()
        {
            currentTournament = null;
            isGameBeingHeld = false;
            town = Settlement.Find("town_A1").Town;//测试，在阿塞莱的Quyaz
        }

        public void Initialize()
        {
            // 注册事件监听，触发比赛逻辑
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        private void OnDailyTick()
        {
            // 比赛触发逻辑，根据游戏日期检查是否需要触发新比赛
            if (!isGameBeingHeld && ShouldStartTournament())
            {
                StartNewTournament(this.town);
            }
            if (isGameBeingHeld && ShouldEndTournament())
            {
                EndTournament();
            }
        }

        private bool ShouldStartTournament()
        {
            // 检查条件，比如一年一度（检查日期），或手动触发
            int currentDay = CampaignTime.Now.GetDayOfYear;
            return currentDay == startDay; // 例如每年第一天触发
        }
        private bool ShouldEndTournament()
        {
            // 检查条件，比如一年一度（检查日期），或手动触发
            int currentDay = CampaignTime.Now.GetDayOfYear;
            return currentDay == endDay; // 例如每年第一天触发
        }
        public void StartNewTournament(Town venue)
        {
            if (isGameBeingHeld)
            {
                return; // 防止重复触发
            }
           
            currentTournament = new AnnualTournament(venue);

            currentTournament.OnStart();
            isGameBeingHeld = true;
            
            InformationManager.DisplayMessage(new InformationMessage($"年度比武正在 {venue.Name} 举办，请在25天内前往参赛"));

        }

        public void EndTournament()
        {
            if (currentTournament == null || !isGameBeingHeld)
            {
                return; // 没有进行中的比赛
            }

            currentTournament.OnEnd();
            InformationManager.DisplayMessage(new InformationMessage($"年度比武在 {currentTournament.vneue_town.Name} 圆满结束！"));

            currentTournament = null; // 清除当前比赛
            isGameBeingHeld = false;
        }
        
        public void StimulateTournament()
        {
            if (isGameBeingHeld && currentTournament != null)
            {
                currentTournament.Stimulate();
            }
        }

        public AnnualTournament GetCurrentTournament()
        {
            return currentTournament; // 返回当前进行中的比赛
        }
    }


    /// @brief 年度比赛积分管理类
    /// @details 此类用于根据比赛类型和排名获取相应的积分。
    public static class AnnualTournamentScoreManager
    {
        /// @brief 比赛类型枚举
        /// @details 定义了三种不同的比赛类型。
        public enum TournamentType
        {
            Triathlon,    ///< 铁人三项
            Combat,       ///< 武斗
            BattleRoyale  ///< 吃鸡大乱斗
        }

        /// @brief 铁人三项比赛的积分表
        /// @details 包括排名和对应的积分，排名1-4分别有固定分数，其他排名得1分。
        private static readonly Dictionary<int, int> triathlonScores = new Dictionary<int, int>()
    {
        { 1, 10 }, { 2, 6 }, { 3, 4 }, { 4, 1 }
    };

        /// @brief 武斗比赛的积分表
        /// @details 包括排名和对应的积分，排名1-4分别有固定分数，其他排名得2分。
        private static readonly Dictionary<int, int> combatScores = new Dictionary<int, int>()
    {
        { 1, 20 }, { 2, 15 }, { 3, 10 }, { 4, 2 }
    };

        /// @brief 吃鸡大乱斗比赛的积分表
        /// @details 包括排名和对应的积分，排名1-4分别有固定分数，其他排名得10分。
        private static readonly Dictionary<int, int> battleRoyaleScores = new Dictionary<int, int>()
    {
        { 1, 50 }, { 2, 40 }, { 3, 30 }, { 4, 10 }
    };

        /// @brief 根据比赛类型和排名获取积分
        /// @param tournamentType 比赛类型
        /// @param rank 选手的排名
        /// @return 返回对应的积分
        /// @exception ArgumentException 如果比赛类型无效，则抛出异常
        /// @details 此方法会根据比赛类型查找对应的积分表，返回排名对应的积分。如果排名不在积分表中，返回表中默认的最低积分。
        public static int GetScoreFromRank(TournamentType tournamentType, int rank)
        {
            switch (tournamentType)
            {
                case TournamentType.Triathlon:
                    return triathlonScores.ContainsKey(rank) ? triathlonScores[rank] : triathlonScores[4]; // 其他得1分
                case TournamentType.Combat:
                    return combatScores.ContainsKey(rank) ? combatScores[rank] : combatScores[4]; // 其他得2分
                case TournamentType.BattleRoyale:
                    return battleRoyaleScores.ContainsKey(rank) ? battleRoyaleScores[rank] : battleRoyaleScores[4]; // 其他得10分
                default:
                    throw new ArgumentException("Invalid tournament type.");
            }
        }
    }
    //brief 储存比武大会整体计分用的
    public class AnnualTournamentParticipant
    {
        public CharacterObject character;
        public int Score { get; private set; } // 分数
        public AnnualTournamentParticipant(CharacterObject the_character)
        {
            character = the_character;
            Score = 0;
        }
        public void AddScore(int points)
        {
            if (points < 0)
            {
                throw new ArgumentException("Points to add cannot be negative.");
            }
            Score += points;
        }

    }
    //brief 储存比武大会整体计分用的
    public class AnnualTournamentParticipants
    {
        public MBList<AnnualTournamentParticipant> participants;
        public MBList<CharacterObject> characters;
        public AnnualTournamentParticipants(MBList<CharacterObject> the_participants)
        {
            characters= new MBList<CharacterObject>();
            participants =new MBList<AnnualTournamentParticipant> ();
            foreach(CharacterObject the_participant in the_participants)
            {
                participants.Add(new AnnualTournamentParticipant(the_participant));
                characters.Add(the_participant);
            }
        }
        public void AddScoreToCharacter(CharacterObject character, int points)
        {
            if (points < 0)
            {
                throw new ArgumentException("Points to add cannot be negative.");
            }

            // 查找对应的参赛者
            AnnualTournamentParticipant participant = participants.FirstOrDefault(p => p.character == character);

            if (participant == null)
            {
                throw new ArgumentException("Character not found in the participants list.");
            }

            // 增加分数
            participant.AddScore(points);
        }
        // 获取分数前三名的方法
        public List<AnnualTournamentParticipant> GetTopThreeParticipants()
        {
            return participants
                .OrderByDescending(p => p.Score) // 按分数从大到小排序
                .Take(3) // 获取前三名
                .ToList(); // 转换为列表返回
        }
        public int GetHeroScore(CharacterObject character)
        {
            AnnualTournamentParticipant participant = participants.FirstOrDefault(p => p.character == character);
            return participant.Score;
        }
        //将英雄和分数转为字符串并返回
        public string GetHeroScoresAsString()
        {

            var scoreList = participants
                .OrderByDescending(p => p.Score) // 按分数降序排列
                .Select(p => $"{p.character.Name} - {p.Score}") // 格式化为 "英雄名 - 分数"
                .ToList();

            return string.Join("\n", scoreList); // 英雄之间用换行分隔
        }

    }

    public class AnnualTournament
    {
        //public MBList<CharacterObject> participants;
        public AnnualTournamentParticipants participants;
        public Town vneue_town
        {
            get; private set;
        }


        public ArcheryTournamentGame archeryGame;
        public BiWuGame joustingGame;
        public BiWuGame boxingGame;
        public BiWuGame fightGame;
        public MBList<ItemObject> prizeList;
        public bool IsActive;
        public bool AllFinished;
        public AnnualTournament(Town vneue_town)
        {
            this.vneue_town = vneue_town;
            this.prizeList = GetBiWuPrizeList();

            participants = new AnnualTournamentParticipants(GetParticipantCharacters());
            archeryGame = new ArcheryTournamentGame(vneue_town, participants.characters);
            //Campaign.Current.TournamentManager.AddTournament(archeryGame);

            joustingGame = new BiWuGame(vneue_town, BiWuGame.EquipmentType.Jousting, participants.characters, this);
            //Campaign.Current.TournamentManager.AddTournament(joustingGame);

            boxingGame = new BiWuGame(vneue_town, BiWuGame.EquipmentType.BoxingEquipment, participants.characters, this);
            //Campaign.Current.TournamentManager.AddTournament(boxingGame);

            fightGame = new BiWuGame(vneue_town, BiWuGame.EquipmentType.FullCharacterEquipment, participants.characters, this);
            // Campaign.Current.TournamentManager.AddTournament(fightGame);
            IsActive = true;
            AllFinished = false;
        }
        public MBList<ItemObject> GetBiWuPrizeList()
        {
            MBList<ItemObject> list= new MBList<ItemObject> ();
            string[] prizeIds = { "pernach_mace_t3", "noble_horse_western", "special_camel" };
            foreach (var id in prizeIds)
            {
                ItemObject prize = Game.Current.ObjectManager.GetObject<ItemObject>(id);
                if (prize != null)
                {
                    list.Add(prize);
                }
            }
            return list;
        }
        public void GivePrizeToOneWinner(CharacterObject winner, ItemObject prize)
        {
            if (winner.HeroObject.PartyBelongedTo == MobileParty.MainParty)
            {
                winner.HeroObject.PartyBelongedTo.ItemRoster.AddToCounts(prize, 1);
                return;
            }
            if (winner.HeroObject.Clan != null)
            {
                GiveGoldAction.ApplyBetweenCharacters(null, winner.HeroObject.Clan.Leader, vneue_town.MarketData.GetPrice(prize, null, false, null), false);
            }
        }
        public void GivePrizetoWinners()
        {
            for(int i=0;i<3; i++)
            {
                ItemObject prize = prizeList[i];
                CharacterObject winner = participants.GetTopThreeParticipants()[i].character;
                GivePrizeToOneWinner(winner,prize);
            }

        }

        public void OnStart()
        {
            //vneue_town.Settlement.CurrentSiegeState= Settlement.SiegeState.Invalid;
        }
        public void OnEnd()
        {
            //获得各个比赛的排名
            //根据排名生成分数
            //最终排名生成，奖品发放
            GivePrizetoWinners();
            IsActive = false;
        }
        public void Stimulate()
        {

        }
        public MBList<CharacterObject> GetParticipantCharacters(bool includePlayer = true)
        {
            MBList<CharacterObject> mblist = new MBList<CharacterObject>();

            // 获取排行榜中的英雄及其排名
            List<KeyValuePair<Hero, int>> heroList = Campaign.Current.TournamentManager.GetLeaderboard();
            if (includePlayer && !CharacterObject.PlayerCharacter.HeroObject.IsPrisoner)
            {
                mblist.Add(CharacterObject.PlayerCharacter);
            }
            // 取前 16 名英雄的 CharacterObject 并添加到 mblist 中

            int count = 0;
            foreach (var hero in heroList)
            {
                if (count >= 16)
                {
                    break; // 已经添加了16个英雄，退出循环
                }

                if (hero.Key != null && hero.Key.CharacterObject != null && !hero.Key.IsPrisoner)
                {
                    mblist.Add(hero.Key.CharacterObject);
                    count++; // 计数器加一，表示已添加一个符合条件的英雄
                }
            }
            //如果不够16个英雄，还需要拉一些贵族兵来凑数
           
            while (mblist.Count < 16)
            {
                CultureObject cultureObject = (this.vneue_town != null) ? this.vneue_town.Culture : Game.Current.ObjectManager.GetObject<CultureObject>("yizhou");
                CharacterObject item = (MBRandom.RandomFloat > 0.5f) ? cultureObject.BasicTroop : cultureObject.EliteBasicTroop;
                mblist.Add(item);
            }
           
            return mblist;
        }
        public void HealAllParticipants()
        {
            if (!participants.characters.IsEmpty())
            {
                foreach (CharacterObject participant in this.participants.characters)
                {
                    participant.HeroObject.Heal(participant.HeroObject.MaxHitPoints);
                }
                InformationManager.DisplayMessage(new InformationMessage("{=*}每位参与的英雄暂时摆脱了战事，得以回复体力"));
            }

        }
    }

    /// @brief 射箭锦标赛游戏类
    /// @details 此类继承自 TournamentGame 类，定义了射箭锦标赛的规则和行为。
    public class ArcheryTournamentGame : TournamentGame
    {
        /// @brief 获取团队的最大人数
        /// @return 返回团队的最大人数，固定为1。
        public override int MaxTeamSize
        {
            get
            {
                return 1;
            }
        }

        /// @brief 获取每场比赛的最大团队数
        /// @return 返回每场比赛的最大团队数，固定为1。
        public override int MaxTeamNumberPerMatch
        {
            get
            {
                return 1;
            }
        }

        /// @brief 获取锦标赛移除的时间限制
        /// @return 返回锦标赛在 15 天后移除。
        public override int RemoveTournamentAfterDays
        {
            get
            {
                return 15;
            }
        }

        /// @brief 获取最大参赛人数
        /// @return 返回最大参赛人数，固定为16。
        public override int MaximumParticipantCount
        {
            get
            {
                return 16;
            }
        }
        public MBList<CharacterObject> participants;
        /// @brief 构造函数
        /// @param town 举办锦标赛的城镇对象。
        /// @details 初始化射箭锦标赛游戏，并设置模式为个人得分赛。
        public ArcheryTournamentGame(Town town, MBList<CharacterObject> game_participants) : base(town, null)
        {
            base.Mode = TournamentGame.QualificationMode.IndividualScore;
            this.participants = game_participants;
        }
       
        /// @brief 检查角色是否符合参赛条件
        /// @param character 要检查的角色对象。
        /// @param considerSkills 是否考虑角色的技能。
        /// @return 返回角色是否可以参赛的布尔值。
        /// @details 英雄需要弓术技能值不低于125，普通角色需要达到5级或更高。
        public override bool CanBeAParticipant(CharacterObject character, bool considerSkills)
        {
            int requiredSkillValue = 125;
            if (!character.IsHero)
            {
                return character.Tier >= 5;
            }
            return !considerSkills || character.HeroObject.GetSkillValue(DefaultSkills.Bow) >= requiredSkillValue;
        }

        /// @brief 获取菜单文本
        /// @return 返回菜单文本。
        /// @details 当前文本为占位符。
        public override TextObject GetMenuText()
        {
            TextObject textObject = new TextObject("AAA");
            return textObject;
        }

        /// @brief 打开比赛场景
        /// @param settlement 比赛场景所在的地点。
        /// @param isPlayerParticipating 玩家是否参与比赛。
        /// @details 根据地点情况加载对应的射箭比赛场景。
        public override void OpenMission(Settlement settlement, bool isPlayerParticipating)
        {
            int upgradeLevel = settlement.IsTown ? settlement.Town.GetWallLevel() : 1;
            SandBoxMission.OpenTournamentArcheryMission(
                LocationComplex.Current.GetScene("arena", upgradeLevel),
                this,
                settlement,
                settlement.Culture,
                isPlayerParticipating
            );
        }

        /// @brief 获取参赛者名单
        /// @param settlement 比赛举办的地点。
        /// @param includePlayer 是否包括玩家角色。
        /// @return 返回参赛者的角色列表。
        /// @details 从排行榜中获取前16名英雄的角色对象并返回，玩家可以选择是否参与。
        public override MBList<CharacterObject> GetParticipantCharacters(Settlement settlement, bool includePlayer = true)
        {
            return this.participants;
        }

        /// @brief 获取锦标赛的奖励物品
        /// @param includePlayer 是否包括玩家。
        /// @param lastRecordedLordCountForTournamentPrize 最近记录的领主数量。
        /// @return 返回奖励物品对象。
        /// @details 当前奖励物品固定为 "spiked_helmet_with_facemask"。
        protected override ItemObject GetTournamentPrize(bool includePlayer, int lastRecordedLordCountForTournamentPrize)
        {
            string objectName = "spiked_helmet_with_facemask";
            return Game.Current.ObjectManager.GetObject<ItemObject>(objectName);
        }
    }
    /**
 * @class BiWuGame
 * @brief 表示比武大会的游戏类，继承自 TournamentGame。
 * 
 * 该类实现了比武大会的具体逻辑，包括参赛条件、比赛机制和奖励设定。
 */
    public class BiWuGame : TournamentGame
    {
        /**
    * @enum EquipmentType
    * @brief 定义比武大会的装备类型。
    * 
    * 包括：
    * - FullCharacterEquipment: 使用全部角色装备
    * - CharacterArmorOnly: 仅使用角色盔甲
    * - BoxingEquipment: 使用拳击装备
    * - Jousting: 使用对枪装备
    */
        public enum EquipmentType
        {
            FullCharacterEquipment,   // 全部使用角色装备
            CharacterArmorOnly,       // 只使用角色盔甲
            BoxingEquipment,           // 使用拳击装备
            Jousting                   //对枪装备
        }

        /**
    * @brief 获取比赛中每队的最大人数。
    * @return 返回当前装备类型下的最大队伍人数。
    */
        public override int MaxTeamSize
        {
            get
            {
                switch (this._equipmentType)
                {
                    case EquipmentType.FullCharacterEquipment:
                        return 4; // 例如，全部使用角色装备时返回 4
                    case EquipmentType.CharacterArmorOnly:
                        return 4; // 例如，只使用角色盔甲时返回 3
                    case EquipmentType.BoxingEquipment:
                        return 4; // 例如，使用拳击装备时返回 2
                    case EquipmentType.Jousting:
                        return 1; // 例如，使用对枪装备时返回 1
                    default:
                        return base.MaxTeamNumberPerMatch; // 默认值
                }
            }
        }
        /**
             * @brief 获取每场比赛中的最大队伍数量。
             * @return 返回当前装备类型下的最大队伍数量。
             */
        public override int MaxTeamNumberPerMatch
        {
            get
            {
                switch (this._equipmentType)
                {
                    case EquipmentType.FullCharacterEquipment:
                        return 4; // 例如，全部使用角色装备时返回 4
                    case EquipmentType.CharacterArmorOnly:
                        return 4; // 例如，只使用角色盔甲时返回 3
                    case EquipmentType.BoxingEquipment:
                        return 4; // 例如，使用拳击装备时返回 2
                    case EquipmentType.Jousting:
                        return 1; // 例如，使用对枪装备时返回 1
                    default:
                        return base.MaxTeamNumberPerMatch; // 默认值
                }
            }
        }

        /**
     * @brief 获取比赛结束后移除比武大会的天数。
     * @return 返回比赛结束后移除的天数（默认为 15）。
     */
        public override int RemoveTournamentAfterDays
        {
            get
            {
                return 15;
            }
        }

        /**
     * @brief 获取比赛中的最大参赛人数。
     * @return 返回最大参赛人数（默认为 16）。
     */
        public override int MaximumParticipantCount
        {
            get
            {
                return 16;
            }
        }
        public MBList<CharacterObject> participants;
        private AnnualTournament _annualTournament;
        /**
    * @brief 构造函数，初始化比武大会。
    * @param town 比武大会所属的城镇。
    * @param equipmentType 比武大会的装备类型。
    */
        public BiWuGame(Town town, EquipmentType equipmentType, MBList<CharacterObject> game_participants, AnnualTournament annualTournament) : base(town, null)
        {
            base.Mode = TournamentGame.QualificationMode.IndividualScore;
            this._equipmentType = equipmentType;
            this.participants = game_participants;
            this._annualTournament = annualTournament;
        }
        /**
    * @brief 判断角色是否可以成为参赛者。
    * @param character 待判断的角色对象。
    * @param considerSkills 是否考虑技能等级。
    * @return 返回角色是否符合参赛条件。
    */
        public override bool CanBeAParticipant(CharacterObject character, bool considerSkills)
        {
            if (!character.IsHero)
            {
                return character.Tier >= 3;
            }
            return !considerSkills || character.HeroObject.GetSkillValue(DefaultSkills.OneHanded) >= 100 || character.HeroObject.GetSkillValue(DefaultSkills.TwoHanded) >= 100;
        }
        /**
     * @brief 获取比赛菜单的文本描述。
     * @return 返回比赛菜单的文本对象。
     */
        public override TextObject GetMenuText()
        {
            TextObject textObject = new TextObject("qqq");
            return textObject;
        }

        /**
     * @brief 打开比武大会的任务场景。
     * @param settlement 当前比赛的地点。
     * @param isPlayerParticipating 玩家是否参与比赛。
     */
        public override void OpenMission(Settlement settlement, bool isPlayerParticipating)
        {
            int upgradeLevel = settlement.IsTown ? settlement.Town.GetWallLevel() : 1;
            //SandBoxMission.OpenTournamentFightMission(LocationComplex.Current.GetScene("arena", upgradeLevel), this, settlement, settlement.Culture, isPlayerParticipating);

            MissionState.OpenNew("TournamentFight", SandBoxMissions.CreateSandBoxMissionInitializerRecord(LocationComplex.Current.GetScene("arena", upgradeLevel), "", false, DecalAtlasGroup.Town), delegate (Mission missionController)
            {
                BiWuMissionController biwuMissionController = new BiWuMissionController(settlement.Culture, this._equipmentType);
                return new MissionBehavior[]
                {
                    new CampaignMissionComponent(),
                    new EquipmentControllerLeaveLogic(),
                    biwuMissionController,
                    new YiguTournamentBehavior(this, settlement, biwuMissionController, isPlayerParticipating,_annualTournament),
                    new AgentVictoryLogic(),
                    new MissionAgentPanicHandler(),
                    new AgentHumanAILogic(),
                    new ArenaAgentStateDeciderLogic(),
                    new MissionHardBorderPlacer(),
                    new MissionBoundaryPlacer(),
                    new MissionOptionsComponent(),
                    new HighlightsController(),
                    new SandboxHighlightsController()
                };
            }, true, true);
        }
        /**
     * @brief 获取比赛的参赛者列表。
     * @param settlement 当前比赛的地点。
     * @param includePlayer 是否包括玩家角色。
     * @return 返回参赛者的角色对象列表。
     */
        public override MBList<CharacterObject> GetParticipantCharacters(Settlement settlement, bool includePlayer = true)
        {
            return this.participants;
        }
        /**
     * @brief 判断是否允许NPC参与比赛
     * @param hero 英雄
     * @param participantCharacters 比赛参与者
     * @param considerSkills 是否考虑技能等级
     * @return 直接返回否
     */
        private bool CanNpcJoinTournament(Hero hero, MBList<CharacterObject> participantCharacters, bool considerSkills)
        {
            return false;
        }

        /**
 * @brief 获取部队的升级目标并添加到列表中。
 * @param troop 当前检查的部队对象。
 * @param list 用于存储可升级目标的部队列表（通过引用传递）。
 * 
 * 此方法递归检查指定部队及其所有升级目标，
 * 如果部队满足比赛参与条件且尚未在列表中，则将其添加到列表中。
 */
        private void GetUpgradeTargets(CharacterObject troop, ref List<CharacterObject> list)
        {
            if (!list.Contains(troop) && this.CanBeAParticipant(troop, false))
            {
                list.Add(troop);
            }
            foreach (CharacterObject troop2 in troop.UpgradeTargets)
            {
                this.GetUpgradeTargets(troop2, ref list);
            }
        }
        /**
 * @brief 对比赛参与者进行排序。
 * @param participantCharacters 比赛参与者的部队列表。
 * 
 * 此方法根据部队在比赛中的优先级分数对参与者列表进行降序排序。
 * 优先级分数通过 `GetTroopPriorityPointForTournament` 方法计算，
 * 分数高的部队排在前面。
 */
        private void SortTournamentParticipants(MBList<CharacterObject> participantCharacters)
        {
            for (int i = 0; i < participantCharacters.Count - 1; i++)
            {
                for (int j = participantCharacters.Count - 1; j > i; j--)
                {
                    if (this.GetTroopPriorityPointForTournament(participantCharacters[j]) > this.GetTroopPriorityPointForTournament(participantCharacters[i]))
                    {
                        CharacterObject value = participantCharacters[j];
                        CharacterObject value2 = participantCharacters[i];
                        participantCharacters[j] = value2;
                        participantCharacters[i] = value;
                    }
                }
            }
        }
        /**
 * @brief 获取部队在比赛中的优先级分数。
 * @param troop 要计算优先级分数的部队。
 * @return 返回计算后的优先级分数，分数越高优先级越高。
 * 
 * 根据部队的特性和状态计算其在比赛中的优先级分数：
 * - 如果是玩家角色，加 80000 分。
 * - 如果是英雄，加 20000 分。
 * - 如果是玩家的伙伴，加 10000 分。
 * - 如果有所属的家族，根据家族的声望加分。
 * - 否则，根据部队等级加分。
 */
        private int GetTroopPriorityPointForTournament(CharacterObject troop)
        {
            int num = 40000;
            if (troop == CharacterObject.PlayerCharacter)
            {
                num += 80000;
            }
            if (troop.IsHero)
            {
                num += 20000;
            }
            if (troop.IsHero && troop.HeroObject.IsPlayerCompanion)
            {
                num += 10000;
            }
            else
            {
                Hero heroObject = troop.HeroObject;
                if (((heroObject != null) ? heroObject.Clan : null) != null)
                {
                    int num2 = num;
                    Clan clan = troop.HeroObject.Clan;
                    num = num2 + (int)((clan != null) ? new float?(clan.Renown) : null).Value;
                }
                else
                {
                    num += troop.Level;
                }
            }
            return num;
        }
        /**
     * @brief 获取比赛奖励物品。
     * @param includePlayer 是否包括玩家。
     * @param lastRecordedLordCountForTournamentPrize 上次记录的奖励基础人数。
     * @return 返回奖励物品对象。
     */
        protected override ItemObject GetTournamentPrize(bool includePlayer, int lastRecordedLordCountForTournamentPrize)
        {
            string objectName = "spiked_helmet_with_facemask";
            return Game.Current.ObjectManager.GetObject<ItemObject>(objectName);
        }





        public const int ParticipantTroopMinimumTierLimit = 3;


        private EquipmentType _equipmentType;
    }


    /**
 * @brief 比武场任务控制器类。
 * 
 * 负责控制比武比赛的相关逻辑，包括初始化任务行为、比赛准备和比赛开始等功能。
 */
    public class BiWuMissionController : MissionLogic, ITournamentGameBehavior
    {
        /**
      * @brief 构造函数，初始化比武任务控制器。
      * @param culture 比赛使用的文化对象。
      * @param equipmentType 比赛使用的装备类型。
      */
        public BiWuMissionController(CultureObject culture, BiWuGame.EquipmentType equipmentType)
        {
            this._match = null;
            this._culture = culture;
            this._cheerStarted = false;
            this._currentTournamentAgents = new List<Agent>();
            this._currentTournamentMountAgents = new List<Agent>();
            this._equipmentType = equipmentType;
        }
        /**
     * @brief 初始化任务行为时的回调函数。
     * 
     * 添加任务行为的初始化逻辑，例如绑定特定的条件检查函数。
     */
        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            base.Mission.CanAgentRout_AdditionalCondition += this.CanAgentRout;
        }
        /**
    * @brief 比赛开始后的初始化逻辑。
    * 
    * 包括显示比赛开始的消息、清理多余的比赛场景元素，以及初始化生成点。
    */
        public override void AfterStart()
        {
            InformationManager.DisplayMessage(new InformationMessage("{=*}比赛开始!"));
            //InformationMessage message = new InformationMessage();
            TournamentBehavior.DeleteTournamentSetsExcept(base.Mission.Scene.FindEntityWithTag("tournament_fight"));
            this._spawnPoints = new List<GameEntity>();
            for (int i = 0; i < 4; i++)
            {
                GameEntity gameEntity = base.Mission.Scene.FindEntityWithTag("sp_arena_" + (i + 1));
                if (gameEntity != null)
                {
                    this._spawnPoints.Add(gameEntity);
                }
            }
            if (this._spawnPoints.Count < 4)
            {
                this._spawnPoints = base.Mission.Scene.FindEntitiesWithTag("sp_arena").ToList<GameEntity>();
            }


        }
        /**
     * @brief 准备比赛。
     * 
     * 为比赛的所有参赛选手分配装备。
     */
        public void PrepareForMatch()
        {
            foreach (TournamentTeam tournamentTeam in this._match.Teams)
            {
                int num = 0;
                foreach (TournamentParticipant tournamentParticipant in tournamentTeam.Participants)
                {

                    this.GiveEquipment(this._culture, tournamentParticipant);
                    num++;
                }
            }
        }
        /**
    * @brief 开始一场比赛。
    * @param match 当前比赛的匹配信息。
    * @param isLastRound 是否为最后一轮比赛。
    * 
    * 负责设置比赛模式、分配团队、生成参赛选手并设置团队敌对关系。
    */
        public void StartMatch(TournamentMatch match, bool isLastRound)
        {
            this._cheerStarted = false;
            this._match = match;
            this._isLastRound = isLastRound;
            this.PrepareForMatch();
            base.Mission.SetMissionMode(MissionMode.Battle, true);
            List<Team> list = new List<Team>();
            int count = this._spawnPoints.Count;
            int num = 0;
            foreach (TournamentTeam tournamentTeam in this._match.Teams)
            {
                BattleSideEnum side = tournamentTeam.IsPlayerTeam ? BattleSideEnum.Defender : BattleSideEnum.Attacker;
                Team team = base.Mission.Teams.Add(side, tournamentTeam.TeamColor, uint.MaxValue, tournamentTeam.TeamBanner, true, false, true);
                GameEntity spawnPoint = this._spawnPoints[num % count];
                foreach (TournamentParticipant tournamentParticipant in tournamentTeam.Participants)
                {
                    if (tournamentParticipant.Character.IsPlayerCharacter)
                    {
                        this.SpawnTournamentParticipant(spawnPoint, tournamentParticipant, team);
                        break;
                    }
                }
                foreach (TournamentParticipant tournamentParticipant2 in tournamentTeam.Participants)
                {
                    if (!tournamentParticipant2.Character.IsPlayerCharacter)
                    {
                        this.SpawnTournamentParticipant(spawnPoint, tournamentParticipant2, team);
                    }
                }
                num++;
                list.Add(team);
            }
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    list[i].SetIsEnemyOf(list[j], true);
                }
            }
            this._aliveParticipants = this._match.Participants.ToList<TournamentParticipant>();
            this._aliveTeams = this._match.Teams.ToList<TournamentTeam>();
            this.HandleHitPoints();
        }
        /**
 * @brief 处理参赛者的血量。
 * 
 * 在拳击赛中，根据装备类型调整当前参赛选手的血量和血量上限。
 */
        private void HandleHitPoints()
        {
            //拳击赛血量降低
            if (this._equipmentType == BiWuGame.EquipmentType.BoxingEquipment)
            {
                InformationManager.DisplayMessage(new InformationMessage("currentTournamentAgents.Count: " + this._currentTournamentAgents.Count.ToString()));
                foreach (Agent agent in this._currentTournamentAgents)
                {
                    agent.Health /= 5;
                    agent.HealthLimit /= 5;
                }
            }
        }
        /**
 * @brief 任务结束时的回调函数。
 * 
 * 在任务结束时取消附加的条件检查。
 */
        protected override void OnEndMission()
        {
            InformationManager.DisplayMessage(new InformationMessage($"比武结束回调函数"));
            base.Mission.CanAgentRout_AdditionalCondition -= this.CanAgentRout;
        }
        /**
 * @brief 生成并设置参赛选手。
 * 
 * @param spawnPoint 生成点实体，用于确定参赛选手的出生位置。
 * @param participant 比赛的参赛选手。
 * @param team 参赛选手所在的团队。
 */
        private void SpawnTournamentParticipant(GameEntity spawnPoint, TournamentParticipant participant, Team team)
        {
            MatrixFrame globalFrame = spawnPoint.GetGlobalFrame();
            globalFrame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
            this.SpawnAgentWithRandomItems(participant, team, globalFrame);
        }
        /**
 * @brief 获取指定团队人数的武器装备列表。
 * 
 * 根据团队人数选择相应的武器模板，并创建装备列表。
 * 
 * @param teamSize 团队人数，用于确定选择的武器模板。
 * @return 返回包含装备的列表。
 */
        private List<Equipment> GetTeamWeaponEquipmentList(int teamSize)
        {
            List<Equipment> list = new List<Equipment>();
            CultureObject culture = PlayerEncounter.EncounterSettlement.Culture;
            MBReadOnlyList<CharacterObject> mbreadOnlyList = (teamSize == 4) ? culture.TournamentTeamTemplatesForFourParticipant : ((teamSize == 2) ? culture.TournamentTeamTemplatesForTwoParticipant : culture.TournamentTeamTemplatesForOneParticipant);
            CharacterObject characterObject;
            if (mbreadOnlyList.Count > 0)
            {
                characterObject = mbreadOnlyList[MBRandom.RandomInt(mbreadOnlyList.Count)];
            }
            else
            {
                characterObject = ((teamSize == 4) ? this._defaultWeaponTemplatesIdTeamSizeFour : ((teamSize == 2) ? this._defaultWeaponTemplatesIdTeamSizeTwo : this._defaultWeaponTemplatesIdTeamSizeOne));
            }
            foreach (Equipment sourceEquipment in characterObject.BattleEquipments)
            {
                Equipment equipment = new Equipment();
                equipment.FillFrom(sourceEquipment, true);
                list.Add(equipment);
            }
            return list;
        }
        /**
 * @brief 跳过当前比赛并模拟比赛结果。
 * 
 * @param match 当前比赛的匹配信息。
 */
        public void SkipMatch(TournamentMatch match)
        {
            this._match = match;
            this.PrepareForMatch();
            this.Simulate();
        }
        /**
 * @brief 判断比赛是否已结束。
 * 
 * 根据比赛的状态和计时器判断比赛是否结束。
 * 
 * @return 如果比赛结束返回 true，否则返回 false。
 */
        public bool IsMatchEnded()
        {
            if (this._isSimulated || this._match == null)
            {
                return true;
            }
            if ((this._endTimer != null && this._endTimer.ElapsedTime > 6f) || this._forceEndMatch)
            {
                this._forceEndMatch = false;
                this._endTimer = null;
                return true;
            }
            if (this._cheerTimer != null && !this._cheerStarted && this._cheerTimer.ElapsedTime > 1f)
            {
                this.OnMatchResultsReady();
                this._cheerTimer = null;
                this._cheerStarted = true;
                AgentVictoryLogic missionBehavior = base.Mission.GetMissionBehavior<AgentVictoryLogic>();
                foreach (Agent agent in this._currentTournamentAgents)
                {
                    if (agent.IsAIControlled)
                    {
                        missionBehavior.SetTimersOfVictoryReactionsOnTournamentVictoryForAgent(agent, 1f, 3f);
                    }
                }
                return false;
            }
            if (this._endTimer == null && !this.CheckIfIsThereAnyEnemies())
            {
                this._endTimer = new BasicMissionTimer();
                if (!this._cheerStarted)
                {
                    this._cheerTimer = new BasicMissionTimer();
                }
            }
            return false;
        }
        /**
 * @brief 比赛结果准备时的回调函数。
 * 
 * 根据玩家是否参与比赛以及比赛结果，显示不同的提示信息。
 */
        public void OnMatchResultsReady()
        {
            if (!this._match.IsPlayerParticipating())
            {
                MBInformationManager.AddQuickInformation(new TextObject("{=*}比赛落幕", null), 0, null, "");
                return;
            }
            if (this._match.IsPlayerWinner())
            {
                if (this._isLastRound)
                {
                    if (this._match.QualificationMode == TournamentGame.QualificationMode.IndividualScore)
                    {
                        MBInformationManager.AddQuickInformation(new TextObject("{=*}回合结束，你成功坚持到最后一轮", null), 0, null, "");
                        return;
                    }
                    MBInformationManager.AddQuickInformation(new TextObject("{=*}回合结束，你的队伍成功坚持到最后一轮", null), 0, null, "");
                    return;
                }
                else
                {
                    if (this._match.QualificationMode == TournamentGame.QualificationMode.IndividualScore)
                    {
                        MBInformationManager.AddQuickInformation(new TextObject("{=*}回合结束，你成功晋级下一轮", null), 0, null, "");
                        return;
                    }
                    MBInformationManager.AddQuickInformation(new TextObject("{=*}回合结束，你的队伍成功晋级下一轮", null), 0, null, "");
                    return;
                }
            }
            else
            {
                if (this._match.QualificationMode == TournamentGame.QualificationMode.IndividualScore)
                {
                    MBInformationManager.AddQuickInformation(new TextObject("{=*}回合结束，你被淘汰", null), 0, null, "");
                    return;
                }
                MBInformationManager.AddQuickInformation(new TextObject("{=*}回合结束，你的队伍被淘汰", null), 0, null, "");
                return;
            }
        }
        /**
 * @brief 比赛结束时的回调函数。
 * 
 * 结束比赛时，清理比赛相关的状态，如移除当前参赛选手、清理尸体、清除队伍数据等。
 */
        public void OnMatchEnded()
        {
            SandBoxHelpers.MissionHelper.FadeOutAgents(from x in this._currentTournamentAgents
                                                       where x.IsActive()
                                                       select x, true, false);
            SandBoxHelpers.MissionHelper.FadeOutAgents(from x in this._currentTournamentMountAgents
                                                       where x.IsActive()
                                                       select x, true, false);
            base.Mission.ClearCorpses(false);
            base.Mission.Teams.Clear();
            base.Mission.RemoveSpawnedItemsAndMissiles();
            this._match = null;
            this._endTimer = null;
            this._cheerTimer = null;
            this._isSimulated = false;
            this._currentTournamentAgents.Clear();
            this._currentTournamentMountAgents.Clear();
        }
        /**
 * @brief 根据随机位置生成参赛选手并添加到比赛中。
 * 
 * @param participant 比赛的参赛选手。
 * @param team 参赛选手所在的队伍。
 * @param frame 参赛选手的生成位置和方向。
 */
        private void SpawnAgentWithRandomItems(TournamentParticipant participant, Team team, MatrixFrame frame)
        {
            frame.Strafe((float)MBRandom.RandomInt(-2, 2) * 1f);
            frame.Advance((float)MBRandom.RandomInt(0, 2) * 1f);
            CharacterObject character = participant.Character;
            AgentBuildData agentBuildData = new AgentBuildData(new SimpleAgentOrigin(character, -1, null, participant.Descriptor)).Team(team).InitialPosition(frame.origin);
            Vec2 vec = frame.rotation.f.AsVec2;
            vec = vec.Normalized();
            AgentBuildData agentBuildData2 = agentBuildData.InitialDirection(vec).Equipment(participant.MatchEquipment).ClothingColor1(team.Color).Banner(team.Banner).Controller(character.IsPlayerCharacter ? Agent.ControllerType.Player : Agent.ControllerType.AI);
            Agent agent = base.Mission.SpawnAgent(agentBuildData2, false);
            if (character.IsPlayerCharacter)
            {
                agent.Health = (float)character.HeroObject.HitPoints;
                base.Mission.PlayerTeam = team;
            }
            else
            {
                agent.SetWatchState(Agent.WatchState.Alarmed);
            }
            agent.WieldInitialWeapons(Agent.WeaponWieldActionType.InstantAfterPickUp, Equipment.InitialWeaponEquipPreference.Any);
            this._currentTournamentAgents.Add(agent);
            if (agent.HasMount)
            {
                this._currentTournamentMountAgents.Add(agent.MountAgent);
            }
        }
        /**
 * @brief 为参赛选手提供相应的装备。
 * 
 * 根据装备类型为参赛选手配置不同的装备。支持的装备类型包括拳击装备、全套角色装备和骑士比赛装备。
 * 
 * @param culture 文化对象，用于确定与文化相关的装备类型（当前未使用）。
 * @param participant 参赛选手，装备将被添加到其 `MatchEquipment` 中。
 */
        private void GiveEquipment(CultureObject culture, TournamentParticipant participant)
        {
            participant.MatchEquipment = new Equipment();
            switch (this._equipmentType)
            {
                case BiWuGame.EquipmentType.BoxingEquipment:
                    {

                        break;
                    }
                case BiWuGame.EquipmentType.FullCharacterEquipment:
                    {
                        Equipment participantArmor = Campaign.Current.Models.TournamentModel.GetParticipantArmor(participant.Character);
                        for (int i = 0; i < 11; i++)
                        {
                            EquipmentElement equipmentFromSlot = participantArmor.GetEquipmentFromSlot((EquipmentIndex)i);
                            if (equipmentFromSlot.Item != null)
                            {
                                participant.MatchEquipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)i, equipmentFromSlot);
                            }
                        }
                        break;
                    }
                case BiWuGame.EquipmentType.Jousting:
                    {
                        Equipment participantArmor = Campaign.Current.Models.TournamentModel.GetParticipantArmor(participant.Character);
                        participant.MatchEquipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Horse, new EquipmentElement(Game.Current.ObjectManager.GetObject<ItemObject>("charger"), null, null, false));
                        participant.MatchEquipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.HorseHarness, new EquipmentElement(Game.Current.ObjectManager.GetObject<ItemObject>("chain_horse_harness"), null, null, false));//horse_harness_e
                        participant.MatchEquipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.WeaponItemBeginSlot, new EquipmentElement(Game.Current.ObjectManager.GetObject<ItemObject>("vlandia_lance_2_t4"), null, null, false));

                        for (int i = 5; i < 10; i++)
                        {

                            EquipmentElement equipmentFromSlot = participantArmor.GetEquipmentFromSlot((EquipmentIndex)i);
                            if (equipmentFromSlot.Item != null)
                            {
                                participant.MatchEquipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)i, equipmentFromSlot);
                            }
                        }
                        break;
                    }
            }

        }
        /**
 * @brief 检查指定队伍是否已全部死亡。
 * 
 * 判断指定队伍的所有参赛选手是否仍然存活，如果有任何一个选手存活，返回 `false`，否则返回 `true`。
 * 
 * @param affectedParticipantTeam 受影响的队伍。
 * @return 如果队伍全部死亡，返回 `true`，否则返回 `false`。
 */
        private bool CheckIfTeamIsDead(TournamentTeam affectedParticipantTeam)
        {
            bool result = true;
            using (List<TournamentParticipant>.Enumerator enumerator = this._aliveParticipants.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current.Team == affectedParticipantTeam)
                    {
                        result = false;
                        break;
                    }
                }
            }
            return result;
        }
        /**
 * @brief 为所有存活的队伍加分。
 * 
 * 遍历所有存活的队伍和其中的参赛选手，并为每个选手添加 1 分。
 */
        private void AddScoreToRemainingTeams()
        {
            foreach (TournamentTeam tournamentTeam in this._aliveTeams)
            {
                foreach (TournamentParticipant tournamentParticipant in tournamentTeam.Participants)
                {
                    tournamentParticipant.AddScore(1);
                }
            }
        }
        /**
 * @brief 处理参赛选手被移除时的逻辑。
 * 
 * 当一个参赛选手被移除时，如果比赛未结束且移除者与受害者不是同一个人，且两者都为人类角色，则从存活参赛选手列表和当前比赛选手列表中移除该选手。
 * 如果该选手所在的队伍已死亡，则从存活队伍列表中移除该队伍，并为剩余队伍加分。
 * 
 * @param affectedAgent 受害的代理人。
 * @param affectorAgent 造成受害的代理人。
 * @param agentState 受害代理人当前的状态。
 * @param killingBlow 造成死亡的致命一击。
 */
        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow killingBlow)
        {
            if (!this.IsMatchEnded() && affectorAgent != null && affectedAgent != affectorAgent && affectedAgent.IsHuman && affectorAgent.IsHuman)
            {
                TournamentParticipant participant = this._match.GetParticipant(affectedAgent.Origin.UniqueSeed);
                this._aliveParticipants.Remove(participant);
                this._currentTournamentAgents.Remove(affectedAgent);
                if (this.CheckIfTeamIsDead(participant.Team))
                {
                    this._aliveTeams.Remove(participant.Team);
                    this.AddScoreToRemainingTeams();
                }
            }
        }
        /**
 * @brief 检查代理人是否能逃跑。
 * 
 * 目前该方法始终返回 `false`，表示代理人无法逃跑。
 * 
 * @param agent 需要检查的代理人。
 * @return 始终返回 `false`，表示代理人无法逃跑。
 */
        public bool CanAgentRout(Agent agent)
        {
            return false;
        }


        /**
 * @brief 处理在比赛中被击中得分的事件
 * 
 * 当一个代理被击中并导致得分变化时触发此方法。该方法会检查被击中代理和攻击代理的有效性，并计算造成的伤害。
 * 基于所造成的伤害，调用 `EnemyHitReward` 方法来奖励攻击者。
 * 
 * @param affectedAgent 受击的代理
 * @param affectorAgent 造成伤害的代理
 * @param attackerWeapon 攻击者使用的武器
 * @param isBlocked 是否被挡住
 * @param isSiegeEngineHit 是否为攻城器械的击打
 * @param blow 造成的击打数据，包含伤害信息
 * @param collisionData 碰撞数据
 * @param damagedHp 受击代理失去的生命值
 * @param hitDistance 攻击发生的距离
 * @param shotDifficulty 攻击的难度系数
 */
        public override void OnScoreHit(Agent affectedAgent, Agent affectorAgent, WeaponComponentData attackerWeapon, bool isBlocked, bool isSiegeEngineHit, in Blow blow, in AttackCollisionData collisionData, float damagedHp, float hitDistance, float shotDifficulty)
        {
            if (affectorAgent == null)
            {
                return;
            }
            if (affectorAgent.IsMount && affectorAgent.RiderAgent != null)
            {
                affectorAgent = affectorAgent.RiderAgent;
            }
            if (affectorAgent.Character == null || affectedAgent.Character == null)
            {
                return;
            }
            float num = (float)blow.InflictedDamage;


            float num2 = num / affectedAgent.HealthLimit;
            this.EnemyHitReward(affectedAgent, affectorAgent, blow.MovementSpeedDamageModifier, shotDifficulty, attackerWeapon, blow.AttackType, 0.5f * num2, num);
        }

        /**
 * @brief 奖励攻击者击中敌人时的经验和技能升级
 * 
 * 该方法用于处理攻击者对敌人的击打，计算并奖励攻击者的经验值。方法通过判断是否为马背冲撞并记录伤害值，
 * 更新相关角色的技能等级。
 * 
 * @param affectedAgent 受击的代理
 * @param affectorAgent 攻击者代理
 * @param lastSpeedBonus 上一回合速度加成
 * @param lastShotDifficulty 上一回合的射击难度
 * @param lastAttackerWeapon 攻击者使用的武器
 * @param attackType 攻击类型
 * @param hitpointRatio 受击比例
 * @param damageAmount 造成的伤害
 */
        private void EnemyHitReward(Agent affectedAgent, Agent affectorAgent, float lastSpeedBonus, float lastShotDifficulty, WeaponComponentData lastAttackerWeapon, AgentAttackType attackType, float hitpointRatio, float damageAmount)
        {
            CharacterObject affectedCharacter = (CharacterObject)affectedAgent.Character;
            CharacterObject affectorCharacter = (CharacterObject)affectorAgent.Character;
            if (affectedAgent.Origin != null && affectorAgent != null && affectorAgent.Origin != null)
            {
                bool isHorseCharge = affectorAgent.MountAgent != null && attackType == AgentAttackType.Collision;
                SkillLevelingManager.OnCombatHit(affectorCharacter, affectedCharacter, null, null, lastSpeedBonus, lastShotDifficulty, lastAttackerWeapon, hitpointRatio, CombatXpModel.MissionTypeEnum.Tournament, affectorAgent.MountAgent != null, affectorAgent.Team == affectedAgent.Team, false, damageAmount, affectedAgent.Health < 1f, false, isHorseCharge);
            }
        }
        /**
 * @brief 检查是否存在敌方团队
 * 
 * 该方法遍历当前比赛中的所有代理，检查是否有敌对团队的存在。若当前有不同团队的代理同时参与比赛，
 * 则返回 true，否则返回 false。
 * 
 * @return 如果有敌方团队，则返回 true；否则返回 false。
 */
        public bool CheckIfIsThereAnyEnemies()
        {
            Team team = null;
            foreach (Agent agent in this._currentTournamentAgents)
            {
                if (agent.IsHuman && agent.IsActive() && agent.Team != null)
                {
                    if (team == null)
                    {
                        team = agent.Team;
                    }
                    else if (team != agent.Team)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        /**
 * @brief 模拟比赛过程
 * 
 * 该方法用于模拟比赛的进行。首先，检查当前比赛的代理列表是否为空。如果为空，初始化参与者和队伍。接着，根据玩家角色找出其所在的队伍，并重置队伍成员的得分。然后，模拟比赛的过程，随机选择两个不同队伍的参与者进行对战，比较他们的攻击力，决定战斗结果，并更新参与者的状态。直到剩下的队伍或参与者数量少于等于1时，模拟结束。
 * 
 * @note 模拟过程中会根据每个参与者的攻击力差异进行胜负判断，并在队伍死亡时移除该队伍。
 */
        private void Simulate()
        {
            this._isSimulated = false;
            if (this._currentTournamentAgents.Count == 0)
            {
                this._aliveParticipants = this._match.Participants.ToList<TournamentParticipant>();
                this._aliveTeams = this._match.Teams.ToList<TournamentTeam>();
            }
            TournamentParticipant tournamentParticipant = this._aliveParticipants.FirstOrDefault((TournamentParticipant x) => x.Character == CharacterObject.PlayerCharacter);
            if (tournamentParticipant != null)
            {
                TournamentTeam team = tournamentParticipant.Team;
                foreach (TournamentParticipant tournamentParticipant2 in team.Participants)
                {
                    tournamentParticipant2.ResetScore();
                    this._aliveParticipants.Remove(tournamentParticipant2);
                }
                this._aliveTeams.Remove(team);
                this.AddScoreToRemainingTeams();
            }
            Dictionary<TournamentParticipant, Tuple<float, float>> dictionary = new Dictionary<TournamentParticipant, Tuple<float, float>>();
            foreach (TournamentParticipant tournamentParticipant3 in this._aliveParticipants)
            {
                float item;
                float item2;
                tournamentParticipant3.Character.GetSimulationAttackPower(out item, out item2, tournamentParticipant3.MatchEquipment);
                dictionary.Add(tournamentParticipant3, new Tuple<float, float>(item, item2));
            }
            int num = 0;
            while (this._aliveParticipants.Count > 1 && this._aliveTeams.Count > 1)
            {
                num++;
                num %= this._aliveParticipants.Count;
                TournamentParticipant tournamentParticipant4 = this._aliveParticipants[num];
                int num2;
                TournamentParticipant tournamentParticipant5;
                do
                {
                    num2 = MBRandom.RandomInt(this._aliveParticipants.Count);
                    tournamentParticipant5 = this._aliveParticipants[num2];
                }
                while (tournamentParticipant4 == tournamentParticipant5 || tournamentParticipant4.Team == tournamentParticipant5.Team);
                if (dictionary[tournamentParticipant5].Item2 - dictionary[tournamentParticipant4].Item1 > 0f)
                {
                    dictionary[tournamentParticipant5] = new Tuple<float, float>(dictionary[tournamentParticipant5].Item1, dictionary[tournamentParticipant5].Item2 - dictionary[tournamentParticipant4].Item1);
                }
                else
                {
                    dictionary.Remove(tournamentParticipant5);
                    this._aliveParticipants.Remove(tournamentParticipant5);
                    if (this.CheckIfTeamIsDead(tournamentParticipant5.Team))
                    {
                        this._aliveTeams.Remove(tournamentParticipant5.Team);
                        this.AddScoreToRemainingTeams();
                    }
                    if (num2 < num)
                    {
                        num--;
                    }
                }
            }
            this._isSimulated = true;
        }
        /**
 * @brief 判断是否有玩家控制的代理
 * 
 * 该方法用于检查当前比赛中是否有玩家控制的代理。如果主代理存在且处于激活状态，则返回 true；否则，检查当前比赛中的代理列表，判断是否有玩家控制的代理。
 * 
 * @return 如果存在玩家控制的代理，则返回 true；否则返回 false。
 */
        private bool IsThereAnyPlayerAgent()
        {
            if (base.Mission.MainAgent != null && base.Mission.MainAgent.IsActive())
            {
                return true;
            }
            return this._currentTournamentAgents.Any((Agent agent) => agent.IsPlayerControlled);
        }

        /**
 * @brief 跳过当前比赛
 * 
 * 该方法用于跳过当前的比赛。当调用此方法时，比赛行为会跳过，继续进行后续操作。
 */
        private void SkipMatch()
        {
            Mission.Current.GetMissionBehavior<TournamentBehavior>().SkipMatch(false);
        }

        /**
 * @brief 处理比赛结束后的请求
 * 
 * 该方法在比赛结束时被调用，判断玩家是否可以离开当前比赛，并根据比赛情况生成相应的询问数据。首先检查当前比赛是否存在，如果玩家参与了比赛并且存在玩家控制的代理，判断玩家是否接近敌人或者是否有其他敌人存在。如果有敌人，则生成一个询问数据，询问玩家是否要放弃比赛；否则强制结束比赛。如果没有敌人或玩家不参与比赛，依旧会生成跳过比赛的询问数据。
 * 
 * @param canPlayerLeave 返回值，表示玩家是否可以离开比赛。
 * 
 * @return 返回一个包含询问数据的对象，用于决定比赛结束后的操作。
 */

        public override InquiryData OnEndMissionRequest(out bool canPlayerLeave)
        {
            InquiryData result = null;
            canPlayerLeave = true;
            if (this._match != null)
            {
                if (this._match.IsPlayerParticipating())
                {
                    MBTextManager.SetTextVariable("SETTLEMENT_NAME", Hero.MainHero.CurrentSettlement.EncyclopediaLinkWithName, false);
                    if (this.IsThereAnyPlayerAgent())
                    {
                        if (base.Mission.IsPlayerCloseToAnEnemy(5f))
                        {
                            canPlayerLeave = false;
                            MBInformationManager.AddQuickInformation(GameTexts.FindText("str_can_not_retreat", null), 0, null, "");
                        }
                        else if (this.CheckIfIsThereAnyEnemies())
                        {
                            result = new InquiryData(GameTexts.FindText("str_tournament", null).ToString(), GameTexts.FindText("str_tournament_forfeit_game", null).ToString(), true, true, GameTexts.FindText("str_yes", null).ToString(), GameTexts.FindText("str_no", null).ToString(), new Action(this.SkipMatch), null, "", 0f, null, null, null);
                        }
                        else
                        {
                            this._forceEndMatch = true;
                            canPlayerLeave = false;
                        }
                    }
                    else if (this.CheckIfIsThereAnyEnemies())
                    {
                        result = new InquiryData(GameTexts.FindText("str_tournament", null).ToString(), GameTexts.FindText("str_tournament_skip", null).ToString(), true, true, GameTexts.FindText("str_yes", null).ToString(), GameTexts.FindText("str_no", null).ToString(), new Action(this.SkipMatch), null, "", 0f, null, null, null);
                    }
                    else
                    {
                        this._forceEndMatch = true;
                        canPlayerLeave = false;
                    }
                }
                else if (this.CheckIfIsThereAnyEnemies())
                {
                    result = new InquiryData(GameTexts.FindText("str_tournament", null).ToString(), GameTexts.FindText("str_tournament_skip", null).ToString(), true, true, GameTexts.FindText("str_yes", null).ToString(), GameTexts.FindText("str_no", null).ToString(), new Action(this.SkipMatch), null, "", 0f, null, null, null);
                }
                else
                {
                    this._forceEndMatch = true;
                    canPlayerLeave = false;
                }
            }
            return result;
        }

        /**
 * @brief 默认武器模板，用于单人团队的比赛
 * 
 * 该变量存储了单人团队比赛时使用的默认武器模板对象，通过MBObjectManager实例化。
 */
        private readonly CharacterObject _defaultWeaponTemplatesIdTeamSizeOne = MBObjectManager.Instance.GetObject<CharacterObject>("tournament_template_empire_one_participant_set_v1");

        /**
         * @brief 默认武器模板，用于双人团队的比赛
         * 
         * 该变量存储了双人团队比赛时使用的默认武器模板对象，通过MBObjectManager实例化。
         */
        private readonly CharacterObject _defaultWeaponTemplatesIdTeamSizeTwo = MBObjectManager.Instance.GetObject<CharacterObject>("tournament_template_empire_two_participant_set_v1");

        /**
         * @brief 默认武器模板，用于四人团队的比赛
         * 
         * 该变量存储了四人团队比赛时使用的默认武器模板对象，通过MBObjectManager实例化。
         */
        private readonly CharacterObject _defaultWeaponTemplatesIdTeamSizeFour = MBObjectManager.Instance.GetObject<CharacterObject>("tournament_template_empire_four_participant_set_v1");

        /**
         * @brief 当前比赛对象
         * 
         * 该变量存储当前正在进行的比赛的实例，用于管理比赛的各项内容。
         */
        private TournamentMatch _match;

        /**
         * @brief 标记当前是否为最后一轮
         * 
         * 该布尔值用于标记比赛是否处于最后一轮。
         */
        private bool _isLastRound;

        /**
         * @brief 比赛结束计时器
         * 
         * 用于控制比赛结束的倒计时。此计时器控制比赛结束的时机。
         */
        private BasicMissionTimer _endTimer;

        /**
         * @brief 欢呼计时器
         * 
         * 用于控制比赛中的欢呼时机，确保在合适的时机触发欢呼。
         */
        private BasicMissionTimer _cheerTimer;

        /**
         * @brief 比赛生成点集合
         * 
         * 存储所有的比赛生成点对象，用于决定比赛中参与者的生成位置。
         */
        private List<GameEntity> _spawnPoints;

        /**
         * @brief 当前比赛是否已模拟
         * 
         * 该布尔值用于标记当前比赛是否已经完成模拟过程。
         */
        private bool _isSimulated;

        /**
         * @brief 强制结束比赛标记
         * 
         * 该布尔值用于标记比赛是否被强制结束，通常用于特殊情况的处理。
         */
        private bool _forceEndMatch;

        /**
         * @brief 欢呼是否开始的标记
         * 
         * 该布尔值用于标记比赛中欢呼过程是否已开始。
         */
        private bool _cheerStarted;

        /**
         * @brief 比赛文化对象
         * 
         * 该变量存储比赛相关的文化对象，定义了比赛的文化背景和相关规则。
         */
        private CultureObject _culture;

        /**
         * @brief 存活的参赛者列表
         * 
         * 存储当前比赛中所有存活的参赛者对象。
         */
        private List<TournamentParticipant> _aliveParticipants;

        /**
         * @brief 存活的队伍列表
         * 
         * 存储当前比赛中所有存活的队伍对象。
         */
        private List<TournamentTeam> _aliveTeams;

        /**
         * @brief 当前比赛中的所有代理对象
         * 
         * 存储当前比赛中所有参赛者的代理对象，用于管理比赛过程中的各个代理。
         */
        private List<Agent> _currentTournamentAgents;

        /**
         * @brief 当前比赛中的所有坐骑代理对象
         * 
         * 存储当前比赛中所有参赛者的坐骑代理对象，管理比赛中的骑乘部分。
         */
        private List<Agent> _currentTournamentMountAgents;

        /**
         * @brief 击杀经验值分享比例
         * 
         * 该常量定义了击杀后经验值的分享比例。默认比例为0.5。
         */
        private const float XpShareForKill = 0.5f;

        /**
         * @brief 伤害经验值分享比例
         * 
         * 该常量定义了伤害后的经验值分享比例。默认比例为0.5。
         */
        private const float XpShareForDamage = 0.5f;

        /**
         * @brief 设备类型
         * 
         * 该变量定义了比赛使用的设备类型，决定比赛中使用的装备类别。
         */
        private BiWuGame.EquipmentType _equipmentType;

    }



    public class YiguTournamentBehavior : MissionLogic, ICameraModeLogic
    {
       
        public TournamentGame TournamentGame
        {
            get
            {
                return this._tournamentGame;
            }
        }

        
        public TournamentRound[] Rounds { get; private set; }

      
        public SpectatorCameraTypes GetMissionCameraLockMode(bool lockedToMainPlayer)
        {
            if (!this.IsPlayerParticipating)
            {
                return SpectatorCameraTypes.LockToAnyAgent;
            }
            return SpectatorCameraTypes.Invalid;
        }

       
        public bool IsPlayerEliminated { get; private set; }

        
        public int CurrentRoundIndex { get; private set; }

       
        public TournamentMatch LastMatch { get; private set; }

       
        public TournamentRound CurrentRound
        {
            get
            {
                return this.Rounds[this.CurrentRoundIndex];
            }
        }

        
        public TournamentRound NextRound
        {
            get
            {
                if (this.CurrentRoundIndex != 3)
                {
                    return this.Rounds[this.CurrentRoundIndex + 1];
                }
                return null;
            }
        }

       
        public TournamentMatch CurrentMatch
        {
            get
            {
                return this.CurrentRound.CurrentMatch;
            }
        }

        
        public TournamentParticipant Winner { get; private set; }

       
        public bool IsPlayerParticipating { get; private set; }

       
        public Settlement Settlement { get; private set; }

        private AnnualTournament _annualTournament; // 引用 AnnualTournament

        public YiguTournamentBehavior(TournamentGame tournamentGame, Settlement settlement, ITournamentGameBehavior gameBehavior, bool isPlayerParticipating, AnnualTournament annualTournament)
        {
            this.Settlement = settlement;
            this._tournamentGame = tournamentGame;
            this._gameBehavior = gameBehavior;
            this.Rounds = new TournamentRound[4];
            this.CreateParticipants(isPlayerParticipating);
            this.CurrentRoundIndex = -1;
            this.LastMatch = null;
            this.Winner = null;
            this.IsPlayerParticipating = isPlayerParticipating;
            _annualTournament = annualTournament; // 初始化 AnnualTournament 引用
        }

       
        public MBList<CharacterObject> GetAllPossibleParticipants()
        {
            return this._tournamentGame.GetParticipantCharacters(this.Settlement, true);
        }

        
        private void CreateParticipants(bool includePlayer)
        {
            this._participants = new TournamentParticipant[this._tournamentGame.MaximumParticipantCount];
            MBList<CharacterObject> participantCharacters = this._tournamentGame.GetParticipantCharacters(this.Settlement, includePlayer);
            participantCharacters.Shuffle<CharacterObject>();
            int num = 0;
            while (num < participantCharacters.Count && num < this._tournamentGame.MaximumParticipantCount)
            {
                this._participants[num] = new TournamentParticipant(participantCharacters[num], default(UniqueTroopDescriptor));
                num++;
            }
        }

       
        public static void DeleteTournamentSetsExcept(GameEntity selectedSetEntity)
        {
            List<GameEntity> list = Mission.Current.Scene.FindEntitiesWithTag("arena_set").ToList<GameEntity>();
            list.Remove(selectedSetEntity);
            foreach (GameEntity gameEntity in list)
            {
                gameEntity.Remove(93);
            }
        }
         
      
        public static void DeleteAllTournamentSets()
        {
            foreach (GameEntity gameEntity in Mission.Current.Scene.FindEntitiesWithTag("arena_set").ToList<GameEntity>())
            {
                gameEntity.Remove(94);
            }
        }

        
        public override void AfterStart()
        {
            this.CurrentRoundIndex = 0;
            this.CreateTournamentTree();
            this.FillParticipants(this._participants.ToList<TournamentParticipant>());
            this.CalculateBet();
        }

        
        public override void OnMissionTick(float dt)
        {
            if (this.CurrentMatch != null && this.CurrentMatch.State == TournamentMatch.MatchState.Started && this._gameBehavior.IsMatchEnded())
            {
                this.EndCurrentMatch(false);
            }
        }

        
        public void StartMatch()
        {
            if (this.CurrentMatch.IsPlayerParticipating())
            {
                Campaign.Current.TournamentManager.OnPlayerJoinMatch(this._tournamentGame.GetType());
            }
            this.CurrentMatch.Start();
            base.Mission.SetMissionMode(MissionMode.Tournament, true);
            this._gameBehavior.StartMatch(this.CurrentMatch, this.NextRound == null);
            CampaignEventDispatcher.Instance.OnPlayerStartedTournamentMatch(this.Settlement.Town);
        }

        
        public void SkipMatch(bool isLeave = false)
        {
            this.CurrentMatch.Start();
            this._gameBehavior.SkipMatch(this.CurrentMatch);
            this.EndCurrentMatch(isLeave);
        }

        public void ApplyScoreForAnnualTournamentParticipants()
        {
            if (_participants == null || _participants.Length == 0 || _annualTournament == null)
            {
                throw new InvalidOperationException("Participants or annual tournament is not properly initialized.");
            }

            // 按分数从高到低排序
            var sortedParticipants = _participants
                .OrderByDescending(p => p.Score)
                .ToArray();

            // 遍历排序后的参赛者并赋予分数
            for (int i = 0; i < sortedParticipants.Length; i++)
            {
                TournamentParticipant participant = sortedParticipants[i];
                int scoreToAdd;

                if (i == 0) // 第一名
                {
                    scoreToAdd = 10;
                }
                else if (i == 1) // 第二名
                {
                    scoreToAdd = 6;
                }
                else if (i == 2) // 第三名
                {
                    scoreToAdd = 4;
                }
                else // 其他名次
                {
                    scoreToAdd = 1;
                }

                // 查找对应的 CharacterObject 并赋予分数
                _annualTournament.participants.AddScoreToCharacter(participant.Character, scoreToAdd);

            }
        }
        private void EndCurrentMatch(bool isLeave)
        {

            this.LastMatch = this.CurrentMatch;
            this.CurrentRound.EndMatch();
            this._gameBehavior.OnMatchEnded();
            if (this.LastMatch.IsPlayerParticipating())
            {
                if (this.LastMatch.Winners.All((TournamentParticipant x) => x.Character != CharacterObject.PlayerCharacter))
                {
                    this.OnPlayerEliminated();
                }
                else
                {
                    this.OnPlayerWinMatch();
                }
            }
            if (this.NextRound != null)
            {
                for (; ; )
                {
                    if (!this.LastMatch.Winners.Any((TournamentParticipant x) => !x.IsAssigned))
                    {
                        break;
                    }
                    foreach (TournamentParticipant tournamentParticipant in this.LastMatch.Winners)
                    {
                        if (!tournamentParticipant.IsAssigned)
                        {
                            this.NextRound.AddParticipant(tournamentParticipant, false);
                            tournamentParticipant.IsAssigned = true;
                        }
                    }
                }
            }
            if (this.CurrentRound.CurrentMatch == null)
            {
                if (this.CurrentRoundIndex < 3)
                {
                    int i = this.CurrentRoundIndex;
                    this.CurrentRoundIndex = i + 1;
                    this.CalculateBet();
                    MissionGameModels missionGameModels = MissionGameModels.Current;
                    if (missionGameModels == null)
                    {
                        return;
                    }
                    AgentStatCalculateModel agentStatCalculateModel = missionGameModels.AgentStatCalculateModel;
                    if (agentStatCalculateModel == null)
                    {
                        return;
                    }
                    agentStatCalculateModel.SetAILevelMultiplier(1f + (float)this.CurrentRoundIndex / 3f);
                    return;
                }
                else
                {
                    MissionGameModels missionGameModels2 = MissionGameModels.Current;
                    if (missionGameModels2 != null)
                    {
                        AgentStatCalculateModel agentStatCalculateModel2 = missionGameModels2.AgentStatCalculateModel;
                        if (agentStatCalculateModel2 != null)
                        {
                            agentStatCalculateModel2.ResetAILevelMultiplier();
                        }
                    }
                    this.CalculateBet();
                    MBInformationManager.AddQuickInformation(new TextObject("{=*}比武结束", null), 0, null, "");
                    
                    ApplyScoreForAnnualTournamentParticipants();//统计分值
                    string heroscores = this._annualTournament.participants.GetHeroScoresAsString();
                    
                    InformationManager.DisplayMessage(new InformationMessage(heroscores));//显示分数

                    this.Winner = this.LastMatch.Winners.FirstOrDefault<TournamentParticipant>();
                    if (this.Winner.Character.IsHero)
                    {
                        //在这里加入导出排名的代码
                        if (this.Winner.Character == CharacterObject.PlayerCharacter)
                        {
                            this.OnPlayerWinTournament();
                        }
                        Campaign.Current.TournamentManager.GivePrizeToWinner(this._tournamentGame, this.Winner.Character.HeroObject, true);
                        Campaign.Current.TournamentManager.AddLeaderboardEntry(this.Winner.Character.HeroObject);
                    }
                    MBList<CharacterObject> mblist = new MBList<CharacterObject>(this._participants.Length);
                    foreach (TournamentParticipant tournamentParticipant2 in this._participants)
                    {
                        mblist.Add(tournamentParticipant2.Character);
                    }
                    CampaignEventDispatcher.Instance.OnTournamentFinished(this.Winner.Character, mblist, this.Settlement.Town, this._tournamentGame.Prize);
                    if (this.TournamentEnd != null && !isLeave)
                    {
                        this.TournamentEnd();
                    }
                }
            }
        }

        
        public void EndTournamentViaLeave()
        {
            while (this.CurrentMatch != null)
            {
                this.SkipMatch(true);
            }
        }

        
        private void OnPlayerEliminated()
        {
            this.IsPlayerEliminated = true;
            this.BetOdd = 0f;
            if (this.BettedDenars > 0)
            {
                GiveGoldAction.ApplyForCharacterToSettlement(null, Settlement.CurrentSettlement, this.BettedDenars, false);
            }
            this.OverallExpectedDenars = 0;
            CampaignEventDispatcher.Instance.OnPlayerEliminatedFromTournament(this.CurrentRoundIndex, this.Settlement.Town);
        }

        
        private void OnPlayerWinMatch()
        {
            Campaign.Current.TournamentManager.OnPlayerWinMatch(this._tournamentGame.GetType());
        }

        
        private void OnPlayerWinTournament()
        {
            if (Campaign.Current.GameMode != CampaignGameMode.Campaign)
            {
                return;
            }
            if (Hero.MainHero.MapFaction.IsKingdomFaction && Hero.MainHero.MapFaction.Leader != Hero.MainHero)
            {
                GainKingdomInfluenceAction.ApplyForDefault(Hero.MainHero, 1f);
            }
            if (this.OverallExpectedDenars > 0)
            {
                GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, this.OverallExpectedDenars, false);
            }
            Campaign.Current.TournamentManager.OnPlayerWinTournament(this._tournamentGame.GetType());
        }

        
        private void CreateTournamentTree()
        {
            int num = 16;
            int b = (int)MathF.Log((float)this._tournamentGame.MaxTeamSize, 2f);
            for (int i = 0; i < 4; i++)
            {
                int num2 = (int)MathF.Log((float)num, 2f);
                int num3 = MBRandom.RandomInt(1, MathF.Min(MathF.Min(3, num2), this._tournamentGame.MaxTeamNumberPerMatch));
                int num4 = MathF.Min(num2 - num3, b);
                int num5 = MathF.Ceiling(MathF.Log((float)(1 + MBRandom.RandomInt((int)MathF.Pow(2f, (float)num4))), 2f));
                int x = num2 - (num3 + num5);
                this.Rounds[i] = new TournamentRound(num, MathF.PowTwo32(x), MathF.PowTwo32(num3), num / 2, this._tournamentGame.Mode);
                num /= 2;
            }
        }

       
        private void FillParticipants(List<TournamentParticipant> participants)
        {
            foreach (TournamentParticipant participant in participants)
            {
                this.Rounds[this.CurrentRoundIndex].AddParticipant(participant, true);
            }
        }

       
        public override InquiryData OnEndMissionRequest(out bool canPlayerLeave)
        {
            InquiryData result = null;
            canPlayerLeave = false;
            return result;
        }

       
        public float BetOdd { get; private set; }

        
        public int MaximumBetInstance
        {
            get
            {
                return MathF.Min(150, this.PlayerDenars);
            }
        }

        
        public int BettedDenars { get; private set; }

        
        public int OverallExpectedDenars { get; private set; }

        
        public int PlayerDenars
        {
            get
            {
                return Hero.MainHero.Gold;
            }
        }

       
        public void PlaceABet(int bet)
        {
            this.BettedDenars += bet;
            this.OverallExpectedDenars += this.GetExpectedDenarsForBet(bet);
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, bet, true);
        }

        
        public int GetExpectedDenarsForBet(int bet)
        {
            return (int)(this.BetOdd * (float)bet);
        }

        
        public int GetMaximumBet()
        {
            int num = 150;
            if (Hero.MainHero.GetPerkValue(DefaultPerks.Roguery.DeepPockets))
            {
                num *= (int)DefaultPerks.Roguery.DeepPockets.PrimaryBonus;
            }
            return num;
        }

        
        private void CalculateBet()
        {
            if (this.IsPlayerParticipating)
            {
                if (this.CurrentRound.CurrentMatch == null)
                {
                    this.BetOdd = 0f;
                    return;
                }
                if (this.IsPlayerEliminated || !this.IsPlayerParticipating)
                {
                    this.OverallExpectedDenars = 0;
                    this.BetOdd = 0f;
                    return;
                }
                List<KeyValuePair<Hero, int>> leaderboard = Campaign.Current.TournamentManager.GetLeaderboard();
                int num = 0;
                int num2 = 0;
                for (int i = 0; i < leaderboard.Count; i++)
                {
                    if (leaderboard[i].Key == Hero.MainHero)
                    {
                        num = leaderboard[i].Value;
                    }
                    if (leaderboard[i].Value > num2)
                    {
                        num2 = leaderboard[i].Value;
                    }
                }
                float num3 = 30f + (float)Hero.MainHero.Level + (float)MathF.Max(0, num * 12 - num2 * 2);
                float num4 = 0f;
                float num5 = 0f;
                float num6 = 0f;
                foreach (TournamentMatch tournamentMatch in this.CurrentRound.Matches)
                {
                    foreach (TournamentTeam tournamentTeam in tournamentMatch.Teams)
                    {
                        float num7 = 0f;
                        foreach (TournamentParticipant tournamentParticipant in tournamentTeam.Participants)
                        {
                            if (tournamentParticipant.Character != CharacterObject.PlayerCharacter)
                            {
                                int num8 = 0;
                                if (tournamentParticipant.Character.IsHero)
                                {
                                    for (int k = 0; k < leaderboard.Count; k++)
                                    {
                                        if (leaderboard[k].Key == tournamentParticipant.Character.HeroObject)
                                        {
                                            num8 = leaderboard[k].Value;
                                        }
                                    }
                                }
                                num7 += (float)(tournamentParticipant.Character.Level + MathF.Max(0, num8 * 8 - num2 * 2));
                            }
                        }
                        if (tournamentTeam.Participants.Any((TournamentParticipant x) => x.Character == CharacterObject.PlayerCharacter))
                        {
                            num5 = num7;
                            foreach (TournamentTeam tournamentTeam2 in tournamentMatch.Teams)
                            {
                                if (tournamentTeam != tournamentTeam2)
                                {
                                    foreach (TournamentParticipant tournamentParticipant2 in tournamentTeam2.Participants)
                                    {
                                        int num9 = 0;
                                        if (tournamentParticipant2.Character.IsHero)
                                        {
                                            for (int l = 0; l < leaderboard.Count; l++)
                                            {
                                                if (leaderboard[l].Key == tournamentParticipant2.Character.HeroObject)
                                                {
                                                    num9 = leaderboard[l].Value;
                                                }
                                            }
                                        }
                                        num6 += (float)(tournamentParticipant2.Character.Level + MathF.Max(0, num9 * 8 - num2 * 2));
                                    }
                                }
                            }
                        }
                        num4 += num7;
                    }
                }
                float num10 = (num5 + num3) / (num6 + num5 + num3);
                float num11 = num3 / (num5 + num3 + 0.5f * (num4 - (num5 + num6)));
                float num12 = num10 * num11;
                float num13 = MathF.Clamp(MathF.Pow(1f / num12, 0.75f), 1.1f, 4f);
                this.BetOdd = (float)((int)(num13 * 10f)) / 10f;
            }
        }
        
        public event Action TournamentEnd;
        
        public const int RoundCount = 4;
        
        public const int ParticipantCount = 16;
        
        public const float EndMatchTimerDuration = 6f;

        public const float CheerTimerDuration = 1f;

        private TournamentGame _tournamentGame;

        private ITournamentGameBehavior _gameBehavior;

        private TournamentParticipant[] _participants;
                
        private const int MaximumBet = 150;
                
        public const float MaximumOdd = 4f;
    }
}


