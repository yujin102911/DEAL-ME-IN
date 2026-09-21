using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using NumberTable;

public static class PresentationPlayChecks
{
    public static string Status="Not run";
    private static int checks;
    private static bool previousBackground;
    public static void Start(){previousBackground=Application.runInBackground;Application.runInBackground=true;Status="Running";checks=0;TableView.Instance.StartCoroutine(Guard());}
    private static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
    private static IEnumerator Guard()
    {
        var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(Tests());
        while(stack.Count>0)
        {
            object current=null;bool more=false;
            try {more=stack.Peek().MoveNext();if(more)current=stack.Peek().Current;}
            catch(Exception e){Status="FAIL: "+e;Debug.LogError(Status);TableView.Instance.NewRun();Application.runInBackground=previousBackground;yield break;}
            if(!more){stack.Pop();continue;}
            var nested=current as IEnumerator;if(nested!=null)stack.Push(nested);else yield return current;
        }
        Status="PASS: "+checks+" presentation checks";Debug.Log(Status);TableView.Instance.NewRun();Application.runInBackground=previousBackground;
    }
    private static IEnumerator Finish(TableView v)
    {
        v.SkipPresentation();float limit=Time.realtimeSinceStartup+4;
        while(v.IsPresenting&&Time.realtimeSinceStartup<limit)yield return null;
        Check(!v.IsPresenting,"Skip completes and unlocks input");
        Check(v.GetComponentsInChildren<UnityEngine.UI.CanvasScaler>().Length==1,"No duplicate canvas");
    }
    private static void Hand(TableView v,params int[] ranks)
    {
        v.NewRun();v.Run.phase=RunPhase.Playing;v.Run.handsPlayed=1;v.Run.hand.Clear();
        for(int i=0;i<ranks.Length;i++)v.Run.hand.Add(new PlayingCard(900+i,ranks[i],0));
        v.Run.coins=20;v.Run.stageScore=100;v.Render();
    }
    private static IEnumerator Tests()
    {
        var v=TableView.Instance;
        for(int seed=0;seed<8;seed++)
        {
            v.NewRun();v.Run.Reset(seed);v.Render();var expected=new TableRun(v.Run.rules);expected.Reset(seed);expected.LeaveWorkshop();
            v.StartHand();Check(v.IsPresenting&&v.Run.hand.Count==0,"Card draw delayed behind neutral back");
            v.ActHit();v.StartHand();Check(v.Run.hand.Count==0,"Repeated input blocked during draw");
            yield return Finish(v);
            Check(v.Run.hand.Select(c=>c.id).SequenceEqual(expected.hand.Select(c=>c.id)),"Presentation preserves seeded draw order");
            Check(v.Run.coins==expected.coins&&v.Run.stageScore==expected.stageScore,"Deal rewards unchanged");
        }
        Hand(v,10,8);int points=v.Run.Score(18);v.ActStand();v.ActStand();v.Continue();
        Check(v.Run.history.Count==1&&v.Run.coins==24&&v.Run.stageScore==100+points,"Stand resolves and rewards exactly once");
        yield return Finish(v);Check(v.Run.phase==RunPhase.Result,"Skip does not advance hand");
        Hand(v,4,1);v.Run.hand[1].aceChoice=11;v.Run.shoe.Clear();v.Run.shoe.Add(new PlayingCard(999,9,0));v.ActHit();yield return Finish(v);
        Check(v.Run.NeedsAceChoice&&v.Run.history.Count==0&&v.Run.coins==20,"Ace rescue stays pending, no premature bust reward");
        v.Run.SetAce(1,1);v.ActStand();yield return Finish(v);Check(v.Run.lastNumber==14&&!v.Run.lastBust,"Ace rescue still scores correctly");
        Hand(v,10,10);v.Run.shoe.Clear();v.Run.shoe.Add(new PlayingCard(999,10,0));v.ActHit();yield return Finish(v);
        Check(v.Run.lastBust&&v.Run.coins==21&&v.Run.stageScore==0,"Bust rewards and score floor unchanged");
        Hand(v,10,8);v.Run.stageScore=v.Run.Target;v.Run.phase=RunPhase.Result;int reward=v.Run.StageReward;v.Continue();v.Continue();yield return Finish(v);
        Check(v.Run.phase==RunPhase.StageClear&&v.Run.coins==20+reward,"Clear base and remaining hand bonus paid once");
        foreach(OfferKind kind in Enum.GetValues(typeof(OfferKind)))
        {
            v.NewRun();int target=kind==OfferKind.Level?18:kind==OfferKind.Unlock?33:1;
            v.Run.offers.Clear();v.Run.offers.Add(new DealerOffer(kind,target){revealed=true});int before=v.Run.coins;
            v.UseDealerCard(0);v.UseDealerCard(0);yield return Finish(v);
            Check(v.Run.offers[0].applied&&v.Run.dealerPicks==1,"Dealer effect selected only once "+kind);
            Check(v.Run.coins==before+(kind==OfferKind.Coins?v.Run.rules.dealerCoinGift:0),"Dealer currency unchanged "+kind);
        }
        Hand(v,1,1);
        Check(v.Run.SetAce(0,1)&&v.Run.SetAce(1,1)&&v.Run.Current.total==2,"Both ace controls accept one");
        v.Render();
        Check(v.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.transform.parent.name=="Number 2"),"Two appears in number collection");
        v.Run.stageScore=v.Run.Target-1;int startingCoins=v.Run.coins;
        v.ActStand();float deadline=Time.realtimeSinceStartup+12;bool sawSlam=false,sawGoal=false,sawCoins=false;
        while(v.IsPresenting&&Time.realtimeSinceStartup<deadline)
        {
            var texts=v.GetComponentsInChildren<UnityEngine.UI.Text>();
            sawSlam|=texts.Any(t=>t.name=="Score Slam");
            sawGoal|=texts.Any(t=>t.name=="Goal Stamp");
            sawCoins|=texts.Any(t=>t.name=="Mark"&&t.transform.parent.name=="Coin");
            yield return null;
        }
        Check(!v.IsPresenting&&sawSlam&&sawGoal&&sawCoins,"Full unskipped score, goal and coin sequence completes");
        Check(v.Run.lastNumber==2&&!v.Run.lastBust&&v.Run.coins==startingCoins+v.Run.rules.successCoins,"Two scores without early clear payout");
        int clearCoins=v.Run.StageReward;v.Continue();yield return Finish(v);
        Check(v.Run.coins==startingCoins+v.Run.rules.successCoins+clearCoins,"Goal animation leaves clear payout exactly once");
        yield return ImpactChecks(v);
        Hand(v,10,8);v.ActStand();v.NewRun();yield return null;
        Check(!v.IsPresenting&&v.Run.history.Count==0&&v.Run.coins==v.Run.rules.initialCoins,"New run cancels animation safely");
    }
    private static IEnumerator ImpactChecks(TableView v)
    {
        // Observe real, unskipped verdicts, then interrupt at the impact itself.
        Hand(v,10,10);v.Run.streak=4;v.Run.stageScore=500;
        v.Run.shoe.Clear();v.Run.shoe.Add(new PlayingCard(999,10,0));
        int expected=Math.Max(0,500-v.Run.Penalty);
        v.ActHit();bool sawBust=false,sawBroken=false,captured=false;float visible=0;
        float end=Time.realtimeSinceStartup+8;
        while(v.IsPresenting&&Time.realtimeSinceStartup<end)
        {
            var texts=v.GetComponentsInChildren<UnityEngine.UI.Text>();
            bool stamp=texts.Any(t=>t.name=="Bust Stamp"&&t.text=="BUST!!");
            sawBust|=stamp;sawBroken|=texts.Any(t=>t.name=="Streak Broken"&&t.text.Contains("4"));
            if(stamp){visible+=Time.unscaledDeltaTime;if(visible>.18f&&!captured){Capture("bust.png");captured=true;}}
            yield return null;
        }
        Check(!v.IsPresenting&&sawBust&&sawBroken,"Full bust verdict and streak break shown");
        Check(v.Run.stageScore==expected&&v.Run.streak==0&&v.Run.coins==21,"Bust effects preserve exact penalty and currency");
        Check(!v.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.name=="Bust Stamp"),"Bust visuals cleaned up");

        foreach(bool cancel in new[]{false,true})
        {
            Hand(v,10,10);v.Run.shoe.Clear();v.Run.shoe.Add(new PlayingCard(999,10,0));v.ActHit();
            end=Time.realtimeSinceStartup+4;
            while(!v.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.name=="Bust Stamp")&&Time.realtimeSinceStartup<end)yield return null;
            Check(v.IsPresenting,"Bust impact reached before interrupt");
            if(cancel){v.NewRun();yield return null;Check(!v.IsPresenting&&v.Run.history.Count==0,"New run cancels active bust");}
            else{yield return Finish(v);Check(v.Run.phase==RunPhase.Result&&v.Run.totalBusts==1,"Skipping active bust resolves exactly once");}
        }
        Hand(v,1,1);v.Run.SetAce(0,1);v.Run.SetAce(1,1);v.Run.levels[2]=10;v.Render();
        int expectedPoints=v.Run.Score(2);v.ActStand();end=Time.realtimeSinceStartup+12;
        bool jackpot=false;captured=false;visible=0;
        while(v.IsPresenting&&Time.realtimeSinceStartup<end)
        {
            var texts=v.GetComponentsInChildren<UnityEngine.UI.Text>();
            bool stamp=texts.Any(t=>t.name=="Score Hype"&&t.text=="JACKPOT!!!");jackpot|=stamp;
            if(stamp){visible+=Time.unscaledDeltaTime;if(visible>.2f&&!captured){Capture("jackpot.png");captured=true;}}
            yield return null;
        }
        Check(!v.IsPresenting&&jackpot,"Stage-sized hand triggers jackpot tier");
        Check(v.Run.stageScore==100+expectedPoints&&v.Run.coins==24,"Jackpot does not inflate rewards");
    }
    private static void Capture(string name)
    {
        string directory=Environment.GetEnvironmentVariable("DEALMEIN_CAPTURE_DIR");
        if(string.IsNullOrEmpty(directory))return;
        System.IO.Directory.CreateDirectory(directory);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(directory,name));
    }
}
