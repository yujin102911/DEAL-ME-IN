using System;
using System.Linq;
using NumberTable;
using UnityEngine;
using UnityEditor;

public static class DealerChecks
{
    [MenuItem("Tools/Number Table/Check Dealer")]
    public static void MenuChecks(){Debug.Log(Run());}
    public static string Run()
    {
        int count=0;Action<bool,string> check=(ok,msg)=>{count++;if(!ok)throw new Exception(msg);};
        var rules=new TableRules();var asset=Resources.Load<TextAsset>("TableBalance");if(asset!=null)JsonUtility.FromJsonOverwrite(asset.text,rules);
        var g=new TableRun(rules);g.Reset(123);g.SkipStamp();
        int money=g.coins;
        check(g.offers.Count==12&&g.offers.Count(o=>o.revealed)==5,"Five visible, seven hidden");
        check(g.FreePicksLeft==2&&g.NextPickCost==0&&g.NextRevealCost==1,"Separate initial prices");
        check(g.RevealOffer(9)&&g.coins==money-1&&g.FreePicksLeft==2&&g.NextRevealCost==2,"Reveal costs one, consumes no selection");
        check(!g.RevealOffer(9)&&g.coins==money-1,"No duplicate reveal charge");
        check(g.RevealOffer(11)&&g.coins==money-3&&g.NextRevealCost==3,"Reveal price increases independently");
        g.coins=0;check(!g.RevealOffer(5)&&g.dealerReveals==2,"Unaffordable reveal blocked");
        check(!g.ApplyOffer(5),"Cannot select hidden card");
        g.offers.Clear();for(int i=0;i<5;i++)g.offers.Add(new DealerOffer(OfferKind.Hand){revealed=true});
        check(g.ApplyOffer(0)&&g.ApplyOffer(1)&&g.coins==0&&g.bonusHands==2,"Two free choices apply immediately");
        check(g.NextPickCost==2&&!g.ApplyOffer(2)&&g.dealerPicks==2,"Third choice costs two and cannot borrow");
        g.coins=5;check(g.ApplyOffer(2)&&g.coins==3&&g.NextPickCost==3,"Third choice charged two");
        check(g.ApplyOffer(3)&&g.coins==0&&g.NextPickCost==4&&g.NextRevealCost==3,"Independent choice escalation");
        check(!g.ApplyOffer(3)&&g.dealerPicks==4,"Applied card cannot be purchased twice");
        Action<OfferKind,int> one=(kind,target)=>{g.Reset(123);g.SkipStamp();g.coins=0;g.offers.Clear();g.offers.Add(new DealerOffer(kind,target){revealed=true});};
        one(OfferKind.Level,3);g.RevealOffer(0);check(g.levels[3]==1,"Revealing does not apply");g.ApplyOffer(0);
        check(g.levels[3]==2&&g.coins==0,"Level effect free");check(!g.ApplyOffer(0)&&g.levels[3]==2,"Apply only once");
        one(OfferKind.Unlock,33);g.RevealOffer(0);g.ApplyOffer(0);check(g.unlocked[33]&&!g.unlocked[32],"Unlock exact number");
        one(OfferKind.Add,1);g.RevealOffer(0);g.ApplyOffer(0);check(g.RankCount(1)==3&&g.deck.Count==27&&g.coins==0,"Free Ace addition");
        check(g.deck.Select(c=>c.id).Distinct().Count()==g.deck.Count,"Added card gets unique ID");
        one(OfferKind.Remove,13);g.RevealOffer(0);g.ApplyOffer(0);check(g.RankCount(13)==1&&g.deck.Count==25&&g.coins==0,"Free removal");
        one(OfferKind.Coins,0);g.RevealOffer(0);g.ApplyOffer(0);check(g.coins==3,"Coins effect grants3");
        one(OfferKind.Hand,0);g.RevealOffer(0);g.ApplyOffer(0);check(g.HandsLeft==9&&g.bonusHands==1,"Extra hand granted");
        g.EnterWorkshop();g.SkipStamp();check(g.bonusHands==1&&g.HandsLeft==9,"Bonus hand persists during stage");
        g.phase=RunPhase.StageClear;g.Advance();check(g.bonusHands==0&&g.HandsLeft==8,"Bonus resets next stage");
        g.SkipStamp();g.RevealOffer(0);g.LeaveWorkshop();check(g.offers.Count==0&&!g.RevealOffer(0)&&!g.ApplyOffer(0),"Leaving expires rewards and blocks dealer actions");
        g.phase=RunPhase.Result;g.handsPlayed=2;g.Advance();check(g.offers.Count==12&&g.offers.Count(o=>o.revealed)==5&&g.dealerPicks==0&&g.NextPickCost==0&&g.NextRevealCost==1,"Next workshop resets free picks");
        one(OfferKind.Level,3);g.levels[3]=4;g.offers.Add(new DealerOffer(OfferKind.Level,3){revealed=true});g.RevealOffer(0);g.RevealOffer(1);g.ApplyOffer(0);
        check(g.levels[3]==5&&g.offers[1].kind==OfferKind.Coins&&g.offers[1].exchanged&&!g.offers[1].applied&&g.coins==0,"Invalid pending reward converts without auto-apply");
        g.ApplyOffer(1);check(g.coins==3&&g.levels[3]==5,"Converted reward usable");
        one(OfferKind.Add,1);while(g.RankCount(1)<rules.aceLimit)g.deck.Add(new PlayingCard(100+g.deck.Count,1,0));
        g.offers[0].revealed=false;g.coins=1;g.RevealOffer(0);check(g.OfferUsable(g.offers[0]),"Unseen invalid reward refreshed before payment");
        one(OfferKind.Remove,13);while(g.deck.Count>rules.minDeck)g.deck.RemoveAt(0);
        g.offers[0].revealed=false;g.coins=1;g.RevealOffer(0);check(g.OfferUsable(g.offers[0]),"No removal offered below deck minimum");
        for(int seed=0;seed<100;seed++)
        {
            g.Reset(seed);g.SkipStamp();for(int n=2;n<=33;n++){g.unlocked[n]=true;g.levels[n]=5;}
            g.EnterWorkshop();g.SkipStamp();check(g.offers.All(o=>g.OfferUsable(o)&&o.kind!=OfferKind.Level&&o.kind!=OfferKind.Unlock),"No max-level or unlocked blanks seed"+seed);
        }
        var a=new TableRun(rules);var b=new TableRun(rules);a.Reset(77);a.SkipStamp();b.Reset(77);b.SkipStamp();
        a.RevealOffer(0);a.RevealOffer(3);a.RevealOffer(5);a.LeaveWorkshop();b.LeaveWorkshop();
        check(a.hand.Select(c=>c.id).SequenceEqual(b.hand.Select(c=>c.id)),"Reveal RNG does not reroll playing cards");
        g.Reset(12);g.SkipStamp();for(int i=5;i<12;i++){g.coins=100;check(g.RevealOffer(i),"All12 picks selectable");}
        check(g.NextRevealCost==8&&g.NextPickCost==0&&!g.RevealOffer(12),"Spread ends cleanly");
        return "PASS: "+count+" dealer checks";
    }
}
