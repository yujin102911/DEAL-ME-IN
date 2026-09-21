using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NumberTable
{
    // Presentation owns only temporary visuals. TableRun remains the sole authority for rewards.
    public partial class TableView
    {
        public bool IsPresenting { get; private set; }
        private Transform effects;
        private Coroutine presentation;
        private bool fastForward;
        private int bestHand;
        private int? visualCoins,visualScore;
        private readonly List<AudioClip> fxSounds=new List<AudioClip>();
        private void OnDisable() { CancelPresentation(); }
        public void SkipPresentation() { fastForward=true;if(audioSource!=null)audioSource.Stop(); }
        private void CancelPresentation()
        {
            if(presentation!=null)StopCoroutine(presentation);
            if(audioSource!=null)audioSource.Stop();
            RestoreImpact();
            presentation=null;IsPresenting=false;fastForward=false;visualCoins=visualScore=null;
            if(page!=null){var input=page.GetComponent<CanvasGroup>();if(input!=null){input.interactable=true;input.blocksRaycasts=true;}}
            if(effects!=null){effects.gameObject.SetActive(false);Destroy(effects.gameObject);effects=null;}
        }
        private void Present(Action action,string label,bool draw=false,int offerIndex=-1,OfferKind kind=OfferKind.Level,int target=0)
        {
            if(IsPresenting)return;
            IsPresenting=true;fastForward=false;
            presentation=StartCoroutine(Presentation(action,label,draw,offerIndex,kind,target));
        }
        private RectTransform FxRoot()
        {
            var r=Rect(canvas,"Presentation",0,0,1600,900);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=Vector2.zero;
            effects=r;Button(r,"Skip FX",655,850,290,32,"SPACE · 연출 건너뛰기",SkipPresentation,panel,dim,12);
            return r;
        }
        private IEnumerator Beat(float seconds,Action<float> update=null)
        {
            float elapsed=0;
            while(elapsed<seconds&&!fastForward)
            {
                update?.Invoke(Mathf.Clamp01(elapsed/seconds));
                elapsed+=Time.unscaledDeltaTime;yield return null;
            }
            update?.Invoke(1);
        }
        private AudioClip[] rewardNotes;
        private void Note(int step)
        {
            if(rewardNotes==null){rewardNotes=new AudioClip[6];for(int i=0;i<6;i++){rewardNotes[i]=RewardTone("reward "+i,330*Mathf.Pow(2,i/5f),.18f);fxSounds.Add(rewardNotes[i]);}}
            if(!fastForward)Play(rewardNotes[Mathf.Clamp(step,0,5)]);
        }
        private Text LabelAt(string path){var t=page.Find(path);return t==null?null:t.GetComponent<Text>();}
        private void Write(string path,string value){var t=LabelAt(path);if(t!=null)t.text=value;}
        private IEnumerator Pulse(Transform t,float strength=.16f)
        {
            if(t==null)yield break;
            yield return Beat(.25f,v=>{if(t!=null)t.localScale=Vector3.one*(1+Mathf.Sin(v*Mathf.PI)*strength);});
        }
        private Vector2 NumberPosition(int n){int i=Mathf.Clamp(n,2,33)-2;return new Vector2(1214+(i%5)*74,259+(i/5)*55);}
        private IEnumerator Fly(Vector2 from,Vector2 to,string label,Color color,float duration=.38f,bool cardShape=false)
        {
            float height=cardShape?94:40;
            var token=Box(effects,"Flying Reward",from.x-32,from.y-height/2,64,height,color);
            Text(token,"Label",2,2,60,height-4,label,cardShape?28:16,bg,TextAnchor.MiddleCenter);
            yield return Beat(duration,t=>{
                var at=Vector2.Lerp(from,to,t)+new Vector2(0,-Mathf.Sin(t*Mathf.PI)*65);
                token.anchoredPosition=new Vector2(at.x-32,-at.y+height/2);
                token.localScale=Vector3.one*Mathf.Lerp(1,.55f,t);
            });
            Destroy(token.gameObject);
        }
        private IEnumerator Spark(Vector2 center,Color color,int count=9)
        {
            var dots=new List<RectTransform>();
            for(int i=0;i<count;i++)dots.Add(Box(effects,"Spark",center.x,center.y,5,5,color));
            yield return Beat(.27f,t=>{
                for(int i=0;i<dots.Count;i++){
                    float angle=i*Mathf.PI*2/dots.Count;
                    var d=dots[i];d.anchoredPosition=new Vector2(center.x+Mathf.Cos(angle)*t*90,-center.y+Mathf.Sin(angle)*t*65);
                    d.GetComponent<Image>().color=new Color(color.r,color.g,color.b,1-t);
                }
            });
            foreach(var d in dots)Destroy(d.gameObject);
        }
        private IEnumerator Coins(int from,int to,Vector2 source,string caption)
        {
            if(to==from)yield break;
            var text=Text(effects,"Coin Transfer",486,708,460,35,caption+"  "+(to-from>=0?"+":"")+(to-from)+" 코인",20,gold,TextAnchor.MiddleCenter);
            yield return CoinStream(from,to,source);
            visualCoins=to;Destroy(text.gameObject);
        }
        private IEnumerator Presentation(Action action,string label,bool draw,int offerIndex,OfferKind kind,int target)
        {
            int beforeCoins=Run.coins,beforeScore=Run.stageScore;
            RunPhase beforePhase=Run.phase;
            visualCoins=beforeCoins;visualScore=beforeScore;
            Render();FxRoot();
            // The rules do not draw until the neutral card back has finished its anticipation.
            if(draw)
            {
                var back=Box(effects,"Draw Back",665,495,110,153,Hex("40364D"));
                Text(back,"Seal",0,30,110,80,"◇",52,gold,TextAnchor.MiddleCenter);
                Play(cardSound);
                yield return Beat(.22f,t=>back.anchoredPosition=new Vector2(Mathf.Lerp(970,665,1-Mathf.Pow(1-t,3)),-495));
                yield return Beat(.12f,t=>back.localScale=new Vector3(1-t,1,1));
                Destroy(back.gameObject);
            }
            action();LastAction=label;Render();
            if(Run.phase==RunPhase.Result&&beforePhase!=RunPhase.Result)
            {
                Write("Playing Table/Score Reward/Amount","정산 준비");
                Write("Playing Table/Calculation","합계 확정");
                Write("Playing Table/ResultNotice","점수와 코인 정산 중…");
            }
            if(draw)
            {
                // Reveal at the same moment the total/result becomes visible: no outcome-dependent pre-cue.
                var card=page.Find("Playing Table/Card "+(Run.hand.Count-1));
                if(card!=null)yield return Beat(.12f,t=>card.localScale=new Vector3(Mathf.Max(.03f,t),1,1));
            }
            if(Run.phase==RunPhase.Playing&&!Run.Current.bust&&(draw||label=="ace"))
            {
                int n=Run.Current.total;bool grown=Run.levels[n]>1;
                Note(grown?2:0);
                yield return Pulse(page.Find("Playing Table/Total"),grown?.14f:.06f);
                if(grown){yield return Fly(NumberPosition(n),new Vector2(716,324),"Lv."+Run.levels[n],mint,.25f);yield return Spark(new Vector2(716,324),mint,7);}
            }
            if(Run.phase==RunPhase.Result&&beforePhase!=RunPhase.Result)
            {
                Write("Playing Table/ResultNotice","점수와 코인 정산 중…");
                Write("Playing Table/Coin Reward/Caption","이번 핸드 보상 · 지급 연출 중");
                Write("Playing Table/Score Reward/Amount","0점");
                if(Run.lastBust)
                {
                    yield return BustImpact(beforeScore);
                }
                else
                {
                    int n=Run.lastNumber;bool record=bestHand>0&&Run.lastPoints>bestHand;bestHand=Math.Max(bestHand,Run.lastPoints);
                    float value=Run.BaseScore(n);
                    float[] values={value,value*Run.LevelMult(n),value*Run.LevelMult(n)*Run.RarityMult(n),Run.lastPoints};
                    string[] labels={"기본 점수","성장 Lv."+Run.levels[n]+"  ×"+Run.LevelMult(n).ToString("0.0"),"희귀도  ×"+Run.RarityMult(n).ToString("0.0"),"연속 성공  ×"+(1+Run.rules.streakBonuses[Math.Min(Run.lastStreak,Run.rules.streakBonuses.Length-1)]).ToString("0.00")};
                    for(int i=0;i<values.Length;i++)
                    {
                        Write("Playing Table/Calculation",labels[i]);Note(i);
                        float from=i==0?0:values[i-1],to=values[i];
                        yield return ScoreStep(from,to,labels[i],i);
                        if(i==1&&Run.levels[n]>1)yield return Pulse(page.Find("Number Collection/Number "+n),.18f);
                    }
                    Write("Playing Table/Score Reward/Amount","+"+Run.lastPoints.ToString("N0")+"점");
                    yield return ScoreImpact(record);
                    if(record)
                    {
                        var badge=Text(effects,"Run Record",476,188,480,38,"이번 런 최고 기록!",24,gold,TextAnchor.MiddleCenter);
                        Note(5);yield return Spark(new Vector2(716,330),gold,14);Destroy(badge.gameObject);
                    }
                    else yield return Pulse(page.Find("Playing Table/Total"),Run.levels[n]>1?.18f:.08f);
                    yield return Fly(new Vector2(550,463),new Vector2(151,390),"+점수",mint,.28f);
                }
                yield return Beat(.22f,t=>{
                    int score=Mathf.RoundToInt(Mathf.Lerp(beforeScore,Run.stageScore,t));Write("Run Status/Score",score.ToString("N0"));
                    var bar=page.Find("Run Status/Progress") as RectTransform;if(bar!=null)bar.sizeDelta=new Vector2(196*Mathf.Clamp01((float)score/Run.Target),6);
                });
                visualScore=Run.stageScore;
                if(!Run.lastBust&&beforeScore<Run.Target&&Run.stageScore>=Run.Target)yield return GoalImpact();
                yield return Coins(beforeCoins,Run.coins,new Vector2(881,462),"핸드 보상");
            }
            else if((Run.phase==RunPhase.StageClear||Run.phase==RunPhase.Victory)&&beforePhase==RunPhase.Result)
            {
                Write("Stage Result/Clear Coins","클리어 보상 정산 중…");
                yield return Spark(new Vector2(716,344),gold,16);
                int baseEnd=beforeCoins+Run.rules.stageCoins;
                yield return Coins(beforeCoins,baseEnd,new Vector2(716,584),"클리어 기본 보상");
                yield return Coins(baseEnd,Run.coins,new Vector2(716,584),"남은 "+Run.HandsLeft+"핸드 보너스");
            }
            else if(offerIndex>=0&&Run.offers[offerIndex].applied)
            {
                Vector2 from=new Vector2(386+(offerIndex%6)*132,406+(offerIndex/6)*185);
                if(kind==OfferKind.Level||kind==OfferKind.Unlock)
                {
                    yield return Fly(from,NumberPosition(target),target.ToString(),gold);
                    Note(3);yield return Pulse(page.Find("Number Collection/Number "+target),.25f);
                    yield return Spark(NumberPosition(target),gold);
                }
                else if(kind==OfferKind.Add)
                {
                    yield return Fly(from,new Vector2(1140,56),new PlayingCard(0,target,0).Label,ink,.38f,true);
                    Note(2);yield return Pulse(page.Find("Deck"));
                }
                else if(kind==OfferKind.Remove)
                {
                    var halves=new List<RectTransform>();string rank=new PlayingCard(0,target,0).Label;
                    for(int i=0;i<2;i++) {var h=Box(effects,"Torn Card",from.x-45+i*45,from.y-55,44,110,ink);Text(h,"Rank",4,20,36,60,i==0?rank:"",28,bg,TextAnchor.MiddleCenter);halves.Add(h);}
                    Play(cardSound);yield return Beat(.42f,t=>{for(int i=0;i<2;i++){halves[i].anchoredPosition=new Vector2(from.x-45+i*45+(i==0?-1:1)*t*65,-from.y+55-t*55);halves[i].localEulerAngles=new Vector3(0,0,(i==0?1:-1)*t*25);halves[i].GetComponent<Image>().color=new Color(ink.r,ink.g,ink.b,1-t);}});
                    foreach(var h in halves)Destroy(h.gameObject);yield return Pulse(page.Find("Deck"));
                }
                else if(kind==OfferKind.Hand){yield return Fly(from,new Vector2(150,499),"+1",mint);Note(3);yield return Pulse(page.Find("Run Status/Hands"));}
                if(kind==OfferKind.Coins)
                {
                    // Show the purchase charge separately from the coin gift, including a net-zero purchase.
                    int paid=beforeCoins+Run.rules.dealerCoinGift-Run.coins;
                    yield return Coins(beforeCoins,beforeCoins-paid,from,"선택 비용");
                    yield return Coins(beforeCoins-paid,Run.coins,from,"코인 카드");
                }
                else yield return Coins(beforeCoins,Run.coins,from,"선택 비용");
            }
            visualCoins=visualScore=null;IsPresenting=false;fastForward=false;
            if(effects!=null){effects.gameObject.SetActive(false);Destroy(effects.gameObject);effects=null;}
            presentation=null;Render();
        }
    }
}
