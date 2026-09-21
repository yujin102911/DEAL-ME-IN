using System;
using System.Linq;
using System.Text;

namespace NumberTable
{
    public static class TableSimulation
    {
        public static string Report(TableRules rules,int rounds)
        {
            var sb=new StringBuilder();
            sb.AppendLine("투자 예산 60코인 / 빌드당 "+rounds.ToString("N0")+"핸드 / 동일 시드 3301");
            sb.AppendLine("덱은 실제와 동일하게 소진 시 재셔플 · 보상/추가 성장은 제외\n");
            string[] names={"STABLE  ·  15~18 분산","FOCUS  ·  18 집중","EXTREME  ·  33 + Ace"};
            for(int mode=0;mode<3;mode++)
            {
                var g=new TableRun(rules); g.Reset(3301); g.SkipStamp(); g.coins=60;
                if(mode==0)
                {
                    foreach(int n in new[]{15,16,17,18,17,18})g.Upgrade(n);
                    g.Remove(13);g.Remove(12);g.Add(6);
                }
                else if(mode==1) { for(int i=0;i<4;i++)g.Upgrade(18);g.Add(6);g.Add(6);g.Add(6); }
                else {g.Unlock(33);g.Upgrade(33);g.Upgrade(33);g.Upgrade(33);g.Add(1);g.Add(1);}
                int spent=60-g.coins,busts=0,hits=0,target=0,stands=0,successes=0;
                double net=0,gross=0,streaks=0,offTarget=0;
                g.hand.Clear();g.Shuffle();
                for(int round=0;round<rounds;round++)
                {
                    // Fixed stage-1 penalty is fully payable, so net EV isn't inflated by score-floor protection.
                    g.stageScore=g.Target;g.handsPlayed=0;g.phase=RunPhase.Result;g.Deal();
                    while(g.phase==RunPhase.Playing)
                    {
                        g.HitForecast(out float probability,out float expected);
                        if(expected>g.Score(g.Current.total)+0.5 && g.hand.Count<12) { g.Hit();hits++; }
                        else {g.Stand();stands++;}
                    }
                    net+=g.lastPoints;streaks+=g.streak;
                    if(g.lastBust)busts++;
                    else
                    {
                        successes++;gross+=g.lastPoints;
                        bool match=mode==0 ? g.lastNumber>=15&&g.lastNumber<=18 : g.lastNumber==(mode==1?18:33);
                        if(match)target++;else offTarget+=g.lastPoints;
                    }
                }
                sb.AppendLine(names[mode]+"   /   사용 "+spent+" C   /   덱 "+g.deck.Count+"장");
                sb.AppendLine("순점수/핸드 "+(net/rounds).ToString("0.0")+"   |   BUST "+(100d*busts/rounds).ToString("0.0")+"%   |   목표 도달 "+(100d*target/rounds).ToString("0.0")+"%");
                sb.AppendLine("HIT/핸드 "+((double)hits/rounds).ToString("0.00")+"   |   평균 연속 "+(streaks/rounds).ToString("0.00")+"   |   HIT 선택 "+(100d*hits/Math.Max(1,hits+stands)).ToString("0.0")+"%");
                sb.AppendLine("성공 시 평균 "+(gross/Math.Max(1,successes)).ToString("0.0")+"   |   비목표 기여/핸드 "+(offTarget/rounds).ToString("0.0")+"\n");
            }
            sb.AppendLine("순점수 = 성공 점수 합 − BUST 페널티 합, 전체 핸드 수로 나눔.");
            return sb.ToString();
        }
    }
}
