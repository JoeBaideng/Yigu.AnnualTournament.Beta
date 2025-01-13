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
using SandBox.Tournaments.AgentControllers;




/// @namespace Yigu.AnnualTournament.Beta
/// 提供年度比武相关的模块和行为。
namespace Yigu.AnnualTournament.Beta
{
    [HarmonyPatch(typeof(TournamentBehavior))]
    [HarmonyPatch("EndCurrentMatch")]
    public static class EndCurrentMatchPatch
    {
        

        static bool Prefix(TournamentBehavior __instance)
        {
            MBInformationManager.AddQuickInformation(new TextObject($"当前回合{__instance.CurrentRoundIndex}", null), 0, null, "");
            if (__instance.CurrentRoundIndex>=3)
            {// 获取 _tournamentGame 字段信息
                var tournamentGameField = typeof(TournamentBehavior)
                    .GetField("_tournamentGame", BindingFlags.NonPublic | BindingFlags.Instance);

                // 从 __instance 中获取 _tournamentGame 字段的实际值
                if (tournamentGameField?.GetValue(__instance) is AbstractTournamentGame agame)
                {
                    // 使用 agame 访问 _equipmentType 和其他逻辑
                    EquipmentType equipmentType = agame._equipmentType;
                    AnnualTournamentManager.Instance.currentTournament.oneGameIsOverMap[equipmentType] = true;
                    var _participants = typeof(TournamentBehavior)
                        .GetField("_participants", BindingFlags.NonPublic | BindingFlags.Instance)
                        .GetValue(__instance) as TournamentParticipant[];
                    if (_participants == null || _participants.Length == 0 || AnnualTournamentManager.Instance.currentTournament == null)
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
                        AnnualTournamentManager.Instance.currentTournament.participants.AddScoreToCharacter(participant.Character, scoreToAdd);

                    }
                    // 设置文本和显示信息
                    GameTexts.SetVariable("BIWU_MENU_TEXT", AnnualTournamentManager.Instance.currentTournament.participants.GetHeroScoresAsString());
                    InformationManager.DisplayMessage(new InformationMessage(AnnualTournamentManager.Instance.currentTournament.participants.GetHeroScoresAsString()));
                    MBInformationManager.AddQuickInformation(new TextObject("{=*}一场赛事结束", null), 0, null, "");
                }

               
            }
            return true; // 继续执行原方法
        }
    }
    public class XmlConfigReader
    {
        private static XmlConfigReader _instance;
        private static readonly object _lock = new object();
        private readonly Dictionary<string, Dictionary<string, string>> _cache = new Dictionary<string, Dictionary<string, string>>();
        private readonly Dictionary<string, List<string>> _listCache = new Dictionary<string, List<string>>();
        private static string xmlFilePath="BiwuConfig.xml";
        private XmlConfigReader(string xmlFilePath)
        {
            try
            {
                var xDoc = XDocument.Load(xmlFilePath);
                LoadCache(xDoc);
            }
            catch (Exception ex)
            {
                throw new Exception($"无法加载 XML 配置文件：{ex.Message}");
            }
        }

        public static XmlConfigReader Instance
        {   get
            {if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new XmlConfigReader(xmlFilePath);
                    }
                }
            }
                return _instance;
            }
        }

        private void LoadCache(XDocument xDoc)
        {
            var config = xDoc.Element("Config");
            if (config == null) throw new Exception("根节点 Config 不存在");

            foreach (var section in config.Elements())
            {
                var sectionName = section.Name.LocalName;
                _cache[sectionName] = new Dictionary<string, string>();

                foreach (var element in section.Elements())
                {
                    _cache[sectionName][element.Name.LocalName] = element.Value;
                }

                // 针对列表结构缓存
                if (sectionName.EndsWith("Prize") || sectionName == "HostCities")
                {
                    _listCache[sectionName] = section.Elements().Select(e => e.Attribute("itemID")?.Value ?? e.Attribute("Name")?.Value).Where(v => !string.IsNullOrEmpty(v)).ToList();
                }
            }
        }

        public string GetValue(string section, string key, string defaultValue = null)
        {
            if (_cache.TryGetValue(section, out var sectionDict) && sectionDict.TryGetValue(key, out var value))
            {
                return value;
            }
            return defaultValue ?? throw new Exception($"未找到键：{section}.{key}");
        }

        public int GetIntValue(string section, string key, int defaultValue = 0)
        {
            var value = GetValue(section, key, defaultValue.ToString());
            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        public List<string> GetListValue(string section)
        {
            if (_listCache.TryGetValue(section, out var list))
            {
                return list;
            }
            throw new Exception($"未找到列表节：{section}");
        }
        public string GetRandomFromSection(string section)
        {
            if (_listCache.TryGetValue(section, out var list))
            {
                return list[MBRandom.RandomInt(list.Count)];
            }
            throw new Exception($"未找到列表节：{section}");
        }
    }

    public enum EquipmentType
    {
        FullCharacterEquipment,   // 全部使用角色装备
        CharacterArmorOnly,       // 射箭装备
        BoxingEquipment,           // 使用拳击装备
        Jousting                   //对枪装备
    }
    /// @class SubModule
    /// 继承自 MBSubModuleBase，定义了比武模块的加载和初始化逻辑。
    public class SubModule : MBSubModuleBase
    {
        /// <summary>
        /// 游戏处于加载界面时最先被调用的函数，你应该在这个函数中完成初始化的主要部分
        /// </summary>
        protected override void OnSubModuleLoad()
        {
            
            var harmony = new Harmony("EndCurrentMatchPatch");
            harmony.PatchAll();

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
            campaignGameStarter.AddBehavior(new BiWuCampaignBehavior());
        }

    }
    /// @class BiWuBehavior
    /// 继承自 CampaignBehaviorBase，定义了年度比武的具体行为逻辑。
    public class BiWuCampaignBehavior : CampaignBehaviorBase
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
        private bool IsGameValid(EquipmentType type)
        {
            if (AnnualTournamentManager.Instance.currentTournament == null)
            {
                return false;
            }

            // 检查是否正在进行比赛
            if (!AnnualTournamentManager.Instance.isGameBeingHeld)
            {
                return false;
            }

            // 检查特定游戏是否结束
            return !AnnualTournamentManager.Instance.currentTournament.oneGameIsOverMap[type];
        }
        /// @brief 添加比武相关的自定义菜单选项。
        /// @param campaignGameStarter 战役游戏启动器。
        private void AddGameMenus(CampaignGameStarter campaignGameStarter)
        {
            // 在城镇菜单中添加一个新的菜单选项，用于进入

            campaignGameStarter.AddGameMenuOption(
                "town_arena", // 目标菜单-竞技场
                "yigu_biwu", // 菜单选项的唯一标识
                "{=*}进入年度比武", // 菜单选项显示的文本
                (MenuCallbackArgs args) => { return AnnualTournamentManager.Instance.isGameBeingHeld; }, // 显示条件,有比赛时才显示
                game_menu_town_biwu_on_consequence); // 选中后的行动

            campaignGameStarter.AddGameMenu("biwu_to_join", "{=*}{BIWU_MENU_TEXT}", new OnInitDelegate(this.game_menu_biwu_join_on_init), GameOverlays.MenuOverlayType.SettlementWithBoth, GameMenu.MenuFlags.None, null);

            campaignGameStarter.AddGameMenuOption(
                "biwu_to_join", // 目标菜单-竞技场
                "yigu_biwu_archery", // 菜单选项的唯一标识
                "{=*}射艺", // 菜单选项显示的文本
                (MenuCallbackArgs args) => { // 检查当前比赛是否为空
                    return IsGameValid(EquipmentType.CharacterArmorOnly);
                }, // 显示条件,一直可以显示
                game_menu_town_biwu_archery_on_consequence); // 选中后的行动

            campaignGameStarter.AddGameMenuOption(
                "biwu_to_join", // 目标菜单-竞技场
                "yigu_biwu_jousting", // 菜单选项的唯一标识
                "{=*}骑战", // 菜单选项显示的文本
                (MenuCallbackArgs args) => { return IsGameValid(EquipmentType.Jousting); }, // 显示条件,一直可以显示
                game_menu_town_biwu_jousting_on_consequence); // 选中后的行动
            campaignGameStarter.AddGameMenuOption(
                "biwu_to_join", // 目标菜单-竞技场
                "yigu_biwu_boxing", // 菜单选项的唯一标识
                "{=*}肉搏", // 菜单选项显示的文本
                (MenuCallbackArgs args) => { return IsGameValid(EquipmentType.BoxingEquipment); }, // 显示条件,一直可以显示
                game_menu_town_biwu_boxing_on_consequence); // 选中后的行动
            campaignGameStarter.AddGameMenuOption(
                "biwu_to_join", // 目标菜单-竞技场
                "yigu_biwu_fight", // 菜单选项的唯一标识
                "{=*}刀剑无眼", // 菜单选项显示的文本
                (MenuCallbackArgs args) => { return IsGameValid(EquipmentType.FullCharacterEquipment); }, // 显示条件,一直可以显示
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
                "{=*}返回主城", // 菜单选项显示的文本
                (MenuCallbackArgs args) => { return AnnualTournamentManager.Instance.currentTournament.AllFinished(); }, // 显示条件,比武全部结束后才显示
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
            if (AnnualTournamentManager.Instance.isGameBeingHeld)
            {
                GameTexts.SetVariable("BIWU_MENU_TEXT", AnnualTournamentManager.Instance.currentTournament.GetMenuText());
            }
            InformationManager.DisplayMessage(new InformationMessage("{=*}年度比武开始!"));
            GameMenu.SwitchToMenu("biwu_to_join");

        }
        /// @brief 点击菜单选项后，打开比武这个任务。
        /// @param myGame 比武大会单场赛事
        /// @param args 菜单回调参数。
        private static void openMyGame(TournamentGame myGame, MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Mission;
            myGame.PrepareForTournamentGame(true);
            //MBInformationManager.AddQuickInformation(myGame.GetMenuText(), 0, null, "");//显示比赛介绍
            CampaignEventDispatcher.Instance.OnPlayerJoinedTournament(Settlement.CurrentSettlement.Town, true);
          
        }
        /// @brief 射箭比赛选项的后续逻辑。
        /// @param args 菜单回调参数。
        private static void game_menu_town_biwu_archery_on_consequence(MenuCallbackArgs args)
        {
            BiwuArcheryGame myGame = AnnualTournamentManager.Instance.currentTournament.archeryGame;
            if (myGame != null)
            {
                openMyGame(myGame, args);
            }

        }
        /// @brief 骑马对枪选项的后续逻辑。
        /// @param args 菜单回调参数。
        private static void game_menu_town_biwu_jousting_on_consequence(MenuCallbackArgs args)
        {

            BiWuFightGame myGame = AnnualTournamentManager.Instance.currentTournament.joustingGame;
            if (myGame != null)
            {
                openMyGame(myGame, args);
            }
        }
        /// @brief 拳击选项的后续逻辑。
        /// @param args 菜单回调参数。
        private static void game_menu_town_biwu_boxing_on_consequence(MenuCallbackArgs args)
        {

            BiWuFightGame myGame = AnnualTournamentManager.Instance.currentTournament.boxingGame;
            if (myGame != null)
            {
                openMyGame(myGame, args);
            }
        }
        /// @brief 自由比武选项的后续逻辑。
        /// @param args 菜单回调参数。
        private static void game_menu_town_biwu_fight_on_consequence(MenuCallbackArgs args)
        {

            BiWuFightGame myGame = AnnualTournamentManager.Instance.currentTournament.fightGame;
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
                    if (AnnualTournamentManager.Instance.currentTournament.AllFinished())
                    {
                      AnnualTournamentManager.Instance.EndTournament();
                      

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
        private int endDay ;
        private int startDay ;
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
            startDay = XmlConfigReader.Instance.GetIntValue("TimeControl", "StartDay", 24);
            endDay = XmlConfigReader.Instance.GetIntValue("TimeControl", "EndDay", 60);
            town = SelectTown();//测试，在阿塞莱的Quyaz
        }
        private Town SelectTown()
        {
            List<string> hostCities = XmlConfigReader.Instance.GetListValue("HostCities");
            if (hostCities.Count > 0)
            {
                string townId = hostCities[MBRandom.RandomInt(hostCities.Count)];
                return Settlement.Find(townId).Town;
            }
            return Settlement.Find("town_A1").Town;
           
        }
        public void Initialize()
        {
            // 注册事件监听，触发比赛逻辑
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        private void OnDailyTick()
        {
            int currentDay = CampaignTime.Now.GetDayOfYear;
            InformationManager.DisplayMessage(new InformationMessage($"当前日期：{currentDay}"));
            // 比赛触发逻辑，根据游戏日期检查是否需要触发新比赛
            if (!isGameBeingHeld && ShouldStartTournament())
            {
                this.town=SelectTown();
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
            
            InformationManager.DisplayMessage(new InformationMessage($"年度比武正在 {venue.Name} 举办，请在{endDay-startDay}天内前往参赛"));

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
                .Select(p => $"{p.character.Name} ----- {p.Score}") // 格式化为 "英雄名 - 分数"
                .ToList();

            return "目前得分：\n" + string.Join("\n", scoreList); // 英雄之间用换行分隔
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


        public BiwuArcheryGame archeryGame;
        public BiWuFightGame joustingGame;
        public BiWuFightGame boxingGame;
        public BiWuFightGame fightGame;
        public MBList<ItemObject> prizeList;
        
        public bool IsActive;
        //public bool AllFinished;
        public Dictionary<EquipmentType, bool> oneGameIsOverMap;//储存比赛是否结束

        public AnnualTournament(Town vneue_town)
        {
            this.vneue_town = vneue_town;
            this.prizeList = GetPrizeList();

            participants = new AnnualTournamentParticipants(GetParticipantCharacters());
            archeryGame = new BiwuArcheryGame(vneue_town, participants.characters);
            //Campaign.Current.TournamentManager.AddTournament(archeryGame);

            joustingGame = new BiWuFightGame(vneue_town, EquipmentType.Jousting, participants.characters, this);
            //Campaign.Current.TournamentManager.AddTournament(joustingGame);

            boxingGame = new BiWuFightGame(vneue_town, EquipmentType.BoxingEquipment, participants.characters, this);
            //Campaign.Current.TournamentManager.AddTournament(boxingGame);

            fightGame = new BiWuFightGame(vneue_town, EquipmentType.FullCharacterEquipment, participants.characters, this);
            // Campaign.Current.TournamentManager.AddTournament(fightGame);
            IsActive = true;
            
            oneGameIsOverMap = new Dictionary<EquipmentType, bool>
        {
            { EquipmentType.FullCharacterEquipment, false },
            { EquipmentType.CharacterArmorOnly, false },
            { EquipmentType.BoxingEquipment, false },
            { EquipmentType.Jousting, false }
        };
        }
        public bool AllFinished()
        {
            // 检查字典中所有值是否都是 true
           foreach (var value in oneGameIsOverMap.Values)
            {
                if (!value)
                {
                    return false;
                }
            }
            return true;
        }

        public TextObject GetMenuText()
        {
            TextObject textObject = new TextObject("{=*}设擂于市，邀四方豪杰，擂鼓三声，则英雄登台斗武，较高下于顷刻之间，胜者名知海内，败者亦可留名江湖。此大会不独较胜负，亦为英雄互识、结交知己之机也。\n" );
             return textObject;
        }
        public MBList<ItemObject> GetPrizeList()
        {
            MBList<ItemObject> list = new MBList<ItemObject>();

            for (int i = 0; i < 3; i++)
            {
                // 获取当前奖品列表
                
                // 随机选取一个奖品 ID
                string prizeId = XmlConfigReader.Instance.GetRandomFromSection($"Winner{i + 1}Prize");

                // 根据 ID 获取奖品对象
                ItemObject prize = Game.Current.ObjectManager.GetObject<ItemObject>(prizeId);

                // 如果奖品对象有效，添加到结果列表
                if (prize != null)
                {
                    list.Add(prize);
                }
                else
                {
                    InformationManager.DisplayMessage(new InformationMessage($"奖品 ID 无效：{prizeId}"));
                }
            }

            return list;
        }

        public void GivePrizeToOneWinner(CharacterObject winner, ItemObject prize)
        {
            if (winner.HeroObject.PartyBelongedTo == MobileParty.MainParty)
            {
                winner.HeroObject.PartyBelongedTo.ItemRoster.AddToCounts(prize, 1);
                InformationManager.DisplayMessage(new InformationMessage($"你获得了比武的奖品{prize.Name}"));
                return;
            }
            if (winner.HeroObject.Clan != null)
            {
                GiveGoldAction.ApplyBetweenCharacters(null, winner.HeroObject.Clan.Leader, vneue_town.MarketData.GetPrice(prize, null, false, null), false);
                InformationManager.DisplayMessage(new InformationMessage($"{winner.Name}获得了比武的奖品{prize.Name}"));
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
            InformationManager.DisplayMessage(new InformationMessage("{=*}年度比武开始!"));
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
           
            //while (mblist.Count < 16)
            //{
            //    CultureObject cultureObject = (this.vneue_town != null) ? this.vneue_town.Culture : Game.Current.ObjectManager.GetObject<CultureObject>("yizhou");
            //    CharacterObject item = (MBRandom.RandomFloat > 0.5f) ? cultureObject.BasicTroop : cultureObject.EliteBasicTroop;
            //    mblist.Add(item);
            //}
           
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
    public abstract class AbstractTournamentGame : TournamentGame
    {
        // 新增共享成员
        //public bool Finished { get; protected set; }
        
        public EquipmentType _equipmentType { get; protected set; }

        // 抽象方法：每个具体的 Tournament 类需要实现
       

        protected AbstractTournamentGame(Town town, EquipmentType equipmentType, ItemObject prize = null) : base(town, prize)
        {
            this._equipmentType = equipmentType;
        }
        //public void FinishTheGame()
        //{
        //    this.Finished = true;
        //}
        
    }
    /// @brief 射箭锦标赛游戏类
    /// @details 此类继承自 TournamentGame 类，定义了射箭锦标赛的规则和行为。
    public class BiwuArcheryGame : AbstractTournamentGame
    {
        private AnnualTournament _annualTournament;
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
        public BiwuArcheryGame(Town town, MBList<CharacterObject> game_participants) : base(town,EquipmentType.CharacterArmorOnly, null)
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
            TextObject textObject = new TextObject("{=*}射者，礼之大节，技之精妙也。比射之时，强弓劲矢，百步穿杨，箭无虚发者，盖为上上之士。观者屏息，竞者奋勇，或中红心，或失正鹄，胜负立判矣。");
            return textObject;
        }

        /// @brief 打开比赛场景
        /// @param settlement 比赛场景所在的地点。
        /// @param isPlayerParticipating 玩家是否参与比赛。
        /// @details 根据地点情况加载对应的射箭比赛场景。
        public override void OpenMission(Settlement settlement, bool isPlayerParticipating)
        {
            int upgradeLevel = settlement.IsTown ? settlement.Town.GetWallLevel() : 1;
           
            string scene = LocationComplex.Current.GetScene("arena", upgradeLevel);
            CultureObject culture = settlement.Culture;

            MissionState.OpenNew("TournamentArchery", SandBoxMissions.CreateSandBoxMissionInitializerRecord(scene, "", false, DecalAtlasGroup.Town), delegate (Mission missionController)
            {
                TournamentArcheryMissionController tournamentArcheryMissionController = new TournamentArcheryMissionController(culture);
                return new MissionBehavior[]
                {
                    new CampaignMissionComponent(),
                    new EquipmentControllerLeaveLogic(),
                    tournamentArcheryMissionController,
                    new TournamentBehavior(this, settlement, tournamentArcheryMissionController, isPlayerParticipating),//需要替换
                    new AgentVictoryLogic(),
                    new MissionAgentPanicHandler(),
                    new AgentHumanAILogic(),
                    new ArenaAgentStateDeciderLogic(),
                    new BasicLeaveMissionLogic(true),
                    new MissionHardBorderPlacer(),
                    new MissionBoundaryPlacer(),
                    new MissionOptionsComponent()
                };
            }, true, true);
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
            
            string objectName = XmlConfigReader.Instance.GetRandomFromSection("SimplePrize");
            return Game.Current.ObjectManager.GetObject<ItemObject>(objectName);
        }
    }
    /**
 * @class BiWuGame
 * @brief 表示比武大会的游戏类，继承自 TournamentGame。
 * 
 * 该类实现了比武大会的具体逻辑，包括参赛条件、比赛机制和奖励设定。
 */
    public class BiWuFightGame : AbstractTournamentGame
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
        //public enum EquipmentType
        //{
        //    FullCharacterEquipment,   // 全部使用角色装备
        //    CharacterArmorOnly,       // 只使用角色盔甲
        //    BoxingEquipment,           // 使用拳击装备
        //    Jousting                   //对枪装备
        //}

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
        public BiWuFightGame(Town town, EquipmentType equipmentType, MBList<CharacterObject> game_participants, AnnualTournament annualTournament) : base(town, equipmentType,null)
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
            TextObject textObject;

            switch (this._equipmentType)
            {
                case EquipmentType.FullCharacterEquipment:
                    textObject = new TextObject("{=*}披甲搏战，乃沙场真勇者之术也。两士披铁甲，执利器，试于擂场，刀枪互击，甲坚而技巧者得先。击碎敌甲，夺其武器，立为胜者。此乃胆略、技艺、气力之全争也。");
                    break;

                case EquipmentType.BoxingEquipment:
                    textObject = new TextObject("{=*}拳者，武艺之本，近战之术也。两士对峙，拳风烈烈，劲道刚柔并济，出拳如电，攻守有法。试于擂台之上，或胜一招半式，或败于毫厘之间，斯乃勇力之决也。");
                    break;

                case EquipmentType.Jousting:
                    textObject = new TextObject("{=*}骑战者，古之军阵重术，乃武人所必修也。比技之时，健骑如龙腾，长枪似霹雳，勇者策马直冲，枪出如电，击中敌之标靶，壮观之至。若能于疾驰中稳枪刺准者，实乃百战之将也。");
                    break;

                default:
                    textObject = new TextObject("{=*}未知的比武方式。");
                    break;
            }

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
                BiWuFightMissionController biwuMissionController = new BiWuFightMissionController(settlement.Culture, this._equipmentType);
                return new MissionBehavior[]
                {
                    new CampaignMissionComponent(),
                    new EquipmentControllerLeaveLogic(),
                    biwuMissionController,
                    new TournamentBehavior(this, settlement, biwuMissionController, isPlayerParticipating),
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
            string objectName = XmlConfigReader.Instance.GetRandomFromSection("SimplePrize");
            return Game.Current.ObjectManager.GetObject<ItemObject>(objectName);
        }





        public const int ParticipantTroopMinimumTierLimit = 3;


        private EquipmentType _equipmentType;
    }

    public class BiwuArcheryMissionController : MissionLogic, ITournamentGameBehavior
    {
        // Token: 0x1700000E RID: 14
        // (get) Token: 0x06000128 RID: 296 RVA: 0x00007FFC File Offset: 0x000061FC
        public IEnumerable<ArcheryTournamentAgentController> AgentControllers
        {
            get
            {
                return this._agentControllers;
            }
        }

        // Token: 0x06000129 RID: 297 RVA: 0x00008004 File Offset: 0x00006204
        public BiwuArcheryMissionController(CultureObject culture)
        {
            string section = "ArcheryEquipment";
            this._culture = culture;
            this.ShootingPositions = new List<GameEntity>();
            this._agentControllers = new List<ArcheryTournamentAgentController>();
            this._archeryEquipment = new Equipment();
            this._archeryEquipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.WeaponItemBeginSlot, new EquipmentElement(Game.Current.ObjectManager.GetObject<ItemObject>(XmlConfigReader.Instance.GetValue(section, EquipmentIndex.WeaponItemBeginSlot.ToString())), null, null, false));
            this._archeryEquipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Weapon1, new EquipmentElement(Game.Current.ObjectManager.GetObject<ItemObject>(XmlConfigReader.Instance.GetValue(section, EquipmentIndex.Weapon1.ToString())), null, null, false));
            this._archeryEquipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Body, new EquipmentElement(Game.Current.ObjectManager.GetObject<ItemObject>(XmlConfigReader.Instance.GetValue(section, EquipmentIndex.Body.ToString())), null, null, false));
            this._archeryEquipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Gloves, new EquipmentElement(Game.Current.ObjectManager.GetObject<ItemObject>(XmlConfigReader.Instance.GetValue(section, EquipmentIndex.Gloves.ToString())), null, null, false));
            this._archeryEquipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Leg, new EquipmentElement(Game.Current.ObjectManager.GetObject<ItemObject>(XmlConfigReader.Instance.GetValue(section, EquipmentIndex.Leg.ToString())), null, null, false));
        }

        // Token: 0x0600012A RID: 298 RVA: 0x00008108 File Offset: 0x00006308
        public override void AfterStart()
        {
            TournamentBehavior.DeleteTournamentSetsExcept(base.Mission.Scene.FindEntityWithTag("tournament_archery"));
            this._spawnPoints = base.Mission.Scene.FindEntitiesWithTag("sp_arena").ToList<GameEntity>();
            base.Mission.SetMissionMode(MissionMode.Battle, true);
            this._targets = (from x in base.Mission.ActiveMissionObjects.FindAllWithType<DestructableComponent>()
                             where x.GameEntity.HasTag("archery_target")
                             select x).ToList<DestructableComponent>();
            foreach (DestructableComponent destructableComponent in this._targets)
            {
                destructableComponent.OnDestroyed += new DestructableComponent.OnHitTakenAndDestroyedDelegate(this.OnTargetDestroyed);
            }
        }

        // Token: 0x0600012B RID: 299 RVA: 0x000081EC File Offset: 0x000063EC
        public void StartMatch(TournamentMatch match, bool isLastRound)
        {
            this._match = match;
            this.ResetTargets();
            int count = this._spawnPoints.Count;
            int num = 0;
            int num2 = 0;
            foreach (TournamentTeam tournamentTeam in this._match.Teams)
            {
                Team team = base.Mission.Teams.Add(BattleSideEnum.None, MissionAgentHandler.GetRandomTournamentTeamColor(num2), uint.MaxValue, null, true, false, true);
                foreach (TournamentParticipant tournamentParticipant in tournamentTeam.Participants)
                {
                    tournamentParticipant.MatchEquipment = this._archeryEquipment.Clone(false);
                    MatrixFrame globalFrame = this._spawnPoints[num % count].GetGlobalFrame();
                    globalFrame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
                    this.SetItemsAndSpawnCharacter(tournamentParticipant, team, globalFrame);
                    num++;
                }
                num2++;
            }
        }

        // Token: 0x0600012C RID: 300 RVA: 0x000082FC File Offset: 0x000064FC
        public void SkipMatch(TournamentMatch match)
        {
            this._match = match;
            this.Simulate();
        }

        // Token: 0x0600012D RID: 301 RVA: 0x0000830C File Offset: 0x0000650C
        private void Simulate()
        {
            this._isSimulated = false;
            List<TournamentParticipant> list = this._match.Participants.ToList<TournamentParticipant>();
            int i = this._targets.Count;
            while (i > 0)
            {
                foreach (TournamentParticipant tournamentParticipant in list)
                {
                    if (i == 0)
                    {
                        break;
                    }
                    if (MBRandom.RandomFloat < this.GetDeadliness(tournamentParticipant))
                    {
                        tournamentParticipant.AddScore(1);
                        i--;
                    }
                }
            }
            this._isSimulated = true;
        }

        // Token: 0x0600012E RID: 302 RVA: 0x000083A4 File Offset: 0x000065A4
        public bool IsMatchEnded()
        {
            if (this._isSimulated || this._match == null)
            {
                return true;
            }
            if (this._endTimer != null && this._endTimer.ElapsedTime > 6f)
            {
                this._endTimer = null;
                return true;
            }
            if (this._endTimer == null && (!this.IsThereAnyTargetLeft() || !this.IsThereAnyArrowLeft()))
            {
                this._endTimer = new BasicMissionTimer();
            }
            return false;
        }

        // Token: 0x0600012F RID: 303 RVA: 0x0000840C File Offset: 0x0000660C
        public void OnMatchEnded()
        {
            SandBoxHelpers.MissionHelper.FadeOutAgents(base.Mission.Agents, true, false);
            base.Mission.ClearCorpses(false);
            base.Mission.Teams.Clear();
            base.Mission.RemoveSpawnedItemsAndMissiles();
            this._match = null;
            this._endTimer = null;
            this._isSimulated = false;
        }

        // Token: 0x06000130 RID: 304 RVA: 0x00008468 File Offset: 0x00006668
        private void ResetTargets()
        {
            foreach (DestructableComponent destructableComponent in this._targets)
            {
                destructableComponent.Reset();
            }
        }

        // Token: 0x06000131 RID: 305 RVA: 0x000084B8 File Offset: 0x000066B8
        private void SetItemsAndSpawnCharacter(TournamentParticipant participant, Team team, MatrixFrame frame)
        {
            AgentBuildData agentBuildData = new AgentBuildData(new SimpleAgentOrigin(participant.Character, -1, null, participant.Descriptor)).Team(team).Equipment(participant.MatchEquipment).InitialPosition(frame.origin);
            Vec2 vec = frame.rotation.f.AsVec2;
            vec = vec.Normalized();
            AgentBuildData agentBuildData2 = agentBuildData.InitialDirection(vec).Controller(participant.Character.IsPlayerCharacter ? Agent.ControllerType.Player : Agent.ControllerType.AI);
            Agent agent = base.Mission.SpawnAgent(agentBuildData2, false);
            agent.Health = agent.HealthLimit;
            ArcheryTournamentAgentController archeryTournamentAgentController = agent.AddController(typeof(ArcheryTournamentAgentController)) as ArcheryTournamentAgentController;
            archeryTournamentAgentController.SetTargets(this._targets);
            this._agentControllers.Add(archeryTournamentAgentController);
            if (participant.Character.IsPlayerCharacter)
            {
                agent.WieldInitialWeapons(Agent.WeaponWieldActionType.InstantAfterPickUp, Equipment.InitialWeaponEquipPreference.Any);
                base.Mission.PlayerTeam = team;
                return;
            }
            agent.SetWatchState(Agent.WatchState.Alarmed);
        }

        // Token: 0x06000132 RID: 306 RVA: 0x000085A4 File Offset: 0x000067A4
        public void OnTargetDestroyed(DestructableComponent destroyedComponent, Agent destroyerAgent, in MissionWeapon attackerWeapon, ScriptComponentBehavior attackerScriptComponentBehavior, int inflictedDamage)
        {
            foreach (ArcheryTournamentAgentController archeryTournamentAgentController in this.AgentControllers)
            {
                archeryTournamentAgentController.OnTargetHit(destroyerAgent, destroyedComponent);
                this._match.GetParticipant(destroyerAgent.Origin.UniqueSeed).AddScore(1);
            }
        }

        // Token: 0x06000133 RID: 307 RVA: 0x00008610 File Offset: 0x00006810
        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (!this.IsMatchEnded())
            {
                foreach (Agent agent in base.Mission.Agents)
                {
                    ArcheryTournamentAgentController controller = agent.GetController<ArcheryTournamentAgentController>();
                    if (controller != null)
                    {
                        controller.OnTick();
                    }
                }
            }
        }

        // Token: 0x06000134 RID: 308 RVA: 0x00008680 File Offset: 0x00006880
        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon attackerWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            base.Mission.EndMission();
        }

        // Token: 0x06000135 RID: 309 RVA: 0x0000868D File Offset: 0x0000688D
        private bool IsThereAnyTargetLeft()
        {
            return this._targets.Any((DestructableComponent e) => !e.IsDestroyed);
        }

        // Token: 0x06000136 RID: 310 RVA: 0x000086B9 File Offset: 0x000068B9
        private bool IsThereAnyArrowLeft()
        {
            return base.Mission.Agents.Any((Agent agent) => agent.Equipment.GetAmmoAmount(EquipmentIndex.WeaponItemBeginSlot) > 0);
        }

        // Token: 0x06000137 RID: 311 RVA: 0x000086EA File Offset: 0x000068EA
        private float GetDeadliness(TournamentParticipant participant)
        {
            return 0.01f + (float)participant.Character.GetSkillValue(DefaultSkills.Bow) / 300f * 0.19f;
        }

        // Token: 0x04000055 RID: 85
        private readonly List<ArcheryTournamentAgentController> _agentControllers;

        // Token: 0x04000056 RID: 86
        private TournamentMatch _match;

        // Token: 0x04000057 RID: 87
        private BasicMissionTimer _endTimer;

        // Token: 0x04000058 RID: 88
        private List<GameEntity> _spawnPoints;

        // Token: 0x04000059 RID: 89
        private bool _isSimulated;

        // Token: 0x0400005A RID: 90
        private CultureObject _culture;

        // Token: 0x0400005B RID: 91
        private List<DestructableComponent> _targets;

        // Token: 0x0400005C RID: 92
        public List<GameEntity> ShootingPositions;

        // Token: 0x0400005D RID: 93
        private readonly Equipment _archeryEquipment;
    }
    /**
 * @brief 比武场任务控制器类。
 * 
 * 负责控制比武比赛的相关逻辑，包括初始化任务行为、比赛准备和比赛开始等功能。
 */
    public class BiWuFightMissionController : MissionLogic, ITournamentGameBehavior
    {
        /**
      * @brief 构造函数，初始化比武任务控制器。
      * @param culture 比赛使用的文化对象。
      * @param equipmentType 比赛使用的装备类型。
      */
        public BiWuFightMissionController(CultureObject culture, EquipmentType equipmentType)
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
            if (this._equipmentType == EquipmentType.BoxingEquipment)
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
                case EquipmentType.BoxingEquipment:
                    {

                        break;
                    }
                case EquipmentType.FullCharacterEquipment:
                    {
                        Equipment participantArmor = Campaign.Current.Models.TournamentModel.GetParticipantArmor(participant.Character);
                        for (int i = 0; i < 12; i++)
                        {
                            EquipmentElement equipmentFromSlot = participantArmor.GetEquipmentFromSlot((EquipmentIndex)i);
                            if (equipmentFromSlot.Item != null)
                            {
                                participant.MatchEquipment.AddEquipmentToSlotWithoutAgent((EquipmentIndex)i, equipmentFromSlot);
                            }
                        }
                        break;
                    }
                case EquipmentType.Jousting:
                    {
                        Equipment participantArmor = Campaign.Current.Models.TournamentModel.GetParticipantArmor(participant.Character);
                        participant.MatchEquipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Horse, new EquipmentElement(Game.Current.ObjectManager.GetObject<ItemObject>(XmlConfigReader.Instance.GetValue("JoustingEquipment", EquipmentIndex.Horse.ToString())), null, null, false));
                        participant.MatchEquipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.HorseHarness, new EquipmentElement(Game.Current.ObjectManager.GetObject<ItemObject>(XmlConfigReader.Instance.GetValue("JoustingEquipment", EquipmentIndex.HorseHarness.ToString())), null, null, false));//horse_harness_e
                        participant.MatchEquipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.WeaponItemBeginSlot, new EquipmentElement(Game.Current.ObjectManager.GetObject<ItemObject>(XmlConfigReader.Instance.GetValue("JoustingEquipment", EquipmentIndex.WeaponItemBeginSlot.ToString())), null, null, false));

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
        private EquipmentType _equipmentType;

    }



    
}


