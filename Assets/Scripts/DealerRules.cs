using System;
using System.Collections.Generic;
using System.Linq;

namespace NumberTable
{
    public enum OfferKind { Level, Unlock, Add, Remove, Coins, Hand }
    [Serializable]
    public class DealerOffer
    {
        public OfferKind kind;
        public int target;
        public bool revealed, applied, exchanged;
        public DealerOffer(OfferKind kind,int target=0) {this.kind=kind;this.target=target;}
    }
    public partial class TableRun
    {
        public readonly List<DealerOffer> offers=new List<DealerOffer>();
        public int dealerPicks, dealerReveals, bonusHands;
        private Random dealerRng;
        public int NextPickCost {get {return dealerPicks<rules.dealerFreePicks?0:dealerPicks-rules.dealerFreePicks+2;}}
        public int NextRevealCost {get {return dealerReveals+1;}}
        public int FreePicksLeft {get {return Math.Max(0,rules.dealerFreePicks-dealerPicks);}}
        public void EnterWorkshop()
        {
            phase=RunPhase.Workshop;dealerPicks=0;dealerReveals=0;offers.Clear();
            for(int i=0;i<rules.dealerSlots;i++)offers.Add(GenerateOffer());
            for(int i=0;i<Math.Min(5,offers.Count);i++)offers[i].revealed=true;
            notice="딜러: 다섯 장 중 두 장은 내 선물. 더 보고 싶으면 뒷면을 골라봐.";
            BeginStamps();
        }
        public bool OfferUsable(DealerOffer o)
        {
            switch(o.kind)
            {
                case OfferKind.Level:return o.target>=2&&o.target<=33&&unlocked[o.target]&&levels[o.target]<rules.levelMultipliers.Length;
                case OfferKind.Unlock:return o.target>=22&&o.target<=33&&!unlocked[o.target];
                case OfferKind.Add:return o.target>=1&&o.target<=13&&deck.Count<rules.maxDeck&&RankCount(o.target)<(o.target==1?rules.aceLimit:rules.rankLimit);
                case OfferKind.Remove:return o.target>=1&&o.target<=13&&deck.Count>rules.minDeck&&RankCount(o.target)>0;
                case OfferKind.Coins:case OfferKind.Hand:return true;
                default:return false;
            }
        }
        private DealerOffer GenerateOffer()
        {
            var pool=new List<DealerOffer>();
            Action<OfferKind,int,int> add=(kind,target,weight)=>{
                var o=new DealerOffer(kind,target);
                if(OfferUsable(o))for(int w=0;w<weight;w++)pool.Add(o);
            };
            // Per-target weights; no blank or unusable effects. Hand rewards are uncommon.
            // Number growth and unlocks now belong to the stamp board.
            for(int r=1;r<=13;r++){add(OfferKind.Add,r,3);add(OfferKind.Remove,r,3);}
            add(OfferKind.Coins,0,10);add(OfferKind.Hand,0,3);
            return pool[dealerRng.Next(pool.Count)];
        }
        public bool CanReveal(int index)
        {
            return phase==RunPhase.Workshop&&index>=0&&index<offers.Count&&!offers[index].revealed&&coins>=NextRevealCost;
        }
        public bool RevealOffer(int index)
        {
            if(!CanReveal(index))return false;
            // Unseen cards are refreshed if earlier choices made their original effect unusable.
            if(!OfferUsable(offers[index]))offers[index]=GenerateOffer();
            int cost=NextRevealCost;coins-=cost;dealerReveals++;offers[index].revealed=true;
            notice=cost+"코인으로 후보 1장 공개 · 선택 횟수는 그대로예요.";
            return true;
        }
        public bool CanApply(int index)
        {return phase==RunPhase.Workshop&&index>=0&&index<offers.Count&&offers[index].revealed&&!offers[index].applied&&coins>=NextPickCost;}
        public bool ApplyOffer(int index)
        {
            if(!CanApply(index))return false;
            var o=offers[index];if(!o.revealed||o.applied)return false;
            if(!OfferUsable(o)){o.kind=OfferKind.Coins;o.target=0;o.exchanged=true;notice="덱 상태가 바뀌어 코인 카드로 교환했어요. 원하면 적용하세요.";return false;}
            int cost=NextPickCost;coins-=cost;dealerPicks++;
            switch(o.kind)
            {
                case OfferKind.Level:levels[o.target]++;selected=o.target;break;
                case OfferKind.Unlock:unlocked[o.target]=true;selected=o.target;break;
                case OfferKind.Add:deck.Add(new PlayingCard(nextId++,o.target,rng.Next(4)));break;
                case OfferKind.Remove:deck.RemoveAt(deck.FindIndex(c=>c.rank==o.target));break;
                case OfferKind.Coins:coins+=rules.dealerCoinGift;break;
                case OfferKind.Hand:bonusHands++;break;
            }
            o.applied=true;
            // Revealed, now-invalid rewards become optional coins instead of dead cards.
            foreach(var pending in offers)if(pending.revealed&&!pending.applied&&!OfferUsable(pending))
            {pending.kind=OfferKind.Coins;pending.target=0;pending.exchanged=true;}
            notice=(cost==0?"무료 선택":cost+"코인으로 추가 선택")+" · "+OfferTitle(o)+" 효과 적용 완료";
            return true;
        }
        public string OfferTitle(DealerOffer o)
        {
            string rank=o.target>=1&&o.target<=13?new PlayingCard(0,o.target,0).Label:"";
            switch(o.kind)
            {
                case OfferKind.Level:return o.target+" 성장";
                case OfferKind.Unlock:return o.target+" 해금";
                case OfferKind.Add:return rank+" 추가";
                case OfferKind.Remove:return rank+" 제거";
                case OfferKind.Coins:return "코인 +"+rules.dealerCoinGift;
                default:return "핸드 +1";
            }
        }
        public string OfferDescription(DealerOffer o)
        {
            switch(o.kind)
            {
                case OfferKind.Level:return "레벨 +1\n"+(o.applied?"Lv."+levels[o.target]:"Lv."+levels[o.target]+" → "+(levels[o.target]+1));
                case OfferKind.Unlock:return "이 합계가\n안전해집니다";
                case OfferKind.Add:return "덱에 1장 추가\n현재 "+RankCount(o.target)+"장";
                case OfferKind.Remove:return "덱에서 1장 제거\n현재 "+RankCount(o.target)+"장";
                case OfferKind.Coins:return o.exchanged?"사용 불가 효과\n대신 코인 선물":"보유 코인\n즉시 증가";
                default:return "이번 스테이지\n기회 1회 추가";
            }
        }
    }
}
