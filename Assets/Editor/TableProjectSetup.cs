using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NumberTable;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TableProjectSetup
{
    [MenuItem("Tools/Number Table/Create Playable Scene")]
    public static void CreateScene()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before creating scene.");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));
        camera.tag="MainCamera";camera.transform.position=new Vector3(0,0,-10);
        camera.GetComponent<Camera>().orthographic=true;camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;
        camera.GetComponent<Camera>().backgroundColor=new Color(.06f,.11f,.12f);
        new GameObject("Number Table",typeof(TableView));
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/NumberTable.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/NumberTable.unity",true)};
        PlayerSettings.productName="Number Table Prototype";
        PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        PlayerSettings.resizableWindow=true;
        AssetDatabase.SaveAssets();
        Debug.Log("NUMBER TABLE: playable scene created.");
    }

    [MenuItem("Tools/Number Table/Run Core Checks")]
    public static string RunCoreChecks()
    {
        int checks=0;
        Action<bool,string> check=(pass,message)=>{checks++;if(!pass)throw new Exception("Check failed: "+message);};
        var rules=new TableRules();var json=Resources.Load<TextAsset>("TableBalance");if(json!=null)JsonUtility.FromJsonOverwrite(json.text,rules);
        var g=new TableRun(rules);g.Reset(33);
        Func<int[],List<PlayingCard>> cards=ranks=>ranks.Select((r,i)=>new PlayingCard(i,r,0)).ToList();
        check(TableRun.Evaluate(cards(new[]{1,7}),g.unlocked).total==18,"A+7 auto =18");
        check(TableRun.Evaluate(cards(new[]{1,7,8}),g.unlocked).total==16,"A+7+8 auto =16");
        check(TableRun.Evaluate(cards(new[]{1,1}),g.unlocked).total==12,"AA default12");
        check(TableRun.Evaluate(cards(new[]{1,1,1}),g.unlocked).total==13,"AAA default13");
        g.unlocked[33]=true;
        check(TableRun.Evaluate(cards(new[]{1,1,1}),g.unlocked).total==33,"AAA unlocked33");
        check(TableRun.Evaluate(cards(new[]{1,1}),g.unlocked).total==12,"33 unlock does not unlock22");
        g.unlocked[27]=true;
        check(TableRun.Evaluate(cards(new[]{10,7,10}),g.unlocked).total==27,"Exact27 allowed");
        check(TableRun.Evaluate(cards(new[]{10,7,5}),g.unlocked).bust,"Locked22 bust even with27 unlocked");
        check(TableRun.Evaluate(cards(new[]{10,10,10,5}),g.unlocked).bust,"35 bust");
        var low=cards(new[]{1,2});low[0].aceChoice=1;
        check(TableRun.Evaluate(low,g.unlocked).total==3,"Manual low Ace enables3");
        g.phase=RunPhase.Playing;g.hand.AddRange(cards(new[]{1,2}));
        check(g.SetAce(0,1)&&g.Current.total==3,"Ace selection API chooses low");
        check(g.SetAce(0,11)&&g.Current.total==13,"Ace selection API chooses high");
        check(g.SetAce(0,0)&&g.hand[0].aceChoice==0,"Ace selection restores auto");
        g.hand.Clear();g.hand.AddRange(cards(new[]{1,10,5}));
        check(!g.SetAce(0,11)&&g.Current.total==16,"Unsafe manual Ace choice rolls back");
        var high=cards(new[]{1,1});high[0].aceChoice=11;high[1].aceChoice=11;
        check(TableRun.Evaluate(high,g.unlocked).bust,"Two fixed11 cannot silently downgrade");
        g.unlocked[22]=true;check(TableRun.Evaluate(high,g.unlocked).total==22,"Fixed11 respects unlock");
        g.Reset(33);g.coins=1000;int rarity=g.Stars(18);int price=g.UpgradeCost(18),money=g.coins;
        check(g.Upgrade(18)&&g.coins==money-price&&g.levels[18]==2,"Upgrade transaction");
        g.Add(6);check(g.Stars(18)==rarity,"Rarity immutable after upgrade and deck edit");
        for(int i=0;i<8;i++)g.Upgrade(18);check(g.levels[18]==5&&!g.CanUpgrade(18),"Level cap");
        check(g.Score(18)>g.Score(21),"Invested18 beats uninvested21");
        g.Reset(33);g.LeaveWorkshop();g.hand.Clear();g.hand.AddRange(cards(new[]{10,8}));
        int score=g.Score(18,0);g.Stand();check(g.lastPoints==score&&g.streak==1,"First score uses prior streak0");
        int sum=g.stageScore;g.Stand();check(g.stageScore==sum,"Repeated stand cannot score twice");
        g.Advance();check(g.phase==RunPhase.Playing&&g.handsPlayed==2,"Continue deals next hand");
        g.hand.Clear();g.hand.AddRange(cards(new[]{10,8}));g.Stand();check(g.lastPoints==g.Score(18,1)&&g.streak==2,"Second score uses prior streak1");
        g.Advance();check(g.phase==RunPhase.Workshop,"Workshop every two hands");
        g.LeaveWorkshop();g.hand.Clear();g.hand.AddRange(cards(new[]{10,10}));g.shoe.Clear();g.shoe.Add(new PlayingCard(500,5,0));g.Hit();
        check(g.lastBust&&g.streak==0&&g.phase==RunPhase.Result,"Bust resolves and resets streak");
        g.stageScore=0;g.phase=RunPhase.Playing;g.hand.Clear();g.hand.AddRange(cards(new[]{10,10}));g.shoe.Clear();g.shoe.Add(new PlayingCard(501,5,0));g.Hit();
        check(g.stageScore==0&&g.lastPoints==0,"Bust floor0");
        g.Reset(33);g.LeaveWorkshop();check(g.hand.Count==2&&g.shoe.Count==24,"Deal removes exactly2");
        g.Shuffle();check(!g.shoe.Any(c=>g.hand.Any(h=>h.id==c.id)),"Reshuffle excludes hand cards");
        check(g.deck.Select(c=>c.id).Distinct().Count()==g.deck.Count,"Unique deck IDs");
        check(!g.Add(6)&&!g.Upgrade(18),"No shopping during hand");
        g.phase=RunPhase.Workshop;g.coins=10000;
        for(int i=0;i<10;i++)g.Add(1);check(g.RankCount(1)==rules.aceLimit,"Ace cap");
        for(int r=2;r<=13;r++)for(int i=0;i<8;i++)g.Add(r);
        check(g.deck.Count==rules.maxDeck&&g.RankCount(2)<=rules.rankLimit,"Deck and rank cap");
        for(int r=1;r<=13;r++)for(int i=0;i<8;i++)g.Remove(r);
        check(g.deck.Count==rules.minDeck,"Minimum deck size");
        g.coins=0;check(!g.Add(1)&&!g.Upgrade(18)&&!g.Unlock(33)&&!g.Remove(1),"No overspending");
        g.phase=RunPhase.Result;g.handsPlayed=rules.handsPerStage;g.stageScore=g.Target;g.Advance();
        check(g.phase==RunPhase.StageClear,"Final hand can still clear stage");
        g.Advance();check(g.stage==1&&g.stageScore==0&&g.handsPlayed==0&&g.phase==RunPhase.Workshop,"Stage transition resets stage only");
        g.phase=RunPhase.Result;g.handsPlayed=rules.handsPerStage;g.stageScore=0;g.Advance();check(g.phase==RunPhase.Defeat,"Failure after last hand");
        g.Reset(33);check(g.levels[18]==1&&!g.unlocked[33]&&g.deck.Count==26&&g.streak==0,"Fresh run resets progression");
        g.phase=RunPhase.Result;g.stage=rules.stageTargets.Length-1;g.stageScore=g.Target;g.Advance();check(g.phase==RunPhase.Victory,"Last stage victory");
        g.Reset(33);g.phase=RunPhase.Playing;g.stageScore=500;g.streak=2;
        g.hand.AddRange(cards(new[]{4,1}));g.hand[1].aceChoice=11;
        g.shoe.Add(new PlayingCard(100,9,0));
        g.HitForecast(out float rescueRisk,out float rescueEv);
        check(rescueRisk==0&&rescueEv==g.Score(14),"Forecast accounts for manual Ace rescue");
        money=g.coins;g.Hit();
        check(g.Current.total==24&&g.NeedsAceChoice&&g.phase==RunPhase.Playing,"4+A11+9 waits for Ace choice");
        check(g.stageScore==500&&g.streak==2&&g.coins==money&&g.totalBusts==0,"Pending Ace does not charge or reward");
        int hitCount=g.totalHits;g.Hit();g.Stand();
        check(g.hand.Count==3&&g.totalHits==hitCount&&g.phase==RunPhase.Playing&&g.stageScore==500,"Pending Ace blocks HIT and STAND including shortcuts");
        check(g.SetAce(1,1)&&g.Current.total==14&&!g.NeedsAceChoice,"Manual Ace rescue returns24 to14");
        score=g.Score(14);g.Stand();g.Stand();
        check(g.stageScore==500+score&&g.coins==money+rules.successCoins&&g.totalBusts==0,"Rescued hand pays success exactly once");
        check(!g.SetAce(1,11),"Resolved hand cannot be edited");
        g.Reset(33);g.phase=RunPhase.Playing;g.unlocked[33]=true;
        g.hand.AddRange(cards(new[]{1,1,1}));foreach(var c in g.hand)c.aceChoice=11;
        g.shoe.Add(new PlayingCard(100,9,0));g.Hit();
        check(g.NeedsAceChoice&&g.SetAce(0,1)&&g.NeedsAceChoice&&g.SetAce(1,1)&&g.NeedsAceChoice&&g.SetAce(2,1)&&g.Current.total==12,"Multiple Ace changes can pass through locked intermediate totals");
        g.Reset(33);g.phase=RunPhase.Playing;g.hand.AddRange(cards(new[]{4,1}));g.hand[1].aceChoice=11;
        g.shoe.Add(new PlayingCard(100,9,0));g.Hit();
        check(g.SetAce(1,0)&&g.Current.total==14&&!g.NeedsAceChoice,"AUTO also rescues fixed11");
        g.Reset(33);g.phase=RunPhase.Playing;g.hand.AddRange(cards(new[]{10,10,1}));
        g.shoe.Add(new PlayingCard(100,3,0));g.Hit();
        check(g.lastBust&&g.phase==RunPhase.Result&&!g.NeedsAceChoice,"Unrecoverable24 still busts immediately");
        for(int remaining=0;remaining<rules.handsPerStage;remaining++)
        {
            g.Reset(33);g.phase=RunPhase.Result;g.handsPlayed=rules.handsPerStage-remaining;g.stageScore=g.Target;money=g.coins;
            int expected=rules.stageCoins+remaining*rules.coinsPerRemainingHand;
            check(g.StageReward==expected,"Clear reward preview with "+remaining+" hands left");
            g.Advance();check(g.coins==money+expected&&g.phase==RunPhase.StageClear,"Clear reward paid with "+remaining+" hands left");
            g.Advance();g.Advance();check(g.coins==money+expected,"No repeated clear payout on stage advance");
        }
        g.Reset(33);g.stage=rules.stageTargets.Length-1;g.phase=RunPhase.Result;g.handsPlayed=3;g.stageScore=g.Target;money=g.coins;
        int finalReward=g.StageReward;g.Advance();g.Advance();check(g.phase==RunPhase.Victory&&g.coins==money+finalReward,"Victory pays remaining-hand bonus once");
        g.Reset(33);g.phase=RunPhase.Result;g.handsPlayed=rules.handsPerStage;money=g.coins;g.Advance();check(g.phase==RunPhase.Defeat&&g.coins==money,"Defeat awards no clear coins");
        check(rules.numberBaseScores!=null&&rules.numberBaseScores.Length==32&&rules.numberBaseScores.All(x=>x>0),"All32 numbers have positive base scores");
        g.Reset(33);
        var lowAces=new System.Collections.Generic.List<PlayingCard>{new PlayingCard(901,1,0),new PlayingCard(902,1,1)};
        lowAces[0].aceChoice=1;lowAces[1].aceChoice=1;
        var two=TableRun.Evaluate(lowAces,g.unlocked);
        check(two.total==2&&!two.bust,"Two low aces are a safe two");
        check(g.Score(2)>g.Score(3)&&g.BaseScore(2)==320,"Two rewards more than three");
        lowAces[0].aceChoice=0;
        check(TableRun.Evaluate(lowAces,g.unlocked).total==12,"Auto ace still chooses highest safe total");
        g.Reset(33);g.levels[3]=5;g.streak=5;
        check(g.Score(3)==2431,"Max3 score uses revised base points");
        g.unlocked[33]=true;g.levels[33]=5;
        check(g.Score(33)==3740,"Max33 score uses revised base points");
        check(g.Score(3)*rules.handsPerStage>=rules.stageTargets.Last(),"Low-number score ceiling can exceed final target");
        g.Reset(33);g.coins=1000;int lastCost=g.RemoveCost;
        for(int i=0;i<8;i++) {int rank=g.deck[0].rank;int charged=g.RemoveCost;money=g.coins;g.Remove(rank);check(g.coins==money-charged&&g.RemoveCost==Math.Min(rules.removeMaxPrice,charged+rules.removeIncrease),"Removal increment and cap "+i);}
        check(g.RemoveCost==6,"Removal price stops at6");
        string result="PASS: "+checks+" core checks\n\n"+TableSimulation.Report(rules,3000);
        Debug.Log(result);return result;
    }
}
