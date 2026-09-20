using System.Linq;
using UnityEngine;

namespace NumberTable
{
    public partial class TableView
    {
        private int justRevealed=-1;
        public void PickDealerCard(int index)
        {
            if(IsPresenting)return;
            if(Run.RevealOffer(index)){justRevealed=index;Play(cardSound);LastAction="reveal";Render();}
        }
        public void UseDealerCard(int index)
        {
            if(IsPresenting||!Run.CanApply(index))return;
            var offer=Run.offers[index];
            var kind=offer.kind;int target=offer.target;
            Present(()=>Run.ApplyOffer(index),"apply",false,index,kind,target);
        }
        private void Workshop()
        {
            var p=Box(page,"Dealer Table",296,134,840,704,Hex("25252F"));
            // A simple masked dealer portrait, drawn with native UI shapes.
            var portrait=Box(p,"Dealer Portrait",28,22,88,95,Hex("161922"));
            Box(portrait,"Coat",17,60,54,30,Hex("464053"));
            Box(portrait,"Face",26,29,36,38,Hex("C9BDA5"));
            Box(portrait,"Mask",26,38,36,12,Hex("23222E"));
            Box(portrait,"EyeLeft",33,42,5,3,gold);Box(portrait,"EyeRight",50,42,5,3,gold);
            Box(portrait,"Hat",20,9,49,24,Hex("393246"));Box(portrait,"Brim",10,29,69,6,gold);
            Text(p,"Dealer Name",139,20,620,24,"THE DEALER  /  미스터 딜",12,gold);
            Text(p,"Dealer Greeting",138,47,667,38,"“두 장은 내 선물. 어디 한번 골라봐.”",25,ink);
            Text(p,"Dealer Explain",140,91,660,22,"공개된 5장 중 2장 무료 선택 · 선택 즉시 적용 · 뒷면은 코인으로 추가 공개",13,dim);
            var price=Box(p,"Draw Cost",28,128,784,43,Hex("3B3344"));
            string cost=Run.FreePicksLeft>0?"무료 선택 "+Run.FreePicksLeft+"장 남음":"추가 선택  "+Run.NextPickCost+"코인";
            Text(price,"Price",15,6,360,31,cost,20,gold);
            Text(price,"Cost Rule",380,8,389,28,Run.offers.All(o=>o.revealed)?"모든 후보 공개 완료":"추가 공개 "+Run.NextRevealCost+"코인 · 선택 비용과 별도",13,ink,TextAnchor.MiddleRight);
            for(int i=0;i<Run.offers.Count;i++)
            {
                int index=i;var offer=Run.offers[i];float x=28+(i%6)*132,y=187+(i/6)*185;
                if(!offer.revealed)
                {
                    var back=Button(p,"Dealer Pick "+i,x,y,124,171,"",()=>PickDealerCard(index),Hex("40364D"),gold,16,Run.CanReveal(i));
                    Box(back,"TopRule",13,16,98,1,Hex("8B735C"),false);Box(back,"BottomRule",13,125,98,1,Hex("8B735C"),false);
                    Text(back,"Seal",10,32,104,67,"◇",56,gold,TextAnchor.MiddleCenter);
                    Text(back,"Index",10,99,104,22,(i+1).ToString("00"),14,gold,TextAnchor.MiddleCenter,true);
                    Text(back,"Pick Cost",6,137,112,25,Run.coins>=Run.NextRevealCost?Run.NextRevealCost+"코인 · 공개":"공개 · 코인 부족",12,ink,TextAnchor.MiddleCenter);
                }
                else
                {
                    var face=Box(p,"Dealer Face "+i,x,y,124,171,offer.applied?Hex("30393B"):Hex("EEE6D6"));
                    if(i==justRevealed)face.gameObject.AddComponent<DealerReveal>();
                    Color fore=offer.applied?dim:bg;
                    string type=offer.kind==OfferKind.Level?"GROWTH":offer.kind==OfferKind.Unlock?"UNLOCK":offer.kind==OfferKind.Add?"ADD":offer.kind==OfferKind.Remove?"REMOVE":offer.kind==OfferKind.Coins?"FORTUNE":"EXTRA HAND";
                    Text(face,"Type",6,9,112,20,type,10,offer.applied?dim:Hex("796044"),TextAnchor.MiddleCenter);
                    Text(face,"Title",6,35,112,32,Run.OfferTitle(offer),22,fore,TextAnchor.MiddleCenter);
                    Text(face,"Detail",6,76,112,43,Run.OfferDescription(offer),12,fore,TextAnchor.MiddleCenter);
                    Button(face,"Dealer Apply "+i,8,130,108,31,offer.applied?"선택 완료":Run.NextPickCost==0?"무료로 선택":Run.NextPickCost+"코인 · 선택",()=>UseDealerCard(index),offer.applied?line:Hex("354B43"),ink,13,Run.CanApply(i));
                }
            }
            justRevealed=-1;
            Text(p,"Dealer Notice",30,565,780,32,Run.notice,13,mint,TextAnchor.MiddleCenter);
            int pending=Run.offers.Count(o=>o.revealed&&!o.applied);
            Text(p,"Departure Note",30,604,780,22,"떠나면 미선택 "+pending+"장 소멸 · 다음 정비: 5장 공개 / 2장 무료 선택",12,dim,TextAnchor.MiddleCenter);
            Button(p,"Deal",235,639,370,45,"정비 마치고  DEAL  →",StartHand,gold,bg,19);
        }
    }
    public class DealerReveal : MonoBehaviour
    {
        private float started;
        private void Awake(){started=Time.unscaledTime;transform.localScale=new Vector3(.04f,1,1);}
        private void Update()
        {
            float t=Mathf.Clamp01((Time.unscaledTime-started)/.2f);
            transform.localScale=new Vector3(Mathf.Lerp(.04f,1,1-Mathf.Pow(1-t,3)),1,1);
            if(t>=1)Destroy(this);
        }
    }
}
