using System;
using System.Linq;
using NumberTable;

public static class StampChecks
{
    public static string Run()
    {
        int count=0;Action<bool,string> check=(ok,message)=>{count++;if(!ok)throw new Exception(message);};
        var rules=new TableRules();var g=new TableRun(rules);
        for(int seed=0;seed<100;seed++)
        {
            g.Reset(seed);
            check(g.phase==RunPhase.Stamp&&g.stamps.Count==3,"Three candidates before dealer");
            check(g.stamps.Any(s=>s.IsKey),"Key guaranteed with locked numbers");
            check(g.stamps.Select((s,i)=>g.FirstStampAnchor(i)).All(n=>n>=2),"Every offered shape has a useful placement");
            check(g.offers.All(o=>o.kind!=OfferKind.Level&&o.kind!=OfferKind.Unlock),"Dealer no longer generates number upgrades");
            check(!g.CanReveal(5)&&!g.CanApply(0),"Dealer blocked during stamping");
            g.LeaveWorkshop();check(g.phase==RunPhase.Stamp&&g.hand.Count==0,"Cannot deal before finishing stamp step");
            var control=new TableRun(rules);control.Reset(seed);control.SkipStamp();control.LeaveWorkshop();
            var offer=g.stamps[0];int anchor=g.FirstStampAnchor(0),coins=g.coins;
            check(g.ApplyStamp(0,anchor)&&g.phase==RunPhase.Workshop&&g.stamps.Count==0,"One stamp consumes all candidates");
            check(!g.ApplyStamp(0,anchor)&&g.coins==coins,"Repeat apply rejected, no coin charge");
            g.LeaveWorkshop();check(g.hand.Select(c=>c.id).SequenceEqual(control.hand.Select(c=>c.id)),"Stamp RNG preserves deck draw order");
        }
        g.Reset(1);g.stamps.Clear();g.stamps.Add(new StampOffer(2)); // T: 12,13,14,18
        int before=g.Score(18);check(g.StampCells(g.stamps[0],12).SequenceEqual(new[]{12,13,14,18}),"Fixed T orientation");
        int[] levels=(int[])g.levels.Clone();
        check(g.CanStamp(g.stamps[0],12)&&g.levels.SequenceEqual(levels),"Preview is pure");
        check(!g.ApplyStamp(0,6)&&!g.ApplyStamp(0,32)&&g.levels.SequenceEqual(levels),"Edges and missing last-row cells rejected atomically");
        check(g.ApplyStamp(0,12)&&g.levels[18]==2&&g.Score(18)>before,"Stamp grows covered totals");
        check(g.levels.Where((lv,n)=>n>=2&&!new[]{12,13,14,18}.Contains(n)).All(lv=>lv==1),"Outside cells unchanged");
        g.EnterWorkshop();g.stamps.Clear();g.stamps.Add(new StampOffer(2));g.ApplyStamp(0,12);
        check(g.levels[18]==3,"Can stamp same cell again in later workshop");
        g.Reset(2);g.stamps.Clear();g.stamps.Add(new StampOffer(2,3)); // 17,18,19 with key at 23
        check(!g.ApplyStamp(0,12),"Key must land on locked cell");
        check(g.ApplyStamp(0,17)&&g.unlocked[23]&&!g.unlocked[22]&&!g.unlocked[24],"Only marked key cell unlocks");
        check(g.levels[23]==1&&g.levels[17]==2&&g.levels[18]==2&&g.levels[19]==2,"Key only unlocks; open neighbors grow");
        g.Reset(3);g.stamps.Clear();g.stamps.Add(new StampOffer(2));
        g.ApplyStamp(0,17);check(!g.unlocked[23]&&g.levels[23]==1,"Growth never unlocks or grows locked cell");
        for(int last=22;last<=33;last++)
        {
            g.Reset(last);for(int n=2;n<=33;n++){g.unlocked[n]=n!=last;g.levels[n]=5;}
            g.EnterWorkshop();check(g.stamps.Any(s=>s.IsKey),"Key exists for last locked number "+last);
            int i=g.stamps.FindIndex(s=>s.IsKey),anchor=g.FirstStampAnchor(i);
            check(g.ApplyStamp(i,anchor)&&g.unlocked[last],"Isolated lock reachable including bottom row "+last);
            g.EnterWorkshop();check(g.stamps.All(s=>!s.IsKey),"No keys once all numbers open");
        }
        g.Reset(5);for(int n=2;n<=33;n++){g.unlocked[n]=true;g.levels[n]=5;}g.EnterWorkshop();
        check(g.phase==RunPhase.Workshop&&g.stamps.Count==0,"Fully maxed board skips stamp step");
        g.Reset(6);int[] old=(int[])g.levels.Clone();g.SkipStamp();g.SkipStamp();
        check(g.phase==RunPhase.Workshop&&g.levels.SequenceEqual(old),"Skip is idempotent without upgrades");
        g.Reset(6);check(g.levels.Skip(2).All(n=>n==1)&&!g.unlocked[22],"New run clears stamp progression");
        return "PASS: "+count+" stamp checks";
    }
}
