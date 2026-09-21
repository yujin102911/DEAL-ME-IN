using System;
using System.Collections.Generic;
using System.Linq;

namespace NumberTable
{
    [Serializable]
    public class TableRules
    {
        public int[] stageTargets = { 1200, 1900, 2800, 3800, 5000 };
        public int handsPerStage = 8;
        public int shopEvery = 2;
        public int initialCoins = 18;
        public int successCoins = 4;
        public int bustCoins = 1;
        public int stageCoins = 8;
        public int coinsPerRemainingHand = 1;
        public int dealerSlots = 12;
        public int dealerFreePicks = 2;
        public int dealerCoinGift = 3;
        public int minDeck = 16;
        public int maxDeck = 40;
        public int rankLimit = 6;
        public int aceLimit = 6;
        public int addPrice = 3;
        public int acePrice = 6;
        public int removePrice = 2;
        public int removeIncrease = 1;
        public int removeMaxPrice = 6;
        // Numbers 2..33: difficulty-adjusted base points, before rarity and growth.
        public int[] numberBaseScores = {320,260,240,220,200,180,160,150,140,140,140,140,140,150,160,170,180,190,200,210,240,250,260,270,280,300,320,340,360,380,390,400};
        public int levelPrice = 6;
        public int levelIncrease = 4;
        public int unlockPrice = 8;
        public float bustTargetFraction = 0.12f;
        public float scoreScale = 10f;
        public float[] levelMultipliers = { 1f, 1.3f, 1.6f, 2f, 2.5f };
        public float[] rarityMultipliers = { 1f, 1.2f, 1.4f, 1.8f, 2.2f };
        public float[] streakBonuses = { 0f, 0.1f, 0.2f, 0.35f, 0.5f, 0.7f };
        // Fixed by number: deck edits and number levels never change rarity.
        public int[] rarity = { 5,5,4,4,3,3,2,2,2,1,1,1,1,2,2,3,3,3,3,3,3,3,3,4,4,4,4,4,4,4,4,5 };
    }

    [Serializable]
    public class PlayingCard
    {
        public int id;
        public int rank;
        public int suit;
        public int aceChoice; // 0 automatic, 1 low, 11 high; hand-local only.
        public PlayingCard(int id, int rank, int suit) { this.id=id; this.rank=rank; this.suit=suit; }
        public PlayingCard Copy() { return new PlayingCard(id, rank, suit); }
        public string Label { get { return rank==1 ? "A" : rank==11 ? "J" : rank==12 ? "Q" : rank==13 ? "K" : rank.ToString(); } }
        public int Value { get { return Math.Min(rank, 10); } }
    }

    public struct HandValue
    {
        public int total;
        public bool bust;
        public int highAutomaticAces;
        public HandValue(int n, bool b, int a=0) { total=n; bust=b; highAutomaticAces=a; }
    }

    public enum RunPhase { Welcome, Workshop, Playing, Result, StageClear, Victory, Defeat, Stamp }

    public partial class TableRun
    {
        public readonly TableRules rules;
        public readonly List<PlayingCard> deck = new List<PlayingCard>();
        public readonly List<PlayingCard> shoe = new List<PlayingCard>();
        public readonly List<PlayingCard> hand = new List<PlayingCard>();
        public readonly List<string> history = new List<string>();
        public readonly int[] levels = new int[34];
        public readonly bool[] unlocked = new bool[34];
        public RunPhase phase = RunPhase.Welcome;
        public int stage, handsPlayed, stageScore, coins, streak, selected=18, removed;
        public int totalScore, totalBusts, totalHits, lastPoints, lastNumber, lastStreak;
        public bool lastBust;
        public string notice = "숫자를 키우고, 그 숫자를 만드는 덱을 설계하세요.";
        public int seed;
        private int nextId;
        private Random rng;
        public TableRun(TableRules rules) { this.rules=rules; Reset(3301); phase=RunPhase.Welcome; }
        public int Target { get { return rules.stageTargets[stage]; } }
        public int Penalty { get { return (int)Math.Round(Target*rules.bustTargetFraction); } }
        public int HandsLeft { get { return rules.handsPerStage+bonusHands-handsPlayed; } }
        public int RemainingHandCoins { get { return Math.Max(0,HandsLeft)*rules.coinsPerRemainingHand; } }
        public int StageReward { get { return rules.stageCoins+RemainingHandCoins; } }
        public HandValue Current { get { return Evaluate(hand, unlocked); } }
        public bool NeedsAceChoice { get { return phase==RunPhase.Playing && Current.bust && !EvaluateFlexible(hand,unlocked).bust; } }
        private static HandValue EvaluateFlexible(IList<PlayingCard> cards,bool[] safe)
        {
            return Evaluate(cards.Select(c=>c.Copy()).ToList(),safe);
        }
        private void CheckDraw()
        {
            if(!Current.bust) return;
            if(NeedsAceChoice) notice="버스트 보류 · 카드 아래에서 Ace를 1 또는 자동으로 바꿔주세요.";
            else ResolveBust();
        }
        public int Stars(int n) { return n>=2 && n<=33 ? rules.rarity[n-2] : 1; }
        public float LevelMult(int n) { return rules.levelMultipliers[Math.Max(0, Math.Min(levels[n]-1, rules.levelMultipliers.Length-1))]; }
        public float RarityMult(int n) { return rules.rarityMultipliers[Stars(n)-1]; }
        public float BaseScore(int n) { return rules.numberBaseScores!=null && rules.numberBaseScores.Length==32 ? rules.numberBaseScores[n-2] : n*rules.scoreScale; }
        public float StreakMult { get { return 1f+rules.streakBonuses[Math.Min(streak,rules.streakBonuses.Length-1)]; } }

