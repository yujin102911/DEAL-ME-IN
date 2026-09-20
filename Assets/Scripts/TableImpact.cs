using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NumberTable
{
    public partial class TableView
    {
        private AudioClip impactSound, coinTick;
        private AudioClip RewardTone(string title,float hz,float seconds,bool impact=false)
        {
            const int rate=22050;
            var samples=new float[Mathf.CeilToInt(seconds*rate)];
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/rate, fade=Mathf.Min(1,t/.008f)*Mathf.Pow(1-t/seconds,2);
                float wave=impact?Mathf.Sin(2*Mathf.PI*(85*t-28*t*t))*.65f+Mathf.Sin(2*Mathf.PI*880*t)*Mathf.Exp(-9*t)*.2f:
                    Mathf.Sin(2*Mathf.PI*hz*t)*.45f+Mathf.Sin(2*Mathf.PI*hz*1.5f*t)*.23f+Mathf.Sin(2*Mathf.PI*hz*2.03f*t)*.12f;
                samples[i]=wave*fade;
            }
            var clip=AudioClip.Create(title,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        private void ImpactSound()
        {
            if(impactSound==null){impactSound=RewardTone("Score impact",60,.48f,true);fxSounds.Add(impactSound);}
            if(!fastForward)Play(impactSound);
        }
        private IEnumerator ScoreImpact(bool record)
        {
            // Frame the table while keeping its cards and score readable.
            var focus=Rect(effects,"Score Focus",0,0,1600,900);
            var group=focus.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;
            Box(focus,"Left Shade",0,0,290,840,new Color(0,0,0,.6f));
            Box(focus,"Right Shade",1142,0,458,840,new Color(0,0,0,.6f));
            Box(focus,"Top Shade",290,0,852,130,new Color(0,0,0,.5f));
            yield return Beat(.1f,t=>group.alpha=t);
            var trails=new List<RectTransform>();
            for(int i=0;i<Run.hand.Count;i++)trails.Add(Box(focus,"Gathered Light",0,0,8,8,gold));
            yield return Beat(.22f,t=>{
                for(int i=0;i<trails.Count;i++){
                    Vector2 from=new Vector2(716+(i-(trails.Count-1)/2f)*120,565);
                    var at=Vector2.Lerp(from,new Vector2(716,324),t*t);
                    trails[i].anchoredPosition=new Vector2(at.x,-at.y);
                }
            });
            foreach(var trail in trails)Destroy(trail.gameObject);
            if(!fastForward)audioSource.Stop();
            yield return Beat(.09f);
            var total=LabelAt("Playing Table/Total");if(total!=null)total.enabled=false;
            var score=Text(focus,"Score Slam",386,252,660,145,"+"+Run.lastPoints.ToString("N0"),record?100:86,gold,TextAnchor.MiddleCenter);
            score.rectTransform.pivot=new Vector2(.5f,.5f);score.rectTransform.anchoredPosition+=new Vector2(330,-72.5f);
            var table=page.Find("Playing Table") as RectTransform;var origin=table.anchoredPosition;
            ImpactSound();
            yield return Beat(.28f,t=>{
                float bounce=Mathf.Sin(t*Mathf.PI*3)*(1-t);
                score.transform.localScale=new Vector3(1+(1-t)*(1-t)*.45f+bounce*.08f,1-bounce*.12f,1);
                table.anchoredPosition=origin+new Vector2(Mathf.Sin(t*65),Mathf.Cos(t*51))*(1-t)*(record?5:2.5f);
            });
            table.anchoredPosition=origin;
            yield return Beat(.12f,t=>group.alpha=1-t);
            if(total!=null)total.enabled=true;
            Destroy(focus.gameObject);
        }
        private IEnumerator GoalImpact()
        {
            ImpactSound();
            yield return Spark(new Vector2(151,435),gold,18);
            var border=Rect(effects,"Goal Border",0,0,1600,900);
            var cg=border.gameObject.AddComponent<CanvasGroup>();cg.blocksRaycasts=false;
            var top=Box(border,"Top",296,134,0,4,gold);
            var bottom=Box(border,"Bottom",1136,834,0,4,gold);
            var left=Box(border,"Left",296,134,4,0,gold);
            var right=Box(border,"Right",1132,134,4,0,gold);
            yield return Beat(.2f,t=>{top.sizeDelta=new Vector2(840*t,4);bottom.sizeDelta=new Vector2(840*t,4);bottom.anchoredPosition=new Vector2(1136-840*t,-834);left.sizeDelta=right.sizeDelta=new Vector2(4,704*t);});
            var stamp=Text(border,"Goal Stamp",466,179,500,60,"목 표 달 성",40,gold,TextAnchor.MiddleCenter);
            yield return Beat(.3f,t=>stamp.transform.localScale=Vector3.one*(1+.3f*(1-t)*(1-t)));
            yield return Beat(.16f,t=>cg.alpha=1-t);
            Destroy(border.gameObject);
        }
        private IEnumerator CoinStream(int from,int to,Vector2 source)
        {
            int count=Mathf.Min(Mathf.Abs(to-from),8),arrived=0;
            var tokens=new List<RectTransform>();
            Vector2 wallet=new Vector2(1488,57),start=to>from?source:wallet,end=to>from?wallet:source;
            for(int i=0;i<count;i++){
                var token=Box(effects,"Coin",start.x,start.y,24,24,gold);
                Text(token,"Mark",0,0,24,24,"•",18,bg,TextAnchor.MiddleCenter);tokens.Add(token);
            }
            if(coinTick==null){coinTick=RewardTone("Coin tick",1450,.065f);fxSounds.Add(coinTick);}
            var walletView=page.Find("Wallet");
            float duration=.3f+(count-1)*.045f;
            yield return Beat(duration,t=>{
                int landed=0;
                for(int i=0;i<count;i++){
                    float progress=Mathf.Clamp01((t*duration-i*.045f)/.3f);
                    var at=Vector2.Lerp(start,end,progress)+new Vector2(Mathf.Sin(progress*Mathf.PI)*(i%2==0?22:-22),-Mathf.Sin(progress*Mathf.PI)*80);
                    tokens[i].anchoredPosition=new Vector2(at.x-12,-at.y+12);
                    tokens[i].gameObject.SetActive(progress<1);
                    if(progress>=1)landed++;
                }
                if(landed>arrived){arrived=landed;if(!fastForward)Play(coinTick);}
                Write("WalletValue",Mathf.RoundToInt(Mathf.Lerp(from,to,(float)arrived/count))+" 코인");
                if(walletView!=null)walletView.localScale=Vector3.one*(1+.045f*Mathf.Abs(Mathf.Sin(t*count*Mathf.PI)));
            });
            if(walletView!=null)walletView.localScale=Vector3.one;
            Write("WalletValue",to+" 코인");
            foreach(var token in tokens)Destroy(token.gameObject);
        }
    }
}