        public void Reset(int newSeed)
        {
            seed=newSeed; rng=new Random(seed); dealerRng=new Random(seed^0x5A173); stampRng=new Random(seed^0x731B); nextId=0;
            deck.Clear(); shoe.Clear(); hand.Clear(); history.Clear();
            for(int rank=1;rank<=13;rank++) for(int s=0;s<2;s++) deck.Add(new PlayingCard(nextId++,rank,s*2+(rank%2)));
            for(int n=0;n<34;n++) { levels[n]=1; unlocked[n]=n>=2 && n<=21; }
            stage=0; handsPlayed=stageScore=streak=removed=totalScore=totalBusts=totalHits=0;
            lastPoints=lastNumber=lastStreak=0; lastBust=false; coins=rules.initialCoins; selected=18;
            bonusHands=0; EnterWorkshop();
        }

        public static HandValue Evaluate(IList<PlayingCard> cards, bool[] safe)
        {
            int low=0, flexible=0;
            foreach(var c in cards)
            {
                if(c.rank!=1) low+=c.Value;
                else if(c.aceChoice==11) low+=11;
                else { low+=1; if(c.aceChoice==0) flexible++; }
            }
            for(int high=flexible; high>=0; high--)
            {
                int n=low+high*10;
                if(n>=2 && n<=33 && safe[n]) return new HandValue(n,false,high);
            }
            return new HandValue(low,true);
        }

        public int Score(int n, int priorStreak=-1, int previewLevel=0)
        {
            if(n<2 || n>33 || !unlocked[n]) return 0;
            float bonus=1f+rules.streakBonuses[Math.Min(priorStreak<0 ? streak : priorStreak, rules.streakBonuses.Length-1)];
            float level=previewLevel>0 ? rules.levelMultipliers[Math.Min(previewLevel,rules.levelMultipliers.Length)-1] : LevelMult(n);
            return (int)Math.Round(BaseScore(n)*level*RarityMult(n)*bonus,MidpointRounding.AwayFromZero);
        }

        public void Shuffle()
        {
            var used=new HashSet<int>(hand.Select(c=>c.id));
            shoe.Clear(); shoe.AddRange(deck.Where(c=>!used.Contains(c.id)).Select(c=>c.Copy()));
            for(int i=shoe.Count-1;i>0;i--) { int j=rng.Next(i+1); var c=shoe[i]; shoe[i]=shoe[j]; shoe[j]=c; }
        }
        private void Draw()
        {
            if(shoe.Count==0) Shuffle();
            if(shoe.Count==0) return;
            int last=shoe.Count-1; hand.Add(shoe[last].Copy()); shoe.RemoveAt(last);
        }
        public void Deal()
        {
            if(phase!=RunPhase.Workshop && phase!=RunPhase.Result) return;
            hand.Clear(); handsPlayed++; lastPoints=0; lastBust=false;
            Draw(); Draw(); phase=RunPhase.Playing;
            notice="HIT으로 한 장 더. STAND로 지금 숫자를 확정.";
            CheckDraw();
        }
        public void LeaveWorkshop()
        {
            if(phase!=RunPhase.Workshop) return;
            offers.Clear(); hand.Clear(); Shuffle(); Deal();
        }
        public void Hit()
        {
            if(phase!=RunPhase.Playing || NeedsAceChoice) return;
            totalHits++; Draw();
            if(Current.bust) CheckDraw();
            else notice=Current.total==selected ? "선택한 숫자에 도달했어요. STAND로 점수를 받으세요." : "한 장 더? 남은 덱의 위험을 확인하세요.";
        }
        public bool SetAce(int index,int value)
        {
            if(phase!=RunPhase.Playing || index<0 || index>=hand.Count || hand[index].rank!=1 || (value!=0 && value!=1 && value!=11)) return false;
            bool wasPending=NeedsAceChoice;
            int before=hand[index].aceChoice; hand[index].aceChoice=value;
            if(Current.bust && !(wasPending && NeedsAceChoice)) { hand[index].aceChoice=before; notice="그 Ace 값은 바로 BUST가 돼요. 안전한 값만 선택할 수 있어요."; return false; }
            if(NeedsAceChoice) { notice="다른 Ace 값도 바꿔 안전한 합계를 만들어주세요."; return true; }
            notice=value==0 ? "Ace를 가장 높은 안전 합계로 자동 계산합니다." : "Ace 값을 "+value+"로 고정했어요. 자동으로 되돌릴 수도 있어요.";
            return true;
        }
        public void Stand()
        {
            if(phase!=RunPhase.Playing || NeedsAceChoice) return;
            var v=Current; if(v.bust) { ResolveBust(); return; }
            lastNumber=v.total; lastStreak=streak; lastPoints=Score(v.total); stageScore+=lastPoints; totalScore+=lastPoints;
            streak++; coins+=rules.successCoins; lastBust=false; phase=RunPhase.Result;
            history.Add(v.total+"  +"+lastPoints);
            notice="숫자 "+v.total+" 확정 · +"+lastPoints+"점 · 코인 +"+rules.successCoins;
        }
        private void ResolveBust()
        {
            lastNumber=Current.total; lastStreak=streak; lastPoints=-Math.Min(stageScore,Penalty); stageScore=Math.Max(0,stageScore-Penalty);
            streak=0; totalBusts++; coins+=rules.bustCoins; lastBust=true; phase=RunPhase.Result;
            history.Add("BUST  "+lastPoints); notice="잠긴 숫자 또는 33 초과 · 연속 성공 초기화 · 점수는 0 아래로 내려가지 않아요.";
        }
        public void Advance()
        {
            if(phase==RunPhase.Result)
            {
                if(stageScore>=Target) { phase=stage==rules.stageTargets.Length-1 ? RunPhase.Victory : RunPhase.StageClear; coins+=StageReward; return; }
                if(HandsLeft<=0) { phase=RunPhase.Defeat; return; }
                if(handsPlayed%rules.shopEvery==0) EnterWorkshop();
                else Deal();
            }
            else if(phase==RunPhase.StageClear) { stage++; stageScore=0; handsPlayed=0; bonusHands=0; EnterWorkshop(); }
        }
        public int UpgradeCost(int n) { return rules.levelPrice+(levels[n]-1)*rules.levelIncrease; }
        public int UnlockCost(int n) { return rules.unlockPrice+(n-22)/2; }
        public int AddCost(int rank) { return rank==1 ? rules.acePrice : rules.addPrice; }
        public int RemoveCost { get { return Math.Min(rules.removeMaxPrice,rules.removePrice+removed*rules.removeIncrease); } }
        public int RankCount(int rank) { return deck.Count(c=>c.rank==rank); }
        public bool CanUpgrade(int n) { return phase==RunPhase.Workshop && unlocked[n] && levels[n]<rules.levelMultipliers.Length && coins>=UpgradeCost(n); }
        public bool CanUnlock(int n) { return phase==RunPhase.Workshop && n>=22 && n<=33 && !unlocked[n] && coins>=UnlockCost(n); }
        public bool Upgrade(int n)
        {
            if(!CanUpgrade(n)) return false; coins-=UpgradeCost(n); levels[n]++; selected=n; notice=n+" 레벨 "+levels[n]+" · 이제 이 숫자를 노려보세요."; return true;
        }
        public bool Unlock(int n)
        {
            if(!CanUnlock(n)) return false; coins-=UnlockCost(n); unlocked[n]=true; selected=n; notice=n+" 해금 · 이 숫자에 정확히 도달하면 안전합니다. 사이의 잠긴 숫자는 여전히 BUST예요."; return true;
        }
        public bool CanAdd(int rank) { return phase==RunPhase.Workshop && deck.Count<rules.maxDeck && RankCount(rank)<(rank==1 ? rules.aceLimit : rules.rankLimit) && coins>=AddCost(rank); }
        public bool Add(int rank)
        {
            if(!CanAdd(rank)) return false; coins-=AddCost(rank); deck.Add(new PlayingCard(nextId++,rank,rng.Next(4))); notice="카드를 추가했어요. 정비를 마치면 덱 전체를 다시 섞습니다."; return true;
        }
        public bool CanRemove(int rank) { return phase==RunPhase.Workshop && deck.Count>rules.minDeck && RankCount(rank)>0 && coins>=RemoveCost; }
        public bool Remove(int rank)
        {
            if(!CanRemove(rank)) return false; int paid=RemoveCost; coins-=paid; removed++; deck.RemoveAt(deck.FindIndex(c=>c.rank==rank)); notice="카드 제거 완료 · −"+paid+"코인 지출 · 다음 제거는 "+RemoveCost+"코인"; return true;
        }

        public List<PlayingCard> PossibleNext()
        {
            if(shoe.Count>0) return shoe;
            var ids=new HashSet<int>(hand.Select(c=>c.id)); return deck.Where(c=>!ids.Contains(c.id)).ToList();
        }
        public void HitForecast(out float bustChance,out float ev)
        {
            bustChance=0; ev=0; var next=PossibleNext(); if(next.Count==0) return;
            foreach(var card in next)
            {
                var candidate=new List<PlayingCard>(hand); candidate.Add(card.Copy()); var v=Evaluate(candidate,unlocked);
                if(v.bust) v=EvaluateFlexible(candidate,unlocked);
                if(v.bust) { bustChance++; ev-=Math.Min(stageScore,Penalty); } else ev+=Score(v.total);
            }
            bustChance/=next.Count; ev/=next.Count;
        }
    }
}
